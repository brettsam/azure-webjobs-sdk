// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Storage.Blob;

namespace Microsoft.Azure.WebJobs.Host.Blobs.Listeners
{
    internal interface IBlobScanInfoManager
    {
        Task<ContainerScanInfo> LoadScanInfoAsync(string containerName);
        Task UpdateScanInfoAsync(string containerName, ContainerScanInfo scanInfo);
    }
}
