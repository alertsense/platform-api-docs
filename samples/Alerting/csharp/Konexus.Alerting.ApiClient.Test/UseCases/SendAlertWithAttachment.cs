using dotenv.net;
using Konexus.Alerting.ApiClient.Model;
using Konexus.FileManagement.ApiClient;
using Konexus.FileManagement.ApiClient.Client;
using Konexus.FileManagement.ApiClient.Model;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using static Duende.IdentityModel.OidcConstants;

namespace Konexus.Alerting.ApiClient.Test.UseCases
{
    public class SendAlertWithAttachment : IAsyncLifetime
    {
        ILogger _output;

        #region API Client Variables
        protected HttpClient _httpClient;
        protected HttpClientHandler _httpClientHandler;
        protected const string _apiBaseUrl = "https://admin.dev.alertsense.io/api";
        protected const string _authorityUrl = "https://auth.dev.alertsense.io";
        protected string _clientId;
        protected string _clientSecret;
        protected AlertingClient _alertingClient;
        protected FileManagementClient _fileManagementClient;
        #endregion

        protected const int _tenantId = 1211;

        protected virtual string FilePath { get; set; }
        protected virtual string AdHocFilePath { get; set; }

        public SendAlertWithAttachment(ITestOutputHelper output)
        {
            #region Logging Setup

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.TestOutput(output, Serilog.Events.LogEventLevel.Verbose)
                .CreateLogger()
                .ForContext<SendAlertWithAttachment>();

            #endregion Logging Setup

            #region Credentials Configuration

            DotEnv.Load();
            _clientId = Environment.GetEnvironmentVariable("CLIENT_ID");
            _clientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET");

            #endregion Credentials Configuration

            string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            // See example pdf file
            FilePath = Path.Combine(assemblyLocation, "ExampleDoc.pdf");
            // See example 'ad-hoc' file - this allows sending to contacts that aren't in the system
            AdHocFilePath = Path.Combine(assemblyLocation, "ExampleContacts.csv");
        }

        public async Task InitializeAsync()
        {
            // reuse the same client for the entire test run to prevent port exhaustion
            _httpClient = new HttpClient();
            _httpClientHandler = new HttpClientHandler();

            _alertingClient = await CreateAuthenticatedAlertingClientAsync(_clientId, _clientSecret);
            _fileManagementClient = await CreateAuthenticatedFileManagementClientAsync(_clientId, _clientSecret);
        }

        public Task DisposeAsync()
        {
            _httpClient?.Dispose();
            _httpClientHandler?.Dispose();
            return Task.CompletedTask;
        }

        protected async Task<AlertingClient> CreateAuthenticatedAlertingClientAsync(string clientId, string clientSecret)
        {
            if (_apiBaseUrl == null)
            {
                throw new ArgumentNullException("_apiBaseUrl", "API Base Url must be provided to create Api Client.");
            }

            var apiClient = new AlertingClient(_apiBaseUrl, _httpClient, _httpClientHandler);

            await apiClient.AuthenticateWithClientCredentialsAsync(_authorityUrl, clientId, clientSecret);

            return apiClient;
        }

        protected async Task<FileManagementClient> CreateAuthenticatedFileManagementClientAsync(string clientId, string clientSecret)
        {
            if (_apiBaseUrl == null)
            {
                throw new ArgumentNullException("_apiBaseUrl", "API Base Url must be provided to create Api Client.");
            }

            var apiClient = new FileManagementClient(_apiBaseUrl, _httpClient, _httpClientHandler);

            await apiClient.AuthenticateWithClientCredentialsAsync(_authorityUrl, clientId, clientSecret);

            return apiClient;
        }

        [Fact]
        public async Task ExampleUseCases()
        {
            await UploadFileFlowAsync(FilePath);
            await UploadFileFlowAsync(AdHocFilePath);
            await SendAlertFlowAsync(FilePath, AdHocFilePath);
        }

        protected async Task UploadFileFlowAsync(string filePath)
        {
            // Get file info
            string fileName = Path.GetFileName(filePath);
            FileInfo fileInfo = new FileInfo(filePath);
            long contentLength = fileInfo.Length;
            string contentType = GetContentType(filePath);

            using (FileStream fileStream = File.OpenRead(filePath))
            {
                // Create FileParameter
                FileParameter fileParam = new FileParameter(fileName, contentType, fileStream);

                // Upload the file
                var fileRes = await _fileManagementClient.FilesApi.UploadFileV4Async(_tenantId, fileName, contentType, contentLength, fileParam);
                Assert.True(!string.IsNullOrEmpty(fileRes?.PublicUrl), $"Failed to get Public Url for new file: {filePath}");
            }
        }

        protected async Task SendAlertFlowAsync(string filePath, string adHocFilePath)
        {
            // Get alert defaults, will be used to build the alert
            var alertDefaultsRes = await _alertingClient.AlertsApi.GetAlertsDefaultAsync();
            Assert.True(alertDefaultsRes?.Status?.IsSuccess, "Failed to get alert defaults");
            AlertDefaults alertSettings = alertDefaultsRes.Item;

            // Create recipients using our ad-hoc file
            // there is currently a defect trying to call GET files/{FileName} for .csv files (ROAD-14471)
            // as an alternative, we can get all files and then find the one we need
            var files = await GetAllFilesAsync();
            var adHocFile = files.Find(file => file.FileName == Path.GetFileName(adHocFilePath));
            string adHocUrl = adHocFile?.PublicUrl;
            Assert.True(!string.IsNullOrEmpty(adHocUrl), $"Failed to get Public Url for ad-hoc file: {adHocFilePath}");

            var adHocRes = await CreateAdHocDistributionListAsync(adHocUrl);
            alertSettings.Recipients.AdHocFileUrl = adHocUrl;
            alertSettings.Recipients.DistributionListId = adHocRes.Id;

            // Get the public url of the file we want to attach
            var fileModel = await GetUploadedFileAsync(filePath);
            string publicUrl = fileModel?.PublicUrl;
            Assert.True(!string.IsNullOrEmpty(publicUrl), $"Failed to get Public Url for attachment: {filePath}");

            // Create a shortened url to use in the alert message
            // (optional: we can use the public url directly, but shortened urls look prettier)
            string shortenedUrl = await CreateShortenedUrlAsync(publicUrl);
            alertSettings.Message.Basic.Subject = "Alert with link";
            alertSettings.Message.Basic.Message = $"View file: {shortenedUrl}";
            alertSettings.Message.Files = new List<FileDetails>
            {
                new FileDetails
                (
                    fileModel.ContentType,
                    fileModel.FileName,
                    fileModel.Length,
                    fileModel.PublicUrl,
                    FileDetails.FileAttachmentTypeEnum.Public,
                    fileModel.Description,
                    fileModel.Metadata,
                    fileModel.OwnerId
                )
            };

            // Ensure text message is enabled
            alertSettings.Channels.TextMessage.Send = true;
            alertSettings.Channels.TextMessage.Primary = true;

            // Create a preview for the alert to ensure the settings are valid
            var preview = await _alertingClient.AlertsApi.CreateAlertsPreviewAsync(new PreviewAlertRequest
            {
                Settings = alertSettings
            });
            Assert.True(preview?.Status?.IsSuccess, "Failed to create alert preview");

            // Create the alert request
            SendAlertRequest alert = new SendAlertRequest
            {
                Settings = alertSettings,
                Async = true
            };
            string jsonAlert = JsonConvert.SerializeObject(alert);

            // Send the alert
            var alertRes = await CreateAlertWithAttachmentAsync(jsonAlert);

            // Get the details of the alert using the id, then we can view the message
            int alertId = alertRes.AlertId;
            var alertDetails = await _alertingClient.AlertsApi.GetAlertsByIdAsync(alertId);
            Assert.True(alertDetails?.Status?.IsSuccess, $"Failed to get alert details for alert: {alertId}");

            Console.WriteLine($"Alert message: {alertDetails.Item.Message.Basic.Message}");

            // Get the status of the alert
            var alertStatus = await _alertingClient.AlertsApi.GetAlertsByIdStatusAsync(alertId);
            Assert.True(alertStatus?.Status?.IsSuccess, $"Failed to get the status for alert: {alertId}");

            Console.WriteLine($"Alert status: {alertStatus.Item.Status}");
        }

        protected async Task<CreateDistributionListFromFileResponse> CreateAdHocDistributionListAsync(string fileUrl)
        {
            var adHocRes = await _alertingClient.ContactsApi.CreateFromFileAsync(_tenantId, new CreateDistributionListFromFileRequest
            {
                FileUrl = fileUrl
            });
            Assert.True(adHocRes?.Errors?.Count == 0, $"Failed to create ad-hoc contacts from file: {fileUrl}");

            return adHocRes;
        }

        protected async Task<FileManagement.ApiClient.Model.FileModel> GetUploadedFileAsync(string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            var fileModel = await _fileManagementClient.FilesApi.GetFileV4Async(_tenantId, fileName);

            return fileModel;
        }

        protected async Task<List<FileManagement.ApiClient.Model.FileModel>> GetAllFilesAsync()
        {
            var files = await _fileManagementClient.FilesApi.GetAllFilesV4Async(_tenantId, false);

            return files;
        }

        protected async Task<string> CreateShortenedUrlAsync(string url)
        {
            var res = await _alertingClient.AlertsApi.CreateShortenedUrlAsync(new CreateShortenedUrlRequest
            {
                OriginalUrl = url
            });
            Assert.True(res?.Status?.IsSuccess, $"Failed to create shortened url for: {url}");

            return res.Item.VarShortenedUrl;
        }

        protected async Task<SendAlertResponse> CreateAlertWithAttachmentAsync(string alert)
        {
            var alertRes = await _alertingClient.AlertsApi.CreateAlertWithAttachmentAsync(alert, null, null, true);
            Assert.True(alertRes?.Status?.IsSuccess, "Failed to send alert");

            return alertRes;
        }

        protected static string GetContentType(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".pdf" => "application/pdf",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".txt" => "text/plain",
                ".json" => "application/json",
                ".csv" => "text/csv",
                _ => "application/octet-stream"
            };
        }
    }
}
