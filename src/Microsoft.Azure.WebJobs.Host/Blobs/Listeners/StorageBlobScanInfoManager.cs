// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.Storage.Blob;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Host.Blobs.Listeners
{
    internal class StorageBlobScanInfoManager : IBlobScanInfoManager
    {
        private const string HostContainerName = "azure-webjobs-hosts";
        private readonly JsonSerializer _serializer;
        private readonly string _hostId;
        private readonly IStorageAccount _storageAccount;
        private IStorageBlobDirectory _blobScanInfoDirectory;

        public StorageBlobScanInfoManager(string hostId, IStorageAccount storageAccount)
        {
            if (string.IsNullOrEmpty(hostId))
            {
                throw new ArgumentNullException("hostId");
            }
            if (storageAccount == null)
            {
                throw new ArgumentNullException("storageAccount");
            }

            _hostId = hostId;
            _storageAccount = storageAccount;
            JsonSerializerSettings settings = new JsonSerializerSettings
            {
                DateFormatHandling = DateFormatHandling.IsoDateFormat
            };
            _serializer = JsonSerializer.Create(settings);
        }

        private IStorageBlobDirectory BlobScanInfoDirectory
        {
            get
            {
                // We have to delay create the blob directory since we require the JobHost ID, and that will only
                // be available AFTER the host as been started
                if (_blobScanInfoDirectory == null)
                {
                    IStorageBlobClient blobClient = _storageAccount.CreateBlobClient();
                    string timerStatusDirectoryPath = string.Format(CultureInfo.InvariantCulture, "blobScanInfo/{0}", _hostId);
                    _blobScanInfoDirectory = blobClient.GetContainerReference(HostContainerName).GetDirectoryReference(timerStatusDirectoryPath);
                }
                return _blobScanInfoDirectory;
            }
        }

        public async Task<ContainerScanInfo> LoadScanInfoAsync(string containerName)
        {
            IStorageBlockBlob scanInfoBlob = GetScanInfoBlobReference(containerName);

            try
            {
                string scanInfoLine = await scanInfoBlob.DownloadTextAsync(CancellationToken.None);
                ContainerScanInfo scanInfo;
                using (StringReader stringReader = new StringReader(scanInfoLine))
                {
                    scanInfo = (ContainerScanInfo)_serializer.Deserialize(stringReader, typeof(ContainerScanInfo));
                }
                return scanInfo;
            }
            catch (StorageException exception)
            {
                if (exception.RequestInformation != null &&
                    exception.RequestInformation.HttpStatusCode == 404)
                {
                    // we haven't saved any scanInfo yet
                    return null;
                }
                throw;
            }
        }

        public async Task UpdateScanInfoAsync(string containerName, ContainerScanInfo scanInfo)
        {
            string scanInfoLine;
            using (StringWriter stringWriter = new StringWriter())
            {
                _serializer.Serialize(stringWriter, scanInfo);
                scanInfoLine = stringWriter.ToString();
            }

            try
            {
                IStorageBlockBlob scanInfoBlob = GetScanInfoBlobReference(containerName);
                await scanInfoBlob.UploadTextAsync(scanInfoLine);
            }
            catch
            {
                // best effort               
            }
        }

        private IStorageBlockBlob GetScanInfoBlobReference(string containerName)
        {
            // Path to the status blob is:
            // blobScanInfo/{hostId}/{containerName}/scanInfo
            string blobName = string.Format(CultureInfo.InvariantCulture, "{0}/scanInfo", containerName);
            return BlobScanInfoDirectory.GetBlockBlobReference(blobName);
        }
    }
}
