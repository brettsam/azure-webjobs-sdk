// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Azure.WebJobs.ServiceBus;

namespace Microsoft.Extensions.Hosting
{
    public static class EventHubHostBuilderExtensions
    {
        public static IHostBuilder AddEventHubs(this IHostBuilder hostBuilder)
        {
            return hostBuilder.AddEventHubs(new EventHubConfiguration());
        }

        public static IHostBuilder AddEventHubs(this IHostBuilder hostBuilder, EventHubConfiguration config)
        {
            return hostBuilder
                .AddExtension<EventHubConfiguration>();
        }
    }
}
