// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Blobs.Listeners;
using Microsoft.Azure.WebJobs.Host.FunctionalTests.TestDoubles;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.FunctionalTests.Blobs.Listeners
{
    public class StorageBlobScanInfoManagerTests
    {
        [Fact]
        public async Task LoadLatestScan_NoTable_ReturnsNull()
        {
            string hostId = Guid.NewGuid().ToString();
            string storageAccountName = Guid.NewGuid().ToString();
            string containerName = Guid.NewGuid().ToString();

            var account = new FakeStorageAccount();
            var client = account.CreateTableClient();

            // by default there is no table in this client
            var manager = new StorageBlobScanInfoManager(hostId, client);

            var result = await manager.LoadLatestScanAsync(storageAccountName, containerName);

            Assert.Null(result);
        }

        [Fact]
        public async Task LoadLatestScan_NoRow_ReturnsNull()
        {
            string hostId = Guid.NewGuid().ToString();
            string storageAccountName = Guid.NewGuid().ToString();
            string containerName = Guid.NewGuid().ToString();

            var account = new FakeStorageAccount();
            var client = account.CreateTableClient();
            var table = client.GetTableReference(HostTableNames.Hosts);
            table.CreateIfNotExists();

            var manager = new StorageBlobScanInfoManager(hostId, client);

            var result = await manager.LoadLatestScanAsync(storageAccountName, containerName);

            Assert.Null(result);
        }

        [Fact]
        public async Task LoadLatestScan_Returns_Timestamp()
        {
            string hostId = Guid.NewGuid().ToString();
            string storageAccountName = Guid.NewGuid().ToString();
            string containerName = Guid.NewGuid().ToString();

            var account = new FakeStorageAccount();
            var client = account.CreateTableClient();
            var table = client.GetTableReference(HostTableNames.Hosts);
            table.CreateIfNotExists();
            DateTime now = DateTime.UtcNow;
            table.Insert(new BlobScanInfoEntity(hostId, storageAccountName, containerName) { LatestScanTimestamp = now });

            var manager = new StorageBlobScanInfoManager(hostId, client);

            var result = await manager.LoadLatestScanAsync(storageAccountName, containerName);

            Assert.Equal(now, result);
        }

        [Fact]
        public async Task UpdateLatestScan_Inserts()
        {
            string hostId = Guid.NewGuid().ToString();
            string storageAccountName = Guid.NewGuid().ToString();
            string containerName = Guid.NewGuid().ToString();
            string partitionKey = BlobScanInfoEntity.GetPartitionKey(hostId);
            string rowKey = BlobScanInfoEntity.GetRowKey(storageAccountName, containerName);

            var account = new FakeStorageAccount();
            var client = account.CreateTableClient();
            var table = client.GetTableReference(HostTableNames.Hosts);
            table.CreateIfNotExists();
            DateTime now = DateTime.UtcNow;

            var manager = new StorageBlobScanInfoManager(hostId, client);

            await manager.UpdateLatestScanAsync(storageAccountName, containerName, now);
            var entity = table.Retrieve<BlobScanInfoEntity>(partitionKey, rowKey);

            Assert.Equal(now, entity.LatestScanTimestamp);
        }

        [Fact]
        public async Task UpdateLatestScan_Updates()
        {
            string hostId = Guid.NewGuid().ToString();
            string storageAccountName = Guid.NewGuid().ToString();
            string containerName = Guid.NewGuid().ToString();
            string partitionKey = BlobScanInfoEntity.GetPartitionKey(hostId);
            string rowKey = BlobScanInfoEntity.GetRowKey(storageAccountName, containerName);

            var account = new FakeStorageAccount();
            var client = account.CreateTableClient();
            var table = client.GetTableReference(HostTableNames.Hosts);
            table.CreateIfNotExists();

            DateTime now = DateTime.UtcNow;
            DateTime past = now.AddMinutes(-1);

            table.Insert(new BlobScanInfoEntity(hostId, storageAccountName, containerName) { LatestScanTimestamp = past });
            var manager = new StorageBlobScanInfoManager(hostId, client);

            await manager.UpdateLatestScanAsync(storageAccountName, containerName, now);

            var entity = table.Retrieve<BlobScanInfoEntity>(partitionKey, rowKey);

            Assert.Equal(now, entity.LatestScanTimestamp);
        }
    }
}