using dotenv.net;
using Konexus.Directory.ApiClient.Model;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using static Duende.IdentityModel.OidcConstants;

namespace Konexus.Directory.ApiClient.Test.UseCases
{
    public class RegisterNewUser
    {
        ILogger _output;

        #region API Client Variables
        protected HttpClient _httpClient;
        protected HttpClientHandler _httpClientHandler;
        protected string _apiBaseUrl;
        protected string _clientId;
        protected string _clientSecret;
        protected string _authorityUrl;
        protected DirectoryClient _apiClient;
        #endregion

        protected string _tenantId;
        protected string _directoryId;
        protected AvailableTimeZonesResponse _availableTimeZones;
        protected AvailableLanguagesResponse _availableLanguages;
        protected List<GroupSummary> _availableGroups;

        protected virtual string TestUserEmail { get; set; }
        protected virtual string TestUserPhone { get; set; }
        protected virtual string ExternalId { get; set; }

        protected virtual FindUserQuery FindUserQuery { get; set; } 

        public RegisterNewUser(ITestOutputHelper output)
        {
            #region Logging Setup

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.TestOutput(output, Serilog.Events.LogEventLevel.Verbose)
                .CreateLogger()
                .ForContext<RegisterNewUser>();

            #endregion Logging Setup

            #region Endpoint and Credentials Configuration

            _tenantId = "3331";
            _directoryId = "public";

            _apiBaseUrl = "https://api-handweave.dev.alertsense.io";
            _authorityUrl = "https://auth.dev.alertsense.io";

            DotEnv.Load();
            string clientId = Environment.GetEnvironmentVariable("CLIENT_ID");
            string clientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET");

            _apiClient = CreateAuthenticatedApiClientAsync(clientId, clientSecret).Result;

            #endregion Endpoint and Credentials Configuration

            // Change to run the test as a different user
            TestUserEmail = "test-deterministic@nagdom.com";
            TestUserPhone = "1234567890";
            ExternalId = "KON1001";
            FindUserQuery = new FindUserQuery(externalId: ExternalId);

        }

        protected async Task<DirectoryClient> CreateAuthenticatedApiClientAsync(string clientId, string clientSecret)
        {
            if(_apiBaseUrl == null)
            {
                throw new ArgumentNullException("_apiBaseUrl", "API Base Url must be provided to create Api Client.");
            }

            // reuse the same client for the entire test run to prevent port exhaustion
            _httpClient = new HttpClient();
            _httpClientHandler = new HttpClientHandler();
            var apiClient = new DirectoryClient(_apiBaseUrl, _httpClient, _httpClientHandler);

            await apiClient.AuthenticateWithClientCredentialsAsync(_authorityUrl, clientId, clientSecret);

            return apiClient;
        }

        [Fact]
        public async Task ExampleUseCases()
        {
            await CreateUserFlowAsync();
            await UpdateUserFlowAsync();            
        }


        protected async Task CreateUserFlowAsync()
        {     
            // check to see if a user already exists with the same email address
            var existingUserProfiles = await FindExistingUser();

            // if the user already exists, we'll skip creationg so we can run updates
            if (existingUserProfiles.Any())
            {
                return;
            }
            // retrieve available groups and metadata
            await DiscoverGroupsAndRetrieveMeta();

            // Create the GroupMembership object from the Discovered Groups
            var groupMemberships = CreateTestGroupMembershipPreferences();

            // Create the UserProfile object
            var newUserProfileCommand = CreateTestUserProfileDetails(TestUserEmail, ExternalId, groupMemberships);

            // Create the new User in the Directory with the API
            var userProfile = await CreateNewUserAsync(newUserProfileCommand);

            foreach(var email in userProfile.ContactInformation.Emails.Items)
            {
                // Check the verification status of their contact information
                Assert.False(email.Verification.Verified);

                // Use the Test API in place of the user to obtain a verification code for each device
                await VerifyEmailAdressWithCode(userProfile.Id, email);
            }

            foreach (var phone in userProfile.ContactInformation.Phones.Items)
            {
                // Check the verification status of their contact information
                Assert.False(phone.Verification.Verified);

                // Use the Test API in place of the user to obtain a verification code for each device
                await VerifyPhoneNumberWithCode(userProfile.Id, phone);
            }

            Assert.True(userProfile.NotificationPreferences.Locations != null);
            foreach (var location in userProfile.NotificationPreferences.Locations.Items)
            {
                Assert.True(location.GeoLocation != null, "Location was not geocoded.");
            }
        }

        protected async Task<List<UserProfileSummary>> FindExistingUser()
        {
            var findUsersResponse = await _apiClient.UsersApi.UsersFindUsersByQueryAsync(_tenantId, _directoryId, FindUserQuery);
            
            return findUsersResponse.Items;
        }

        protected async Task<UserProfile> GetUserProfilesAsync(string userId)
        {
            var userProfileResponse = await _apiClient.UsersApi.UsersGetByIdAsync(_tenantId, _directoryId, userId);

            return userProfileResponse;
        }

        protected async Task DiscoverGroupsAndRetrieveMeta()
        {
            _availableTimeZones = await _apiClient.MetaApi.MetaGetTimeZonesAsync(_tenantId);
            _availableLanguages = await _apiClient.MetaApi.MetaGetLanguagesAsync(_tenantId);

            var availableGroupsResponse = await _apiClient.GroupsApi.GroupsDiscoverAvailableGroupsAsync(_tenantId, _directoryId);
            _availableGroups = availableGroupsResponse.Items;
        }               

        protected List<CreateGroupMembership> CreateTestGroupMembershipPreferences()
        {
            // can be changed to manually pick groups to join
            // returns all groups that don't have children. Users can only join groups without children
            return _availableGroups.Where(u => u.Children == null).Select(u => new CreateGroupMembership(u.Id)).ToList();
        }

        protected CreateUserProfileCommand CreateTestUserProfileDetails(string emailAddress, string externalId, List<CreateGroupMembership> groupMemberships)
        {
            // select our preferred timezone by standard name
            var preferredTimeZone = _availableTimeZones.TimeZones.FirstOrDefault(u => u.StandardName.Contains("Mountain"));

            // select our preferred spoken language by standard name
            var spokenLanguage = _availableLanguages.SpokenLanguages.FirstOrDefault(u => u.Value.StartsWith("English"));
            // select our preferred timezone by standard name
            var writtenLanguage = _availableLanguages.WrittenLanguages.FirstOrDefault(u => u.Value.StartsWith("English"));

            var profileToCreate = new Model.CreateUserProfileCommand
            (
                firstName: "John",
                lastName: "Dough",
                externalId: externalId,
                // if display name is null, the system will provide the combintation of first and last names
                displayName: null,
                preferredWrittenLanguage: writtenLanguage?.Value,
                preferredSpokenLanguage: spokenLanguage?.Value,
                // the users preferred locale for app localization, should be left null unless the user is planning on using the mobile app to send notifications
                locale: null,
                timezone: preferredTimeZone?.Id,
                emails: new List<CreateEmailCommand>
                {
                    new CreateEmailCommand(ContactTargetType.Work, true, emailAddress)
                },
                phones: new List<CreatePhoneCommand>
                {
                    new CreatePhoneCommand(ContactTargetType.Work, "+12081112222", true)
                },
                locations: new List<CreateLocationCommand>
                {
                    new CreateLocationCommand("Office", AddressType.Work, "500 E Shore Drive #240", "Eagle", "ID", "83616", "US")
                },
                memberships: groupMemberships//availableGroups.Items.Select(u => new CreateGroupMembership(u.Id)).ToList()
            );



            return profileToCreate;
        }

        public async Task<UserProfile> CreateNewUserAsync(CreateUserProfileCommand createUserProfileCommand)
        {
            var profile = await _apiClient.UsersApi.UsersCreateAsync(_tenantId, _directoryId, createUserProfileCommand);            
            return profile;
        }
        protected async Task VerifyEmailAdressWithCode(string userId, EmailAddress email)
        {
            // retrieve the verification code out of band like some kind of peasant.
            var pendingVerificationCode = await _apiClient.TestApi.TestGetPendingEmailVerificationsAsync(email.Value);
            Assert.True(pendingVerificationCode != null, $"A pending verification code was not found for {email.Value}");

            // verify the email address with the code provided out of band
            var verificationResponse = await _apiClient.UsersApi.UsersVerifyEmailAsync(_tenantId, _directoryId, userId, email.Value, new VerifyEmailCommand(pendingVerificationCode.VerificationCode));
            Assert.True(verificationResponse.Success);

            // fetch the email and make sure it's still verified
            var emailAddressDetails = await _apiClient.UsersApi.UsersGetEmailAsync(_tenantId, _directoryId, userId, email.Value);
            Assert.True(emailAddressDetails.Verification.Verified);
        }

        protected async Task VerifyPhoneNumberWithCode(string userId, PhoneNumber phone)
        {
            // retrieve the verification code out of band like some kind of peasant.
            var pendingVerificationCode = await _apiClient.TestApi.TestGetPendingPhoneVerificationsAsync(phone.Value);
            Assert.True(pendingVerificationCode != null, $"A pending verification code was not found for {phone.Value}");

            bool verified = false;

            for (int i = 0; i < 4 && !verified; i++)
            {
                try
                {
                    // verify the phone address with the code provided out of band
                    var verificationResponse = await _apiClient.UsersApi.UsersVerifyPhoneAsync(_tenantId, _directoryId, userId, phone.Value, new VerifyPhoneCommand(pendingVerificationCode.VerificationCode));
                    verified = verificationResponse.Success;
                }
                catch (Exception ex)
                {

                }
            }

            Assert.True(verified);


            // fetch the phone and make sure it's still verified
            var emailAddressDetails = await _apiClient.UsersApi.UsersGetPhoneAsync(_tenantId, _directoryId, userId, phone.Value);
            Assert.True(emailAddressDetails.Verification.Verified);
        }

        protected async Task UpdateUserFlowAsync()
        {
            // check to see if a user already exists with the same email address
            var existingUserProfiles = await FindExistingUser();

            // if the user already exists, the test cannot run as expected, so assert a failure
            Assert.True(existingUserProfiles.Any(), $"Unable to edit existing User, there is no matching user for {TestUserEmail}");

            var existingUser = existingUserProfiles.FirstOrDefault();

            // retrieve available groups and metadata
            await DiscoverGroupsAndRetrieveMeta();

            // Create the new User in the Directory with the API
            var userProfile = await GetUserProfilesAsync(existingUser.Profile.Id);

            await UpdateUserAddEmail(userProfile);
            await UpdateUserAddPhone(userProfile);
        }

        private async Task UpdateUserAddEmail(UserProfile userProfile)
        {
            var secondaryEmail = $"second_{TestUserEmail}";

            if (userProfile.ContactInformation.Emails.Items.Any(u => u.Value == secondaryEmail))
            {
                await _apiClient.UsersApi.UsersRemoveEmailAsync(_tenantId, _directoryId, userProfile.Id, secondaryEmail);

                userProfile = await GetUserProfilesAsync(userProfile.Id);
                Assert.False(userProfile.ContactInformation.Emails.Items.Any(u => u.Value == secondaryEmail), "User has secondary email that should have been removed");
            }

            // add a secondary email to the user by Id
            var email = await _apiClient.UsersApi.UsersCreateNewEmailAsync(_tenantId, _directoryId, userProfile.Id, new CreateEmailCommand(ContactTargetType.Work, false, secondaryEmail));

            // Check the verification status of their contact information
            Assert.False(email.Verification.Verified);

            // Use the Test API in place of the user to obtain a verification code for each device
            await VerifyEmailAdressWithCode(userProfile.Id, email);

            userProfile = await GetUserProfilesAsync(userProfile.Id);
            Assert.True(userProfile.ContactInformation.Emails.Items.Any(u => u.Value == secondaryEmail), "Secondary email was added to user profile");
        }

        protected virtual async Task UpdateUserAddPhone(UserProfile userProfile)
        {
            if (TestUserPhone != null)
            {
                if (userProfile.ContactInformation.Phones.Items.Any(u => u.Value == TestUserPhone))
                {
                    await _apiClient.UsersApi.UsersRemovePhoneAsync(_tenantId, _directoryId, userProfile.Id, TestUserPhone);

                    userProfile = await GetUserProfilesAsync(userProfile.Id);
                    Assert.False(userProfile.ContactInformation.Phones.Items.Any(u => u.Value == TestUserPhone), "User has primary phone that should have been removed");
                }

                var createPhoneCommand = new CreatePhoneCommand(ContactTargetType.Work, TestUserPhone, true, true);

                var phone = await _apiClient.UsersApi.UsersCreateNewPhoneAsync(_tenantId, _directoryId, userProfile.Id, createPhoneCommand);

                // Check the verification status of their contact information
                Assert.False(phone.Verification.Verified);

                // Use the Test API in place of the user to obtain a verification code for each device
                await VerifyPhoneNumberWithCode(userProfile.Id, phone);

                userProfile = await GetUserProfilesAsync(userProfile.Id);
                Assert.True(userProfile.ContactInformation.Phones.Items.Any(u => u.Value == TestUserPhone), "Primary phone was added to user profile");
            }
        }
    }
}
