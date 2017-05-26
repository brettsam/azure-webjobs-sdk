// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Loggers.Logger;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.Storage.Queue;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;

namespace Microsoft.Azure.WebJobs.Host.Queues
{
    internal static class StorageQueueExtensions
    {
        public static async Task AddMessageAsync(this IStorageQueue queue, IStorageQueueMessage message, ILogger logger, Extensions.Logging.LogLevel level, CancellationToken token)
        {
            await TrackQueueDependencyAsync(queue, logger, level, nameof(AddMessageAsync), async () =>
            {
                await queue.AddMessageAsync(message, token);
                return "201";
            });
        }

        public static async Task CreateIfNotExistsAsync(this IStorageQueue queue, ILogger logger, Extensions.Logging.LogLevel level, CancellationToken cancellationToken)
        {
            await TrackQueueDependencyAsync(queue, logger, level, nameof(CreateIfNotExistsAsync), async () =>
            {
                await queue.CreateIfNotExistsAsync(cancellationToken);
                return "201";
            });
        }

        private static async Task TrackQueueDependencyAsync(IStorageQueue queue, ILogger logger, Extensions.Logging.LogLevel level, string operationName, Func<Task<string>> queueOperation)
        {
            // If Logger is null, the DependencyResult is created, but nothing is logged.
            using (DependencyResult result = logger.TrackDependency(level))
            {
                result.Type = "Azure queue";
                result.Target = queue.Name;
                result.Name = operationName;
                result.Data = queue.SdkObject.Uri.ToString();

                try
                {
                    // Allow the operation to set its own result
                    result.ResultCode = await queueOperation();
                    result.Success = true;
                }
                catch (Exception ex)
                {
                    StorageException storageEx = ex as StorageException;
                    if (storageEx != null)
                    {
                        result.ResultCode = $"{storageEx.RequestInformation?.HttpStatusCode} {storageEx.RequestInformation?.HttpStatusMessage}";
                    }

                    result.Success = false;
                    throw;
                }
            }
        }

        public static async Task AddMessageAndCreateIfNotExistsAsync(this IStorageQueue queue, IStorageQueueMessage message, CancellationToken cancellationToken)
        {
            await queue.AddMessageAndCreateIfNotExistsAsync(message, null, Extensions.Logging.LogLevel.None, cancellationToken);
        }

        public static async Task AddMessageAndCreateIfNotExistsAsync(this IStorageQueue queue,
            IStorageQueueMessage message, ILogger logger, Extensions.Logging.LogLevel level, CancellationToken cancellationToken)
        {
            if (queue == null)
            {
                throw new ArgumentNullException("queue");
            }

            bool isQueueNotFoundException = false;

            try
            {
                await queue.AddMessageAsync(message, logger, level, cancellationToken);
                return;
            }
            catch (StorageException exception)
            {
                if (!exception.IsNotFoundQueueNotFound())
                {
                    throw;
                }

                isQueueNotFoundException = true;
            }

            Debug.Assert(isQueueNotFoundException);
            await queue.CreateIfNotExistsAsync(logger, level, cancellationToken);
            await queue.AddMessageAsync(message, logger, level, cancellationToken);
        }
    }
}
