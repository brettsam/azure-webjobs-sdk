// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Xunit;

namespace Microsoft.Azure.WebJobs.ServiceBus.UnitTests.Config
{
    public class ServiceBusConfigurationTests
    {
        [Fact]
        public void Constructor_SetsExpectedDefaults()
        {
            ServiceBusOptions config = new ServiceBusOptions();
            Assert.Equal(16, config.MessageOptions.MaxConcurrentCalls);
            Assert.Equal(0, config.PrefetchCount);
        }

        [Fact]
        public void ConnectionString_ReturnsExpectedDefaultUntilSetExplicitly()
        {
            ServiceBusOptions config = new ServiceBusOptions();

            string defaultConnection = null; // AmbientConnectionStringProvider.Instance.GetConnectionString(ConnectionStringNames.ServiceBus);
            Assert.Equal(defaultConnection, config.ConnectionString);

            string testConnection = "testconnection";
            config.ConnectionString = testConnection;
            Assert.Equal(testConnection, config.ConnectionString);
        }

        [Fact]
        public void PrefetchCount_GetSet()
        {
            ServiceBusOptions config = new ServiceBusOptions();
            Assert.Equal(0, config.PrefetchCount);
            config.PrefetchCount = 100;
            Assert.Equal(100, config.PrefetchCount);
        }
    }
}
