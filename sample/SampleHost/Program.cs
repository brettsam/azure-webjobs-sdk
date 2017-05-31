// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Logging;

namespace SampleHost
{
    class Program
    {
        static void Main(string[] args)
        {
            var config = new JobHostConfiguration();
            config.Queues.VisibilityTimeout = TimeSpan.FromSeconds(15);
            config.Queues.MaxDequeueCount = 3;

            if (config.IsDevelopment)
            {
                config.UseDevelopmentSettings();
            }

            CheckAndEnableAppInsights(config);

            config.Queues.BatchSize = 1;

            var host = new JobHost(config);
            host.RunAndBlock();
        }

        private static void CheckAndEnableAppInsights(JobHostConfiguration config)
        {
            // If AppInsights is enabled, build up a LoggerFactory
            string instrumentationKey = Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY");
            if (!string.IsNullOrEmpty(instrumentationKey))
            {
                var aiFilter = new LogCategoryFilter();
                aiFilter.DefaultLevel = LogLevel.Information;

                var conFilter = new LogCategoryFilter();
                conFilter.DefaultLevel = LogLevel.Information;

                config.LoggerFactory = new LoggerFactory()
                    .AddApplicationInsights(instrumentationKey, aiFilter.Filter)
                    .AddConsole(conFilter.Filter);

                config.Tracing.ConsoleLevel = TraceLevel.Off;
            }
        }
    }
}
