// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace SampleHost
{
    public static class Functions
    {
        public static void BlobTrigger(
            [BlobTrigger("test")] string blob)
        {
            Console.WriteLine("Processed blob: " + blob);
        }

        public static void BlobPoisonBlobHandler(
            [QueueTrigger("webjobs-blobtrigger-poison")] JObject blobInfo)
        {
            string container = (string)blobInfo["ContainerName"];
            string blobName = (string)blobInfo["BlobName"];

            Console.WriteLine($"Poison blob: {container}/{blobName}");
        }

        public static async Task QueueTrigger(
            [QueueTrigger("test")] string message,
            [Queue("test-out")] IAsyncCollector<string> collector,
            [Queue("test-out2")] IAsyncCollector<string> collector2,
            ILogger logger)
        {
            logger.LogInformation("Processed message: " + message);

            char[] characters = message.ToCharArray();
            for (int i = 0; i < characters.Length; i++)
            {
                await collector.AddAsync(characters[i].ToString());
                await collector2.AddAsync(characters[i].ToString());
            }
        }
    }
}
