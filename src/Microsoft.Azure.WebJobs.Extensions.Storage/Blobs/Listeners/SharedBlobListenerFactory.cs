// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Azure.WebJobs.Extensions.Storage;
using Microsoft.Azure.WebJobs.Host.Timers;

namespace Microsoft.Azure.WebJobs.Host.Blobs.Listeners
{
    internal class SharedBlobListenerFactory : IFactory<SharedBlobListener>
    {
        private readonly StorageAccount _account;
        private readonly IWebJobsExceptionHandler _exceptionHandler;
        private readonly IContextSetter<IBlobWrittenWatcher> _blobWrittenWatcherSetter;
        private readonly ResponseListener _responseListener;
        private readonly string _hostId;

        public SharedBlobListenerFactory(string hostId, StorageAccount account,
            IWebJobsExceptionHandler exceptionHandler,
            IContextSetter<IBlobWrittenWatcher> blobWrittenWatcherSetter,
            ResponseListener responseListener)
        {
            if (account == null)
            {
                throw new ArgumentNullException("account");
            }

            if (exceptionHandler == null)
            {
                throw new ArgumentNullException("exceptionHandler");
            }

            if (blobWrittenWatcherSetter == null)
            {
                throw new ArgumentNullException("blobWrittenWatcherSetter");
            }

            _hostId = hostId;
            _account = account;
            _exceptionHandler = exceptionHandler;
            _blobWrittenWatcherSetter = blobWrittenWatcherSetter;
            _responseListener = responseListener ?? throw new ArgumentNullException(nameof(responseListener));
        }

        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        public SharedBlobListener Create()
        {
            SharedBlobListener listener = new SharedBlobListener(_hostId, _account, _exceptionHandler, _responseListener);
            _blobWrittenWatcherSetter.SetValue(listener.BlobWritterWatcher);
            return listener;
        }
    }
}
