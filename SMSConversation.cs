using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;
using System.Collections.Generic;

namespace Hollan.Function
{

    [JsonObject(MemberSerialization.OptIn)]
    public class SMSConversation
    {
        private readonly ILogger log;
        private readonly IAsyncCollector<CreateMessageOptions> twilioBinding;
        private readonly string toPhoneNumber = Environment.GetEnvironmentVariable("TwilioTo");
        [JsonProperty]
        public List<string> resourceMap = new List<string>();
        
        [JsonProperty]
        public Dictionary<int, string> entityMap = new Dictionary<int, string>();
        public SMSConversation(ILogger log, IAsyncCollector<CreateMessageOptions> twilioBinding)
        {
            this.log = log;
            this.twilioBinding = twilioBinding;
        }

        public async Task SendMessage(string message)
        {
            await twilioBinding.AddAsync(
                new CreateMessageOptions(new PhoneNumber(toPhoneNumber))
                {
                    Body = message
                }
            );
            await twilioBinding.FlushAsync();
        }

        public async Task ReceiveExtendMessage((string, string, string) commandTuple)
        {
            string entityKey = entityMap[int.Parse(commandTuple.Item2)];
            string resourceId = resourceMap[int.Parse(commandTuple.Item2)];
            string resourceGroupName = StringParsers.ParseResourceGroupName(resourceId);

            Entity.Current.SignalEntity(
                new EntityId(nameof(AzureResource), entityKey),
                nameof(AzureResource.ExtendScheduledDeath),
                int.Parse(commandTuple.Item3)
            );

            await SendMessage($"Extending death of {resourceGroupName} by {commandTuple.Item3} days 😇");
        }

        public async Task AddResource(dynamic input)
        {
            string resourceId = input["ResourceId"];
            string entityKey = input["EntityKey"];
            DateTime scheduledDeath = input["ScheduledDeath"];
            DateTimeOffset localTimeDeath = scheduledDeath.AddHours(-7);
            string resourceGroupName = StringParsers.ParseResourceGroupName(resourceId);

            if (resourceMap.Contains(resourceId))
                throw new InvalidOperationException("Cannot add a resource that already exists");

            // Find if an empty spot in our array
            int indexOfNull = resourceMap.IndexOf(null);
            // If no empty spots, add to end of the list
            if (indexOfNull < 0)
            {
                resourceMap.Add(resourceId);
            }
            // If an empty spot, add to the first empty spot
            else
            {
                resourceMap[indexOfNull] = resourceId;
            }

            int currentIndex = resourceMap.IndexOf(resourceId);

            if(entityMap.ContainsKey(currentIndex))
                entityMap.Remove(currentIndex);

            entityMap.Add(currentIndex, entityKey);

            await SendMessage(
                $"Created resource {resourceGroupName}. Set to expire at {localTimeDeath.DateTime.ToShortDateString()} {localTimeDeath.DateTime.ToShortTimeString()}. To extend, reply 'Extend {currentIndex} <num-of-days>'"
            );
        }

        public async Task WarnMessage(string resourceId)
        {
            int index = resourceMap.IndexOf(resourceId);
            string resourceGroupName = StringParsers.ParseResourceGroupName(resourceId);
            await SendMessage(
                $"Death approaches for {resourceGroupName}.  To extend, reply 'Extend {index} <num-of-days>'"
            );
        }

        public async Task RemoveResource(string resourceId)
        {
            int index = resourceMap.IndexOf(resourceId);
            string resourceGroupName = StringParsers.ParseResourceGroupName(resourceId);
            if(index >= 0)
                resourceMap[index] = null;
            await SendMessage(
                $"🔔 The bell tolls for {resourceGroupName} ⚰️"
            );
        }

        [FunctionName(nameof(SMSConversation))]
        public static Task Run([EntityTrigger] IDurableEntityContext ctx,
            [TwilioSms(AccountSidSetting = "TwilioSid", AuthTokenSetting = "TwilioToken", From = "%TwilioFrom%")] IAsyncCollector<CreateMessageOptions> twilioBinding,
            ILogger log)
            => ctx.DispatchAsync<SMSConversation>(log, twilioBinding);
    }
}