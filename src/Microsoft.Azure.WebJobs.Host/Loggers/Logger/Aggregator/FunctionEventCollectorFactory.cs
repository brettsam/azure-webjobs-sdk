// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Logging
{
    internal class FunctionEventCollectorFactory : IFunctionEventCollectorFactory
    {
        private readonly FunctionResultAggregatorOptions _aggregatorOptions;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IAsyncCollector<FunctionInstanceLogEntry> _registeredCollector;
        private readonly Lazy<IAsyncCollector<FunctionInstanceLogEntry>> _functionEventCollectorLazy;

        // This allows us to take the registered collector (i.e. the FastLogger from Script) and combine it with the Aggregator into a
        // single collector. Any consumers of this collector (FunctionExecutor) should use this factory to ensure the composition occurs.
        public FunctionEventCollectorFactory(IOptions<FunctionResultAggregatorOptions> aggregatorOptions, ILoggerFactory loggerFactory, IAsyncCollector<FunctionInstanceLogEntry> registeredFunctionEventCollector = null)
        {
            _aggregatorOptions = aggregatorOptions.Value;
            _loggerFactory = loggerFactory;
            _registeredCollector = registeredFunctionEventCollector;

            _functionEventCollectorLazy = new Lazy<IAsyncCollector<FunctionInstanceLogEntry>>(CreateCollector);
        }

        public IAsyncCollector<FunctionInstanceLogEntry> Create()
        {
            return _functionEventCollectorLazy.Value;
        }

        private IAsyncCollector<FunctionInstanceLogEntry> CreateCollector()
        {
            IAsyncCollector<FunctionInstanceLogEntry> functionEventCollector;

            // Create the aggregator if all the pieces are configured
            IAsyncCollector<FunctionInstanceLogEntry> aggregator = null;
            if (_loggerFactory != null && _aggregatorOptions.IsEnabled)
            {
                aggregator = new FunctionResultAggregator(_aggregatorOptions.BatchSize, _aggregatorOptions.FlushTimeout, _loggerFactory);
            }

            if (_registeredCollector != null && aggregator != null)
            {
                // If there are both an aggregator and a registered FunctionEventCollector, wrap them in a composite
                functionEventCollector = new CompositeFunctionEventCollector(new[] { _registeredCollector, aggregator });
            }
            else
            {
                // Otherwise, take whichever one is null (or use null if both are)
                functionEventCollector = aggregator ?? _registeredCollector;
            }

            return functionEventCollector;
        }
    }
}
