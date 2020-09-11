using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Web;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;

namespace Hollan.Function
{
    public static class TwilioTrigger
    {
        [FunctionName("TwilioTrigger")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            [DurableClient] IDurableClient client,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");
            string requestString = await new StreamReader(req.Body).ReadToEndAsync();
            string decodedString = HttpUtility.UrlDecode(requestString);
            string command = StringParsers.ParseTextMessage(decodedString);
            (string, string, string) commandTuple = StringParsers.ParseCommand(command);

            switch(commandTuple.Item1)
            {
                case "extend":
                    await client.SignalEntityAsync(
                        new EntityId(nameof(SMSConversation), "default"),
                        nameof(SMSConversation.ReceiveExtendMessage),
                        commandTuple
                    );
                    break;
                case "untrack":
                    await client.SignalEntityAsync(
                        new EntityId(nameof(SMSConversation), "default"),
                        nameof(SMSConversation.UntrackResource),
                        commandTuple
                    );
                    break;
                default:
                    break;
            }
            return new AcceptedResult();
        }
    }
}
