using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.WindowsAzure;
using Microsoft.Azure;

namespace FDataCollector.WebJob
{
    // WebJobs Service Bus
    public class Functions
    {
        public static void ProcessQueueMessage([QueueTrigger("getdata")] string message, TextWriter log)
        // public static void ProcessQueueMessage([ServiceBusTrigger("getdata")] string message, TextWriter log)
        {
            log.WriteLine(message);
            var blobConnectionString = CloudConfigurationManager.GetSetting("AzureWebJobsStorage");            
            var data = message.Split(',');

            System.Diagnostics.Debug.WriteLine(string.Format("Processing {0}", data[1]));           

            FDataCollectorHelper.processGetDataMessage(blobConnectionString, data[0], data[1], data[2]);
        }
    }


}
