// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Loggers.Logger
{
    internal class DependencyResult : IDisposable
    {
        private readonly Stopwatch _stopwatch;
        private readonly ILogger _logger;
        private readonly LogLevel _level;
        private bool _disposed = false;

        internal DependencyResult(ILogger logger, LogLevel level)
        {
            _logger = logger;
            _level = level;
            Timestamp = DateTimeOffset.UtcNow;
            _stopwatch = Stopwatch.StartNew();
        }

        public bool? Success { get; set; }
        public TimeSpan Duration { get; private set; }
        public string Type { get; set; }
        public string Target { get; set; }
        public string Data { get; set; }
        public string Name { get; set; }
        public string ResultCode { get; set; }
        public DateTimeOffset Timestamp { get; private set; }

        internal IReadOnlyDictionary<string, object> ToReadOnlyDictionary()
        {
            return new ReadOnlyDictionary<string, object>(new Dictionary<string, object>
            {
                [LoggingKeys.Succeeded] = Success,
                [LoggingKeys.DependencyType] = Type,
                [LoggingKeys.DependencyTarget] = Target,
                [LoggingKeys.Name] = Name,
                [LoggingKeys.Timestamp] = Timestamp,
                [LoggingKeys.Duration] = Duration,
                [LoggingKeys.DependencyResultCode] = ResultCode,
                [LoggingKeys.DependencyData] = Data
            });
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _stopwatch.Stop();
                Duration = _stopwatch.Elapsed;

                _logger?.LogDependency(this, _level);
                _disposed = true;
            }
        }
    }
}
