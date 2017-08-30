// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Logging.ApplicationInsights
{
    internal class WebJobsTelemetryInitializer : ITelemetryInitializer
    {
        private const string ComputerNameKey = "COMPUTERNAME";
        private const string WebSiteInstanceIdKey = "WEBSITE_INSTANCE_ID";

        private static string _roleInstanceName = GetRoleInstanceName();

        public void Initialize(ITelemetry telemetry)
        {
            if (telemetry == null)
            {
                return;
            }

            telemetry.Context.Cloud.RoleInstance = _roleInstanceName;

            // Zero out all IP addresses other than Requests
            if (!(telemetry is RequestTelemetry))
            {
                telemetry.Context.Location.Ip = LoggingConstants.ZeroIpAddress;
            }

            if (telemetry is DependencyTelemetry dependency && dependency.Properties.ContainsKey(LogConstants.CategoryNameKey))
            {
                SanitizeDependency(dependency);
                return;
            }

            // Apply our special scope properties
            IDictionary<string, object> scopeProps = DictionaryLoggerScope.GetMergedStateDictionary() ?? new Dictionary<string, object>();

            telemetry.Context.Operation.Id = scopeProps.GetValueOrDefault<string>(ScopeKeys.FunctionInvocationId);
            telemetry.Context.Operation.Name = scopeProps.GetValueOrDefault<string>(ScopeKeys.FunctionName);

            // Apply Category and LogLevel to all telemetry
            ISupportProperties telemetryProps = telemetry as ISupportProperties;
            if (telemetryProps != null)
            {
                string category = scopeProps.GetValueOrDefault<string>(LogConstants.CategoryNameKey);
                if (category != null)
                {
                    telemetryProps.Properties[LogConstants.CategoryNameKey] = category;
                }

                LogLevel? logLevel = scopeProps.GetValueOrDefault<LogLevel?>(LogConstants.LogLevelKey);
                if (logLevel != null)
                {
                    telemetryProps.Properties[LogConstants.LogLevelKey] = logLevel.Value.ToString();
                }
            }
        }

        private static void SanitizeDependency(DependencyTelemetry dependency)
        {
            if (dependency == null)
            {
                return;
            }

            // We poll 'azure-webjobs-host-{id}' queues looking for shutdown calls, but it may
            // not exist. We don't want this to look like an error.
            if (dependency.Type == "Azure queue" &&
                dependency.Name.Contains("azure-webjobs-host-") &&
                dependency.ResultCode == "404")
            {
                dependency.Success = true;
            }
        }

        private static string GetRoleInstanceName()
        {
            string instanceName = Environment.GetEnvironmentVariable(WebSiteInstanceIdKey);
            if (string.IsNullOrEmpty(instanceName))
            {
                instanceName = Environment.GetEnvironmentVariable(ComputerNameKey);
            }

            return instanceName;
        }
    }
}
