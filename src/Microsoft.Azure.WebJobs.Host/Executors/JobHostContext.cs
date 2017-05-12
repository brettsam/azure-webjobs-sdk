// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Loggers;

namespace Microsoft.Azure.WebJobs.Host.Executors
{
    // JobHostContext are the fields that a JobHost needs to operate at runtime. 
    // This is created from a JobHostConfiguration. 
    internal sealed class JobHostContext : IDisposable
    {
        private readonly IFunctionIndexLookup _functionLookup;
        private readonly IFunctionExecutor _executor;
        private readonly IListener _listener;
        private readonly LogContext _logContext;
        private readonly IAsyncCollector<FunctionInstanceLogEntry> _functionEventCollector; // optional        

        private bool _disposed;

        public JobHostContext(IFunctionIndexLookup functionLookup,
            IFunctionExecutor executor,
            IListener listener,
            LogContext logContext,
            IAsyncCollector<FunctionInstanceLogEntry> functionEventCollector = null)
        {
            _functionLookup = functionLookup;
            _executor = executor;
            _listener = listener;
            _logContext = logContext;
            _functionEventCollector = functionEventCollector;
        }

        public LogContext LogContext
        {
            get
            {
                ThrowIfDisposed();
                return _logContext;
            }
        }

        public IFunctionIndexLookup FunctionLookup
        {
            get
            {
                ThrowIfDisposed();
                return _functionLookup;
            }
        }

        public IFunctionExecutor Executor
        {
            get
            {
                ThrowIfDisposed();
                return _executor;
            }
        }

        public IListener Listener
        {
            get
            {
                ThrowIfDisposed();
                return _listener;
            }
        }

        public IAsyncCollector<FunctionInstanceLogEntry> FunctionEventCollector
        {
            get
            {
                ThrowIfDisposed();
                return _functionEventCollector;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _listener.Dispose();
                _logContext?.Dispose();

                _disposed = true;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(null);
            }
        }
    }
}