using Duende.IdentityModel.Client;
using Microsoft.Extensions.Configuration;

namespace OPMS.HttpApi.ExternalApiClients.Jund
{
    public class JundAuthHeaderHandler : HttpClientHandler 
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public JundAuthHeaderHandler(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _configuration = configuration;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var oAuthClient = _httpClientFactory.CreateClient();
            var configurationSection = _configuration.GetSection("ExternalApis:Jund");

            var discoveryDoc = await oAuthClient.GetDiscoveryDocumentAsync(configurationSection["ClientCredential:Address"]);


            var clientCredential = new ClientCredentialsTokenRequest
            {
                GrantType = "client_credentials",
                Method = HttpMethod.Post,
                ClientCredentialStyle = ClientCredentialStyle.AuthorizationHeader,
                Address = discoveryDoc.TokenEndpoint,
                ClientId = configurationSection["ClientCredential:ClientId"],
                ClientSecret = configurationSection["ClientCredential:ClientSecret"],
                Scope = configurationSection["ClientCredential:Scope"],
            };

            // This code is to preview the request sent 
            if (request.Content != null)
            {
                var requestBody = await request.Content.ReadAsStringAsync();
               
            }


            var tokenResponse = await oAuthClient.RequestClientCredentialsTokenAsync(clientCredential);
            //potentially refresh token here if it has expired etc.
            request.SetBearerToken(tokenResponse.AccessToken);

            //request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResponse.AccessToken);
            var resposne = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            // This code is to preview the response received 
            if (resposne.Content != null)
            {
                var responseBody = await resposne.Content.ReadAsStringAsync();
            }
            return resposne;
        }
    }

}
