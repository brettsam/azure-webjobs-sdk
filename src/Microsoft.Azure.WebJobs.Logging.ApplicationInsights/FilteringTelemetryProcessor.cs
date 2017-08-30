// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    internal class FilteringTelemetryProcessor : ITelemetryProcessor
    {
        private Func<string, LogLevel, bool> _filter;
        private ITelemetryProcessor _next;

        public FilteringTelemetryProcessor(Func<string, LogLevel, bool> filter, ITelemetryProcessor next)
        {
            _filter = filter;
            _next = next;
        }

        public void Process(ITelemetry item)
        {
            if (IsEnabled(item))
            {
                Debug.WriteLine($"Logging {item?.GetType()?.Name}: {item?.Context?.Operation?.Name}");
                _next.Process(item);
            }
        }

        private bool IsEnabled(ITelemetry item)
        {
            bool isDependency = item is DependencyTelemetry;

            // We default to false if this is a Dependency; We only want Dependencies
            // with proper LogLevel attached to flow through.
            bool enabled = !isDependency;

            ISupportProperties properties = item as ISupportProperties;
            if (properties != null && _filter != null)
            {
                string categoryName = null;
                if (!properties.Properties.TryGetValue(LogConstants.CategoryNameKey, out categoryName))
                {
                    // If no category is specified, it will be filtered by the default filter
                    categoryName = string.Empty;
                }

                // Extract the log level and apply the filter
                string logLevelString = null;
                LogLevel logLevel;
                if (properties.Properties.TryGetValue(LogConstants.LogLevelKey, out logLevelString) &&
                    Enum.TryParse(logLevelString, out logLevel))
                {
                    enabled = _filter(categoryName, logLevel);
                }
            }

            return enabled;
        }
    }
}
