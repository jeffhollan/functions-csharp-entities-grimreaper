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
            builder.Services.AddHttpClient();
        }
    }
}