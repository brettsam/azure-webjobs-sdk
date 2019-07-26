// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Blobs.Listeners
{
    internal sealed partial class PollLogsStrategy
    {
        private class Logger
        {
            // Keep these events in 300-399 range.

            private static readonly Action<ILogger<BlobListener>, string, Exception> _processingBlobFromLogScan =
               LoggerMessage.Define<string>(LogLevel.Debug, new EventId(300, nameof(ProcessingBlobFromLogScan)),
                   "Blob log scan is processing blob '{blobName}'.");

            public static void ProcessingBlobFromLogScan(ILogger<BlobListener> logger, string blobName) =>
                _processingBlobFromLogScan(logger, blobName, null);
        }
    }
}
