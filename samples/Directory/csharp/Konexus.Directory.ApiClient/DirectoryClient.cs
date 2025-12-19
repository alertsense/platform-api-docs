using Duende.IdentityModel.Client;
using Konexus.Directory.ApiClient.Api;
using Konexus.Directory.ApiClient.Client;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Konexus.Directory.ApiClient
{    
    public class DirectoryClient
    {
        private HttpClient _httpClient;
        private HttpClientHandler _httpClientHandler;
        public string BaseApiUrl { get; private set; }
        public Configuration Configuration { get; protected set; }

        public UsersApi UsersApi { get; private set; }
        public MetaApi MetaApi { get; private set; }
        public GroupsApi GroupsApi { get; private set; }
        public TestApi TestApi { get; private set; }

        public async Task AuthenticateWithClientCredentialsAsync(string authorityUrl, string clientId, string clientSecret, string scopes = "tamarack content")
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

        public DirectoryClient(string baseApiUrl, HttpClient httpClient, HttpClientHandler httpClientHandler = null)
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
            UsersApi = new UsersApi(_httpClient, Configuration, _httpClientHandler);
            MetaApi = new MetaApi(_httpClient, Configuration, _httpClientHandler);
            GroupsApi = new GroupsApi(_httpClient, Configuration, _httpClientHandler);
            TestApi = new TestApi(_httpClient, Configuration, _httpClientHandler);
        }
    }
}
