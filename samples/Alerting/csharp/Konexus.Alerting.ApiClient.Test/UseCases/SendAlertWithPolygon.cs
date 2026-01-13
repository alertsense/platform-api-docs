using dotenv.net;
using Konexus.Alerting.ApiClient.Model;
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
    public class SendAlertWithPolygon : IAsyncLifetime
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
        #endregion

        protected const int _tenantId = 1211;

        protected virtual List<double> Bbox { get; set; }
        protected virtual List<List<List<double>>> Coordinates { get; set; }

        public SendAlertWithPolygon(ITestOutputHelper output)
        {
            #region Logging Setup

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.TestOutput(output, Serilog.Events.LogEventLevel.Verbose)
                .CreateLogger()
                .ForContext<SendAlertWithPolygon>();

            #endregion Logging Setup

            #region Credentials Configuration

            DotEnv.Load();
            _clientId = Environment.GetEnvironmentVariable("CLIENT_ID");
            _clientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET");

            #endregion Credentials Configuration

            // The bounding box for the polygon
            Bbox = new List<double>
            {
                -116.31916974347632,
                43.646603675700405,
                -116.31670211118283,
                43.64840478081464
            };
            // The coordinates of the polygon
            Coordinates = new List<List<List<double>>>
            {
                new List<List<double>> {
                    new List<double> { -116.31916974347632, 43.646603675700405 },
                    new List<double> { -116.31916974347632, 43.64840478081464 },
                    new List<double> { -116.31670211118283, 43.64840478081464 },
                    new List<double> { -116.31670211118283, 43.646603675700405 },
                    new List<double> { -116.31916974347632, 43.646603675700405 }
                }
            };
        }

        public async Task InitializeAsync()
        {
            // reuse the same client for the entire test run to prevent port exhaustion
            _httpClient = new HttpClient();
            _httpClientHandler = new HttpClientHandler();

            _alertingClient = await CreateAuthenticatedAlertingClientAsync(_clientId, _clientSecret);
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

        [Fact]
        public async Task ExampleUseCases()
        {
            await SendAlertFlowAsync(FeatureLayerSettings.FeaturesSourceEnum.PublicUsers, Bbox, Coordinates);
        }

        protected async Task SendAlertFlowAsync(FeatureLayerSettings.FeaturesSourceEnum userType, List<double> bbox, List<List<List<double>>> coordinates)
        {
            // Get alert defaults, will be used to build the alert
            var alertDefaultsRes = await _alertingClient.AlertsApi.GetAlertsDefaultAsync();
            Assert.True(alertDefaultsRes?.Status?.IsSuccess, "Failed to get alert defaults");
            AlertDefaults alertSettings = alertDefaultsRes.Item;

            // Get recipients from a location
            // First, create the geojson
            // NOTE: resend functionality is not currently supported for alerts sent via the api integration that include geojson map selections
            FeatureCollection geojson = new FeatureCollection
            {
                Type = FeatureCollection.TypeEnum.FeatureCollection,
                Features = new List<Feature>
                {
                    new Feature
                    {
                        Type = Feature.TypeEnum.Feature,
                        Geometry = new IGeometryObject
                        {
                            Type = IGeometryObject.TypeEnum.Polygon,
                            Bbox = bbox,
                            Coordinates = coordinates
                        },
                        Properties = new Dictionary<string, object> { }
                    }
                }
            };
            // Use the polygon to retrieve the number of recipients in the area
            var countsRes = await _alertingClient.AlertsApi.PostFeaturesCountAsync(null, null, userType.ToString(), new FeatureCollectionRequest
            {
                CountsOnly = true,
                GeoJson = geojson
            });
            Assert.True(countsRes != null);

            // Add the count and the recipient type to the alert settings
            alertSettings.Recipients.FeatureLayers = new List<FeatureLayerSettings>
            {
                new FeatureLayerSettings
                {
                    FeaturesSource = userType,
                    Count = countsRes.Count
                }
            };

            // Add the geojson to the alert settings
            alertSettings.Recipients.Geofences = geojson;

            // Set the alert subject and message
            alertSettings.Message.Basic.Subject = "Alert with polygon";
            alertSettings.Message.Basic.Message = "This is a test alert with a location";

            // Set the desired delivery methods
            alertSettings.Channels.Email.Send = true;

            // Create a preview for the alert to ensure the settings are valid
            var preview = await _alertingClient.AlertsApi.CreateAlertsPreviewAsync(new PreviewAlertRequest
            {
                Settings = alertSettings
            });
            Assert.True(preview?.Status?.IsSuccess, "Failed to create alert preview");

            // Create the alert request
            SendAlertRequest alertRequest = new SendAlertRequest
            {
                Settings = alertSettings,
                Async = true
            };

            // Send the alert
            var alertRes = await CreateAlertAsync(alertRequest);

            // View the details of the alert using the id
            int alertId = alertRes.AlertId;
            var alertDetails = await _alertingClient.AlertsApi.GetAlertsByIdAsync(alertId);
            Assert.True(alertDetails?.Status?.IsSuccess, $"Failed to get alert details for alert: {alertId}");

            // Get the status of the alert
            var alertStatus = await _alertingClient.AlertsApi.GetAlertsByIdStatusAsync(alertId);
            Assert.True(alertStatus?.Status?.IsSuccess, $"Failed to get the status for alert: {alertId}");

            Console.WriteLine($"Alert status: {alertStatus.Item.Status}");
        }

        protected async Task<SendAlertResponse> CreateAlertAsync(SendAlertRequest alertRequest)
        {
            var alertRes = await _alertingClient.AlertsApi.CreateAlertsAsync(alertRequest);
            Assert.True(alertRes?.Status?.IsSuccess, "Failed to send alert");

            return alertRes;
        }
    }
}
