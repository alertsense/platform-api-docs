using Duende.IdentityModel.Client;
using Konexus.FileManagement.ApiClient.Client;
using Konexus.FileManagement.ApiClient.Api;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Konexus.FileManagement.ApiClient
{    
    public class FileManagementClient
    {
        private HttpClient _httpClient;
        private HttpClientHandler _httpClientHandler;
        public string BaseApiUrl { get; private set; }
        public Configuration Configuration { get; protected set; }

        public FilesApi FilesApi { get; private set; }

        public async Task AuthenticateWithClientCredentialsAsync(string authorityUrl, string clientId, string clientSecret, string scopes = "tamarack")
        {

            if (authorityUrl == null)
            {
                throw new ArgumentNullException("authorityUrl", "Authority Url must be provided to authenticate with client_credentials.");
            }
            if (clientId == null)
            {
                throw new ArgumentNullException("clientId", "clientId must be provided to authenticate.");
            }
            if (clientSecret == null)
            {
                throw new ArgumentNullException("clientSecret", "clientSecret must be provided to authenticate.");
            }

            DiscoveryDocumentResponse DiscoveryDocument = await _httpClient.GetDiscoveryDocumentAsync(authorityUrl);
            if (DiscoveryDocument.IsError)
            {
                throw new Exception(DiscoveryDocument.Error);
            }

            var tokenRequest = new TokenRequest
            {
                Address = DiscoveryDocument.TokenEndpoint,
                GrantType = "client_credentials",
                ClientId = clientId,
                ClientSecret = clientSecret,
                Parameters =
                {
                    { "scope", scopes },
                }
            };

            var tokenResponse = await _httpClient.RequestTokenAsync(tokenRequest);

            Configuration.AccessToken = tokenResponse.AccessToken;
            ConfigureApis();
        }

        public FileManagementClient(string baseApiUrl, HttpClient httpClient, HttpClientHandler httpClientHandler = null)
        {
            BaseApiUrl = baseApiUrl;
            _httpClient = httpClient;
            _httpClientHandler = httpClientHandler;

            var defaultHeaders = new Dictionary<string, string>();
            var apiKeyPrefix = new Dictionary<string, string>();
            var apiKey = new Dictionary<string, string>();

            Configuration = new Configuration(defaultHeaders, apiKeyPrefix, apiKey, BaseApiUrl);

            ConfigureApis();
        }

        private void ConfigureApis()
        {
            FilesApi = new FilesApi(_httpClient, Configuration, _httpClientHandler);
        }
    }
}
