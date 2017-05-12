// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Loggers
{
    internal class LogContext : IDisposable
    {
        private bool _disposed = false;

        public LogContext(TraceWriter trace, ILoggerFactory loggerFactory)
        {
            TraceWriter = trace;
            LoggerFactory = loggerFactory;
        }

        public ILoggerFactory LoggerFactory { get; private set; }

        public TraceWriter TraceWriter { get; private set; }

        public void LogInformation(string category, string message)
        {
            TraceWriter?.Info(message, source: category);
            LoggerFactory?.CreateLogger(category).LogInformation(message);
        }

        public void LogWarning(string category, string message)
        {
            TraceWriter?.Warning(message, source: category);
            LoggerFactory?.CreateLogger(category).LogWarning(message);
        }

        public void LogDebug(string category, string message)
        {
            TraceWriter?.Verbose(message, source: category);
            LoggerFactory?.CreateLogger(category).LogDebug(message);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                LoggerFactory?.Dispose();

                _disposed = true;
            }
        }
    }
}
