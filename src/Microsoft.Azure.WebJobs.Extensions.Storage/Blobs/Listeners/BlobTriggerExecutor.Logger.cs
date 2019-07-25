// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Blobs.Listeners
{
    internal partial class BlobTriggerExecutor
    {
        private static class Logger
        {
            // Keep these events in 100-199 range.

            private static readonly Action<ILogger<BlobListener>, string, string, Exception> _blobDoesNotMatchPattern =
               LoggerMessage.Define<string, string>(LogLevel.Debug, new EventId(100, nameof(BlobDoesNotMatchPattern)),
                   "Blob '{blobName}' will be skipped because it does not match the pattern '{pattern}'.");

            private static readonly Action<ILogger<BlobListener>, string, Exception> _blobHasNoETag =
                LoggerMessage.Define<string>(LogLevel.Debug, new EventId(101, nameof(BlobHasNoETag)),
                    "Blob '{blobName}' will be skipped because its ETag cannot be found. The blob may have been deleted.");

            private static readonly Action<ILogger<BlobListener>, string, string, Exception> _blobAlreadyProcessed =
                LoggerMessage.Define<string, string>(LogLevel.Debug, new EventId(102, nameof(BlobAlreadyProcessed)),
                    "Blob '{blobName}' will be skipped because this blob with ETag '{eTag}' has already been processed.");

            private static readonly Action<ILogger<BlobListener>, string, string, string, Exception> _blobMessageEnqueued =
                LoggerMessage.Define<string, string, string>(LogLevel.Debug, new EventId(103, nameof(BlobMessageEnqueued)),
                    "Blob '{blobName}' is ready for processing. A message with id '{messageId}' has been added to queue '{queueName}'. This message will be dequeued and processed by the BlobTrigger.");

            public static void BlobDoesNotMatchPattern(ILogger<BlobListener> logger, string blobName, string pattern) =>
                _blobDoesNotMatchPattern(logger, blobName, pattern, null);

            public static void BlobHasNoETag(ILogger<BlobListener> logger, string blobName) =>
                _blobHasNoETag(logger, blobName, null);

            public static void BlobAlreadyProcessed(ILogger<BlobListener> logger, string blobName, string eTag) =>
                _blobAlreadyProcessed(logger, blobName, eTag, null);

            public static void BlobMessageEnqueued(ILogger<BlobListener> logger, string blobName, string queueMessageId, string queueName) =>
                _blobMessageEnqueued(logger, blobName, queueMessageId, queueName, null);
        }
    }
}
