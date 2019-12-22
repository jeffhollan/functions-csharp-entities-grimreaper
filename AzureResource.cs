using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Net.Http;

namespace Hollan.Function
{

    [JsonObject(MemberSerialization.OptIn)]
    public class AzureResource
    {
        private readonly ILogger _log;
        private readonly HttpClient _client;

        public AzureResource(ILogger log, HttpClient client)
        {
            _log = log;
            _client = client;
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
                    EntityKey = Entity.Current.EntityKey
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
                var response = await _client.DeleteAsync($"https://management.azure.com{ResourceId}?api-version=2019-08-01");
                _log.LogInformation($"Grim Reaper got response {response.StatusCode} with content {await response.Content.ReadAsStringAsync()}.");
                Entity.Current.SignalEntity(new EntityId(nameof(SMSConversation), "default"), nameof(SMSConversation.RemoveResource), ResourceId);
                _log.LogInformation($"☠️ the deed is done.");
                Entity.Current.DeleteState();
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

        private void BookAppointmentWithReaper()
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

        [FunctionName(nameof(AzureResource))]
        public static Task Run([EntityTrigger] IDurableEntityContext ctx,
            ILogger log)
            => ctx.DispatchAsync<AzureResource>(log);
    }
}