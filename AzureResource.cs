using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using Microsoft.Azure.Services.AppAuthentication;
using System.Net.Http.Headers;

namespace Hollan.Function
{

    [JsonObject(MemberSerialization.OptIn)]
    public class AzureResource
    {
        private readonly ILogger _log;
        private readonly HttpClient _client;

        public AzureResource(ILogger log, IHttpClientFactory factory)
        {
            _log = log;
            _client = factory.CreateClient();
        }

        [JsonProperty("value")]
        public DateTime ScheduledDeath { get; set; }

        [JsonProperty]
        public string ResourceId { get; set; }

        public void CreateResource(string ResourceId)
        {
            this.ResourceId = ResourceId;
            _log.LogInformation($"Adding grim watcher for {ResourceId}");
            ScheduledDeath = DateTime.UtcNow.AddDays(1);
            Entity.Current.SignalEntity(
                new EntityId(nameof(SMSConversation), "default"), nameof(SMSConversation.AddResource),
                new
                {
                    ResourceId,
                    ScheduledDeath,
                    Entity.Current.EntityKey
                }
            );

            BookAppointmentWithReaper();
        }

        public async Task DeleteResource()
        {
            // Check to see if scheduled death got extended
            if (DateTime.UtcNow > ScheduledDeath)
            {
                _log.LogInformation($"⚰️ the bell tolls for death of {ResourceId}");
                // Get ARM token
                var azureServiceTokenProvider = new AzureServiceTokenProvider();
                string accessToken = azureServiceTokenProvider.GetAccessTokenAsync("https://management.core.windows.net").Result;
                _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                // DELETE Azure Resource
                var response = await _client.DeleteAsync($"https://management.azure.com{ResourceId}?api-version=2019-08-01");

                _log.LogInformation($"Grim Reaper got response {response.StatusCode} with content {await response.Content.ReadAsStringAsync()}.");

                // Remove this entity from the SMS history
                Entity.Current.SignalEntity(new EntityId(nameof(SMSConversation), "default"), nameof(SMSConversation.RemoveResource), ResourceId);

                _log.LogInformation($"☠️ the deed is done.");

                // Remove this entity
                Delete();
            }
        }

        public void WarnDeleteResource()
        {
            // Check to see if scheduled death got extended
            if (DateTime.UtcNow > ScheduledDeath.AddMinutes(-70))
            {
                Entity.Current.SignalEntity(
                    new EntityId(nameof(SMSConversation), "default"), 
                    nameof(SMSConversation.WarnMessage),
                    ResourceId
                );
            }
        }

        public void ExtendScheduledDeath(int numOfDays)
        {
            ScheduledDeath = ScheduledDeath.AddDays(numOfDays);
            BookAppointmentWithReaper();
        }

        public void BookAppointmentWithReaper()
        {
            // If this would be longer than the 7 day schedule window of durable entities
            if(ScheduledDeath > DateTime.UtcNow.AddDays(6))
            {
                // Wait for 6 days and try this method again
                Entity.Current.SignalEntity(
                    Entity.Current.EntityId,
                    DateTime.UtcNow.AddDays(6),
                    nameof(this.BookAppointmentWithReaper),
                    null);
            }
            // Else can schedule termination signal
            else
            {
                // Setup a warning 1 hour before the reaper visits
                Entity.Current.SignalEntity(
                    Entity.Current.EntityId,
                    ScheduledDeath.AddMinutes(-60),
                    nameof(this.WarnDeleteResource),
                    null);

                // For whom death tolls 🔔
                Entity.Current.SignalEntity(
                    Entity.Current.EntityId,
                    ScheduledDeath.AddMinutes(5),
                    nameof(this.DeleteResource),
                    null
                );
            }
        }
        public void Delete()
        {
            Entity.Current.DeleteState();
        }

        [FunctionName(nameof(AzureResource))]
        public static Task Run([EntityTrigger] IDurableEntityContext ctx,
            ILogger log)
            => ctx.DispatchAsync<AzureResource>(log);
    }
}