// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using System.Threading.Tasks;

namespace Hollan.Function
{
    public static class EventGridTrigger
    {
        [FunctionName("EventGridTrigger")]
        public static async Task Run(
            [EventGridTrigger]EventGridEvent eventGridEvent, 
            [DurableClient] IDurableClient client,
            ILogger log)
        {
            log.LogInformation($"Received notification of create for resource group {eventGridEvent.Subject}");

            string resourceGroupName = StringParsers.ParseResourceGroupName(eventGridEvent.Subject);

            if(resourceGroupName.StartsWith("a-") || resourceGroupName.StartsWith("d-"))
            {
                log.LogInformation($"Ignoring resource group {resourceGroupName} for reserved characters");
                return;
            }

            await client.SignalEntityAsync(
                new EntityId(nameof(AzureResource), eventGridEvent.Id),
                nameof(AzureResource.CreateResource),
                eventGridEvent.Subject
            );
        }
    }
}
