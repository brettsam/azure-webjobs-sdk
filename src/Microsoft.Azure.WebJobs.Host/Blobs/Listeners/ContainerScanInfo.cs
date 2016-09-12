// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;

namespace Microsoft.Azure.WebJobs.Host.Blobs.Listeners
{
    internal class ContainerScanInfo
    {
        public ContainerScanInfo()
        {
            Registrations = new List<ITriggerExecutor<IStorageBlob>>();
        }

        [JsonIgnore]
        public ICollection<ITriggerExecutor<IStorageBlob>> Registrations { get; set; }

        public DateTime LastSweepCycleLatestModified { get; set; }

        public DateTime CurrentSweepCycleLatestModified { get; set; }

        [JsonIgnore]
        public BlobContinuationToken ContinuationToken { get; set; }
    }
}
