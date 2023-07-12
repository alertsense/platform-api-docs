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
using static IdentityModel.OidcConstants;

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
        protected string _realmId;
        protected AvailableTimeZonesResponse _availableTimeZones;
        protected AvailableLanguagesResponse _availableLanguages;
        protected List<GroupSummary> _availableGroups;

        public RegisterNewUser(ITestOutputHelper output)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.TestOutput(output, Serilog.Events.LogEventLevel.Verbose)
                .CreateLogger()
                .ForContext<RegisterNewUser>();


            _tenantId = "3331";
            _directoryId = "public";
            _realmId = "civicready";

            _apiBaseUrl = "https://api-handweave.dev.alertsense.io";
            _apiBaseUrl = "http://localhost:5010";
            _authorityUrl = "https://auth.dev.alertsense.io";

            string clientId = "XXXXX";
            string clientSecret = "XXXXXXXXX";
            _apiClient = CreateAuthenticatedApiClientAsync(clientId, clientSecret).Result;
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
            // generate a fake email address to use for each execution of the test
            string testUserEmail = $"test-{Guid.NewGuid()}@nagdom.com".ToLower();

            await CreateUserFlowAsync(testUserEmail);
            await UpdateUserFlowAsync(testUserEmail);            
        }


        protected async Task CreateUserFlowAsync(string testUserEmail)
        {     
            // check to see if a user already exists with the same email address
            var existingUserProfiles = await FindExistingUser(testUserEmail);

            // if the user already exists, the test cannot run as expected, so assert a failure
            Assert.False(existingUserProfiles.Any(), $"Unable to create a new User, there is already an existing user for {testUserEmail}");

            // retrieve available groups and metadata
            await DiscoverGroupsAndRetrieveMeta();

            // Create the GroupMembership object from the Discovered Groups
            var groupMemberships = CreateTestGroupMembershipPreferences();

            // Create the UserProfile object
            var newUserProfileCommand = CreateTestUserProfileDetails(testUserEmail, groupMemberships);

            // Create the new User in the Directory with the API
            var userProfile = await CreateNewUserAsync(newUserProfileCommand);

            foreach(var email in userProfile.ContactInformation.Emails)
            {
                // Check the verification status of their contact information
                Assert.False(email.Verification.Verified);

                // Use the Test API in place of the user to obtain a verification code for each device
                await VerifyEmailAdressWithCode(userProfile.Id, email);
            }

            foreach (var phone in userProfile.ContactInformation.Phones)
            {
                // Check the verification status of their contact information
                Assert.False(phone.Verification.Verified);

                // Use the Test API in place of the user to obtain a verification code for each device
                await VerifyPhoneNumberWithCode(userProfile.Id, phone);
            }

            Assert.True(userProfile.NotificationPreferences.Locations != null);
            foreach (var location in userProfile.NotificationPreferences.Locations)
            {
                Assert.True(location.GeoLocation != null, "Location was not geocoded.");
            }
        }

        protected async Task UpdateUserFlowAsync(string testUserEmail)
        {
            // check to see if a user already exists with the same email address
            var existingUserProfiles = await FindExistingUser(testUserEmail);

            // if the user already exists, the test cannot run as expected, so assert a failure
            Assert.True(existingUserProfiles.Any(), $"Unable to edit existing User, there is no matching user for {testUserEmail}");

            var existingUser = existingUserProfiles.FirstOrDefault();

            // retrieve available groups and metadata
            await DiscoverGroupsAndRetrieveMeta();

            // Create the GroupMembership object from the Discovered Groups
            var groupMemberships = CreateTestGroupMembershipPreferences();

            // Create the UserProfile object
            var newUserProfileCommand = CreateTestUserProfileDetails(testUserEmail, groupMemberships);

            // Create the new User in the Directory with the API
            var userProfile = await CreateNewUserAsync(newUserProfileCommand);

            var secondaryEmail = $"second_{testUserEmail}";

            // add a secondary email to the user by Id
            var email = await _apiClient.UserApi.UserAddNewEmailAsync(_tenantId, _directoryId, userProfile.Id, new AddEmailCommand(ContactTargetType.Work, null, secondaryEmail, false));
            
            // Check the verification status of their contact information
            Assert.False(email.Verification.Verified);

            // Use the Test API in place of the user to obtain a verification code for each device
            await VerifyEmailAdressWithCode(userProfile.Id, email);
        }

        protected async Task<List<UserProfileSummary>> FindExistingUser(string emailAddress)
        {
            var findUsersResponse = await _apiClient.UserApi.UserFindUsersByQueryAsync(_tenantId, _directoryId, new FindUserQuery(emailAddress));
            
            return findUsersResponse.Items;
        }

        protected async Task DiscoverGroupsAndRetrieveMeta()
        {
            _availableTimeZones = await _apiClient.MetaApi.MetaGetTimeZonesAsync(_realmId);
            _availableLanguages = await _apiClient.MetaApi.MetaGetLanguagesAsync(_realmId);

            var availableGroupsResponse = await _apiClient.GroupsApi.GroupsDiscoverGroupsAsync(_tenantId, _directoryId);
            _availableGroups = availableGroupsResponse.Items;
        }               

        protected List<CreateGroupMembership> CreateTestGroupMembershipPreferences()
        {
            // can be changed to manually pick groups to join
            // returns all groups that don't have children. Users can only join groups without children
            return _availableGroups.Where(u => u.Children == null).Select(u => new CreateGroupMembership(u.Id)).ToList();
        }

        protected CreateUserProfileCommand CreateTestUserProfileDetails(string emailAddress, List<CreateGroupMembership> groupMemberships)
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
                // if display name is null, the system will provide the combintation of first and last names
                displayName: null,
                preferredWrittenLanguage: writtenLanguage?.Value,
                preferredSpokenLanguage: spokenLanguage?.Value,
                // the users preferred locale for app localization, should be left null unless the user is planning on using the mobile app to send notifications
                locale: null,
                timezone: preferredTimeZone?.Id,
                emails: new List<CreateEmailAddress>
                {
                    new CreateEmailAddress(ContactTargetType.Work, true, emailAddress)
                },
                phones: new List<CreatePhoneNumber>
                {
                    new CreatePhoneNumber(ContactTargetType.Work, true, "+12081112222")
                },
                locations: new List<CreateLocation>
                {
                    new CreateLocation(new Address("Personal", null, "1234 W North St", "City", "State", "012345", "US"))
                },
                memberships: groupMemberships//availableGroups.Items.Select(u => new CreateGroupMembership(u.Id)).ToList()
            );
            return profileToCreate;
        }

        public async Task<UserProfile> CreateNewUserAsync(CreateUserProfileCommand createUserProfileCommand)
        {
            var profile = await _apiClient.UserApi.UserCreateAsync(_tenantId, _directoryId, createUserProfileCommand);            
            return profile;
        }
        protected async Task VerifyEmailAdressWithCode(string userId, EmailAddress email)
        {
            // retrieve the verification code out of band like some kind of peasant.
            var pendingVerificationCode = await _apiClient.TestApi.TestGetPendingEmailVerificationsAsync(email.Value);
            Assert.True(pendingVerificationCode != null, $"A pending verification code was not found for {email.Value}");

            // verify the email address with the code provided out of band
            var verificationResponse = await _apiClient.UserApi.UserVerifyEmailAsync(_tenantId, _directoryId, userId, email.Value, pendingVerificationCode.VerificationCode);
            Assert.True(verificationResponse.Success);

            // fetch the email and make sure it's still verified
            var emailAddressDetails = await _apiClient.UserApi.UserGetEmailAsync(_tenantId, _directoryId, userId, email.Value);
            Assert.True(emailAddressDetails.Verification.Verified);
        }

        protected async Task VerifyPhoneNumberWithCode(string userId, PhoneNumber phone)
        {
            // retrieve the verification code out of band like some kind of peasant.
            var pendingVerificationCode = await _apiClient.TestApi.TestGetPendingPhoneVerificationsAsync(phone.Value);
            Assert.True(pendingVerificationCode != null, $"A pending verification code was not found for {phone.Value}");

            // verify the phone address with the code provided out of band
            var verificationResponse = await _apiClient.UserApi.UserVerifyPhoneAsync(_tenantId, _directoryId, userId, phone.Value, pendingVerificationCode.VerificationCode);
            Assert.True(verificationResponse.Success);

            // fetch the phone and make sure it's still verified
            var emailAddressDetails = await _apiClient.UserApi.UserGetPhoneAsync(_tenantId, _directoryId, userId, phone.Value);
            Assert.True(emailAddressDetails.Verification.Verified);
        }
    }
}
