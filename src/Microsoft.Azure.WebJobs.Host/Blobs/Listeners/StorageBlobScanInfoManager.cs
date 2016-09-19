// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.Storage.Table;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace Microsoft.Azure.WebJobs.Host.Blobs.Listeners
{
    internal class StorageBlobScanInfoManager : IBlobScanInfoManager
    {
        private readonly string _hostId;
        private IStorageTable _blobScanInfoTable;

        public StorageBlobScanInfoManager(string hostId, IStorageTableClient hostTableClient)
        {
            if (string.IsNullOrEmpty(hostId))
            {
                throw new ArgumentNullException("hostId");
            }
            if (hostTableClient == null)
            {
                throw new ArgumentNullException("hostTableClient");
            }

            _hostId = hostId;
            _blobScanInfoTable = hostTableClient.GetTableReference(HostTableNames.Hosts);
        }

        public async Task<DateTime?> LoadLatestScanAsync(string storageAccountName, string containerName)
        {
            DateTime? value = null;
            string partitionKey = BlobScanInfoEntity.GetPartitionKey(_hostId);
            string rowKey = BlobScanInfoEntity.GetRowKey(storageAccountName, containerName);
            IStorageTableOperation retrieveOperation = _blobScanInfoTable.CreateRetrieveOperation<BlobScanInfoEntity>(partitionKey, rowKey);
            try
            {
                TableResult result = await _blobScanInfoTable.ExecuteAsync(retrieveOperation, CancellationToken.None);
                BlobScanInfoEntity blobScanInfo = (BlobScanInfoEntity)result.Result;
                if (blobScanInfo != null)
                {
                    value = blobScanInfo.LatestScanTimestamp.DateTime;
                }
            }
            catch
            {
                // best effort
            }

            return value;
        }

        public async Task UpdateLatestScanAsync(string storageAccountName, string containerName, DateTime latestScan)
        {
            BlobScanInfoEntity entity = new BlobScanInfoEntity(_hostId, storageAccountName, containerName);
            entity.LatestScanTimestamp = latestScan;

            IStorageTableOperation insertOrReplaceOperation = _blobScanInfoTable.CreateInsertOrReplaceOperation(entity);

            try
            {
                await _blobScanInfoTable.ExecuteAsync(insertOrReplaceOperation, CancellationToken.None);
                return;
            }
            catch (Exception ex)
            {
                StorageException storageEx = ex as StorageException;
                if (storageEx == null || !storageEx.IsNotFoundTableNotFound())
                {
                    // best effort
                    return;
                }
            }

            // we can only get here if Table was not found.
            await _blobScanInfoTable.CreateIfNotExistsAsync(CancellationToken.None);

            try
            {
                await _blobScanInfoTable.ExecuteAsync(insertOrReplaceOperation, CancellationToken.None);
            }
            catch
            {
                // best effort
            }
        }
    }
}
