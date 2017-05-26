// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.Loggers.Logger;
using Microsoft.Azure.WebJobs.Logging;

namespace Microsoft.Extensions.Logging
{
    /// <summary>
    /// Extension methods for <see cref="ILogger"/>.
    /// </summary>
    [CLSCompliant(false)]
    public static class ILoggerExtensions
    {
        /// <summary>        
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="metricName"></param>
        /// <param name="metricValue"></param>
        /// <param name="level"></param>
        public static void LogMetric(this ILogger logger, string metricName, double metricValue, LogLevel level = LogLevel.Information)
        {
            IDictionary<string, object> values = new Dictionary<string, object>();
            values[LoggingKeys.MetricName] = metricName;
            values[LoggingKeys.MetricValue] = metricValue;

            logger?.Log(level, LogEvents.Metric, values, null, (s, e) => s.ToString());
        }

        /// <summary>        
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="metricName"></param>
        /// <param name="count"></param>
        /// <param name="sum"></param>
        /// <param name="level"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="standardDeviation"></param>
        public static void LogMetric(this ILogger logger, string metricName, int count, double sum, double min, double max,
            double standardDeviation, LogLevel level = LogLevel.Information)
        {
            IDictionary<string, object> values = new Dictionary<string, object>();
            values[LoggingKeys.MetricName] = metricName;
            values[LoggingKeys.Count] = count;
            values[LoggingKeys.MetricSum] = sum;
            values[LoggingKeys.MetricMin] = min;
            values[LoggingKeys.MetricMax] = max;
            values[LoggingKeys.MetricStandardDeviation] = standardDeviation;

            logger?.Log(level, LogEvents.Metric, values, null, (s, e) => s.ToString());
        }

        internal static DependencyResult TrackDependency(this ILogger logger, LogLevel level = LogLevel.Information)
        {
            return new DependencyResult(logger, level);
        }

        internal static void LogDependency(this ILogger logger, DependencyResult result, LogLevel level = LogLevel.Information)
        {
            // we won't output any string here, just the data
            FormattedLogValuesCollection payload = new FormattedLogValuesCollection(string.Empty, null, result.ToReadOnlyDictionary());
            logger?.Log(level, LogEvents.Dependency, payload, null, (s, e) => s.ToString());
        }

        // We want the short name for use with Application Insights.
        internal static void LogFunctionResult(this ILogger logger, string shortName, FunctionInstanceLogEntry logEntry, TimeSpan duration, Exception exception = null)
        {
            bool succeeded = exception == null;

            // build the string and values
            string result = succeeded ? "Succeeded" : "Failed";
            string logString = $"Executed '{{{LoggingKeys.FullName}}}' ({result}, Id={{{LoggingKeys.InvocationId}}})";
            object[] values = new object[]
            {
                logEntry.FunctionName,
                logEntry.FunctionInstanceId
            };

            // generate additional payload that is not in the string
            IDictionary<string, object> properties = new Dictionary<string, object>();
            properties.Add(LoggingKeys.Name, shortName);
            properties.Add(LoggingKeys.TriggerReason, logEntry.TriggerReason);
            properties.Add(LoggingKeys.StartTime, logEntry.StartTime);
            properties.Add(LoggingKeys.EndTime, logEntry.EndTime);
            properties.Add(LoggingKeys.Duration, duration);
            properties.Add(LoggingKeys.Succeeded, succeeded);

            if (logEntry.Arguments != null)
            {
                foreach (var arg in logEntry.Arguments)
                {
                    properties.Add(LoggingKeys.ParameterPrefix + arg.Key, arg.Value);
                }
            }

            FormattedLogValuesCollection payload = new FormattedLogValuesCollection(logString, values, new ReadOnlyDictionary<string, object>(properties));
            LogLevel level = succeeded ? LogLevel.Information : LogLevel.Error;
            logger.Log(level, 0, payload, exception, (s, e) => s.ToString());
        }

        internal static void LogFunctionResultAggregate(this ILogger logger, FunctionResultAggregate resultAggregate)
        {
            // we won't output any string here, just the data
            FormattedLogValuesCollection payload = new FormattedLogValuesCollection(string.Empty, null, resultAggregate.ToReadOnlyDictionary());
            logger.Log(LogLevel.Information, 0, payload, null, (s, e) => s.ToString());
        }

        internal static IDisposable BeginFunctionScope(this ILogger logger, IFunctionInstance functionInstance)
        {
            return logger?.BeginScope(
                new Dictionary<string, object>
                {
                    [ScopeKeys.FunctionInvocationId] = functionInstance?.Id,
                    [ScopeKeys.FunctionName] = functionInstance?.FunctionDescriptor?.Method?.Name
                });
        }
    }
}
