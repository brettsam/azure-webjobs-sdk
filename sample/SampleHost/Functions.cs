// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace SampleHost
{
    public static class Functions
    {
        //public static void BlobTrigger(
        //    [BlobTrigger("test")] string blob)
        //{
        //    Console.WriteLine("Processed blob: " + blob);
        //}

        //public static void BlobPoisonBlobHandler(
        //    [QueueTrigger("webjobs-blobtrigger-poison")] JObject blobInfo)
        //{
        //    string container = (string)blobInfo["ContainerName"];
        //    string blobName = (string)blobInfo["BlobName"];

        //    Console.WriteLine($"Poison blob: {container}/{blobName}");
        //}

        public static async Task QueueTrigger(
            [QueueTrigger("test")] string message,
            [Queue("test-out")] IAsyncCollector<object> collector,
            ILogger logger, TraceWriter trace)
        {
            logger.LogInformation("logger");
            trace.Info("tracewriter");

            for (int i = 0; i < 10; i++)
            {
                await collector.AddAsync(new { text = "hello " });
            }
        }
    }
}
