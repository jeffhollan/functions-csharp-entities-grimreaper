using System.Net.Http;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.Services.AppAuthentication;
using System.Net.Http.Headers;

[assembly: FunctionsStartup(typeof(Hollan.Function.Startup))]

namespace Hollan.Function
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            builder.Services.AddSingleton(sp =>
            {
                var client = new HttpClient();
                var azureServiceTokenProvider = new AzureServiceTokenProvider();
                string accessToken = azureServiceTokenProvider.GetAccessTokenAsync("https://management.core.windows.net").Result;
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                return client;
            });
        }
    }
}