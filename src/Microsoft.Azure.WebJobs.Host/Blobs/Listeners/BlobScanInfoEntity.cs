// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Globalization;
using Microsoft.WindowsAzure.Storage.Table;

namespace Microsoft.Azure.WebJobs.Host.Blobs.Listeners
{
    internal class BlobScanInfoEntity : TableEntity
    {
        public BlobScanInfoEntity()
        {
        }

        public BlobScanInfoEntity(string hostId, string storageAccountName, string containerName)
        {
            PartitionKey = GetPartitionKey(hostId);
            RowKey = GetRowKey(storageAccountName, containerName);
        }
        public DateTimeOffset LatestScanTimestamp { get; set; }

        public static string GetPartitionKey(string hostId)
        {
            return HostPartitionKeyNames.BlobScanInfoPrefix + hostId;
        }

        public static string GetRowKey(string storageAccountName, string containerName)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}-{1}", storageAccountName, containerName);
        }
    }
}
