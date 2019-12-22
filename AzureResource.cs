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
            ScheduledDeath = DateTime.UtcNow.AddMinutes(1);
            Entity.Current.SignalEntity(
                new EntityId(nameof(SMSConversation), "default"), nameof(SMSConversation.AddResource),
                new
                {
                    ResourceId,
                    ScheduledDeath
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
                await _client.DeleteAsync($"https://management.azure.com{ResourceId}?api-version=2019-08-01");
                Entity.Current.SignalEntity(new EntityId(nameof(SMSConversation), "default"), nameof(SMSConversation.RemoveResource), ResourceId);
                _log.LogInformation($"☠️ the deed is done.");
                Entity.Current.DeleteState();
            }
        }

        public void UpdateScheduledDeath(DateTime updatedScheduledDeath)
        {
            ScheduledDeath = updatedScheduledDeath;
            BookAppointmentWithReaper();
        }

        private void BookAppointmentWithReaper()
        {
            Entity.Current.SignalEntity(
                Entity.Current.EntityId,
                ScheduledDeath.AddSeconds(30),
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