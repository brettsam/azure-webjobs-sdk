﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Loggers;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    public class JobHostConfigurationTests
    {
        [Fact]
        public void ConstructorDefaults()
        {
            JobHostOptions config = new JobHostOptions();

            // TODO: DI: Fix as required
            //Assert.NotNull(config.Singleton);
            //Assert.NotNull(config.LoggerFactory);
            //Assert.False(config.Blobs.CentralizedPoisonQueue);

            StorageClientFactory clientFactory = null; // config.GetService<StorageClientFactory>();
            Assert.NotNull(clientFactory);
        }

        [Fact]
        public void HostId_IfNull_DoesNotThrow()
        {
            // Arrange
            JobHostOptions configuration = new JobHostOptions();
            string hostId = null;

            // Act & Assert
            configuration.HostId = hostId;
        }

        [Fact]
        public void HostId_IfValid_DoesNotThrow()
        {
            // Arrange
            JobHostOptions configuration = new JobHostOptions();
            string hostId = "abc";

            // Act & Assert
            configuration.HostId = hostId;
        }

        [Fact]
        public void HostId_IfMinimumLength_DoesNotThrow()
        {
            // Arrange
            JobHostOptions configuration = new JobHostOptions();
            string hostId = "a";

            // Act & Assert
            configuration.HostId = hostId;
        }

        [Fact]
        public void HostId_IfMaximumLength_DoesNotThrow()
        {
            // Arrange
            JobHostOptions configuration = new JobHostOptions();
            const int maximumValidCharacters = 32;
            string hostId = new string('a', maximumValidCharacters);

            // Act & Assert
            configuration.HostId = hostId;
        }

        [Fact]
        public void HostId_IfContainsEveryValidLetter_DoesNotThrow()
        {
            // Arrange
            JobHostOptions configuration = new JobHostOptions();
            string hostId = "abcdefghijklmnopqrstuvwxyz";

            // Act & Assert
            configuration.HostId = hostId;
        }

        [Fact]
        public void HostId_IfContainsEveryValidOtherCharacter_DoesNotThrow()
        {
            // Arrange
            JobHostOptions configuration = new JobHostOptions();
            string hostId = "0-123456789";

            // Act & Assert
            configuration.HostId = hostId;
        }

        [Fact]
        public void HostId_IfEmpty_Throws()
        {
            TestHostIdThrows(String.Empty);
        }

        [Fact]
        public void HostId_IfTooLong_Throws()
        {
            const int maximumValidCharacters = 32;
            string hostId = new string('a', maximumValidCharacters + 1);
            TestHostIdThrows(hostId);
        }

        [Fact]
        public void HostId_IfContainsInvalidCharacter_Throws()
        {
            // Uppercase character are not allowed.
            TestHostIdThrows("aBc");
        }

        [Fact]
        public void HostId_IfStartsWithDash_Throws()
        {
            TestHostIdThrows("-abc");
        }

        [Fact]
        public void HostId_IfEndsWithDash_Throws()
        {
            TestHostIdThrows("abc-");
        }

        [Fact]
        public void HostId_IfContainsConsecutiveDashes_Throws()
        {
            TestHostIdThrows("a--bc");
        }

        [Fact]
        public void JobActivator_IfNull_Throws()
        {
            JobHostOptions configuration = new JobHostOptions();

            Assert.False(true, "Remove once DI fixes are in place");
            //ExceptionAssert.ThrowsArgumentNull(() => configuration.JobActivator = null, "value");
        }

        // TODO: DI: Change to use IHostingEnvironment
        //[Theory]
        //[InlineData(null, false)]
        //[InlineData("Blah", false)]
        //[InlineData("Development", true)]
        //[InlineData("development", true)]
        //public void IsDevelopment_ReturnsCorrectValue(string settingValue, bool expected)
        //{
        //    using (EnvVarHolder.Set(Constants.EnvironmentSettingName, settingValue))
        //    {
        //        JobHostOptions config = new JobHostOptions();
        //        Assert.Equal(config.IsDevelopment, expected);
        //    }
        //}

        //public void UseDevelopmentSettings_ConfiguresCorrectValues()
        //{
        //    using (EnvVarHolder.Set(Constants.EnvironmentSettingName, "Development"))
        //    {
        //        JobHostOptions config = new JobHostOptions();
        //        Assert.False(config.UsingDevelopmentSettings);

        //        if (config.IsDevelopment)
        //        {
        //            config.UseDevelopmentSettings();
        //        }

        //        Assert.True(config.UsingDevelopmentSettings);
        //        Assert.Equal(TimeSpan.FromSeconds(2), config.Queues.MaxPollingInterval);
        //        Assert.Equal(TimeSpan.FromSeconds(15), config.Singleton.ListenerLockPeriod);
        //    }
        //}

        private static void TestHostIdThrows(string hostId)
        {
            // Arrange
            JobHostOptions configuration = new JobHostOptions();

            // Act & Assert
            ExceptionAssert.ThrowsArgument(() => { configuration.HostId = hostId; }, "value",
                "A host ID must be between 1 and 32 characters, contain only lowercase letters, numbers, and " +
                "dashes, not start or end with a dash, and not contain consecutive dashes.");
        }

        private class CustomStorageClientFactory : StorageClientFactory
        {
        }

        private class FastLogger : IAsyncCollector<FunctionInstanceLogEntry>
        {
            public List<FunctionInstanceLogEntry> List = new List<FunctionInstanceLogEntry>();

            public static FunctionInstanceLogEntry FlushEntry = new FunctionInstanceLogEntry(); // marker for flushes

            public Task AddAsync(FunctionInstanceLogEntry item, CancellationToken cancellationToken = default(CancellationToken))
            {
                if (item.Arguments == null)
                {
                    return Task.CompletedTask;
                }
                var clone = JsonConvert.DeserializeObject<FunctionInstanceLogEntry>(JsonConvert.SerializeObject(item));
                List.Add(clone);
                return Task.CompletedTask;
            }

            public Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
            {
                List.Add(FlushEntry);
                return Task.CompletedTask;
            }
        }

        // Verify that JobHostConfig pulls a Sas container from appsettings. 
        [Fact]
        public void JobHost_UsesSas()
        {
            var fakeSasUri = "https://contoso.blob.core.windows.net/myContainer?signature=foo";
            using (EnvVarHolder.Set("AzureWebJobsInternalSasBlobContainer", fakeSasUri))
            {
                JobHostOptions config = new JobHostOptions();

                //Assert.NotNull(config.InternalStorageConfiguration); // Env var should cause this to get initialized 

                // TODO: DI:
                //var container = null;//config.InternalStorageConfiguration.InternalContainer;
                //Assert.NotNull(container);

                Assert.False(true, "Remove once DI fixes are in place");
                //Assert.Equal(container.Name, "myContainer"); // specified in sas. 
            }
        }

        // Test that we can explicitly disable storage and call through a function
        // And enable the fast table logger and ensure that's getting events.
        [Fact]
        public void JobHost_NoStorage_Succeeds()
        {
            using (EnvVarHolder.Set("AzureWebJobsStorage", null))
            using (EnvVarHolder.Set("AzureWebJobsDashboard", null))
            {
                JobHostOptions config = new JobHostOptions()
                {
                    // TODO: DI: This needs to be updated to perform proper service registration
                    //TypeLocator = new FakeTypeLocator(typeof(BasicTest))
                };

                // TODO: DI:
                Assert.False(true, "Remove this once fixed");
                //Assert.Null(config.InternalStorageConfiguration);

                // Explicitly disalbe storage.
                config.HostId = Guid.NewGuid().ToString("n");
                // TODO: DI: Use storage account provider
                //config.DashboardConnectionString = null;
                //config.StorageConnectionString = null;

                var randomValue = Guid.NewGuid().ToString();

                StringBuilder sbLoggingCallbacks = new StringBuilder();
                var fastLogger = new FastLogger();

                // TODO: DI: This needs to be updated to perform proper service registration
                // config.AddService<IAsyncCollector<FunctionInstanceLogEntry>>(fastLogger);

                JobHost host = new JobHost(new OptionsWrapper<JobHostOptions>(new JobHostOptions()), new Mock<IJobHostContextFactory>().Object);

                // Manually invoked.
                var method = typeof(BasicTest).GetMethod("Method", BindingFlags.Public | BindingFlags.Static);

                host.Call(method, new { value = randomValue });
                Assert.True(BasicTest.Called);

                Assert.Equal(2, fastLogger.List.Count); // We should be batching, so flush not called yet.

                host.Start(); // required to call stop()
                host.Stop(); // will ensure flush is called.

                // Verify fast logs
                Assert.Equal(3, fastLogger.List.Count);

                var startMsg = fastLogger.List[0];
                Assert.Equal("BasicTest.Method", startMsg.FunctionName);
                Assert.Equal(null, startMsg.EndTime);
                Assert.NotNull(startMsg.StartTime);

                var endMsg = fastLogger.List[1];
                Assert.Equal(startMsg.FunctionName, endMsg.FunctionName);
                Assert.Equal(startMsg.StartTime, endMsg.StartTime);
                Assert.Equal(startMsg.FunctionInstanceId, endMsg.FunctionInstanceId);
                Assert.NotNull(endMsg.EndTime); // signal completed
                Assert.True(endMsg.StartTime <= endMsg.EndTime);
                Assert.Null(endMsg.ErrorDetails);
                Assert.Null(endMsg.ParentId);

                Assert.Equal(2, endMsg.Arguments.Count);
                Assert.True(endMsg.Arguments.ContainsKey("log"));
                Assert.Equal(randomValue, endMsg.Arguments["value"]);
                Assert.Equal("val=" + randomValue, endMsg.LogOutput.Trim());

                Assert.Same(FastLogger.FlushEntry, fastLogger.List[2]);
            }
        }

        [Fact]
        public void TestServices()
        {
            // Test configuration similar to how ScriptRuntime works.             
            // - config is created and immediatey passed to a JobHost ctor
            // - config is then initialized, including adding extensions 
            // - extensions may register their own services. 
            JobHostOptions config = new JobHostOptions();
            var host = new JobHost(new OptionsWrapper<JobHostOptions>(new JobHostOptions()), new Mock<IJobHostContextFactory>().Object);

            // TODO: DI: This needs to be updated to perform proper service registration
            //var lockManager = config.GetService<IDistributedLockManager>();
            IDistributedLockManager lockManager = null;
            Assert.Null(lockManager); // Not initialized yet. 

            var nameResolver = new FakeNameResolver();
            // TODO: DI: This needs to be updated to perform proper service registration
            //config.AddExtension(new TestExtension()); // this extension will add services. 
            //config.AddService<INameResolver>(nameResolver);

            //  Now succeeds when called on JobHost instead of Config object. 
            // TODO: DI: This needs to be updated to perform proper service registration
            //lockManager = host.Services.GetService<IDistributedLockManager>();

            Assert.NotNull(lockManager);
            Assert.IsType<TestLockManager>(lockManager); // verify it's our custom type             
        }

        // A test extension. This registers a new service in the initialization path. 
        class TestExtension : IExtensionConfigProvider
        {
            public void Initialize(ExtensionConfigContext context)
            {
                // TODO: DI: This needs to be updated to perform proper service registration
                //context.Config.AddService<IDistributedLockManager>(new TestLockManager());
            }
        }

        class TestLockManager : IDistributedLockManager
        {
            public Task<string> GetLockOwnerAsync(string account, string lockId, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task ReleaseLockAsync(IDistributedLock lockHandle, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<bool> RenewAsync(IDistributedLock lockHandle, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }

            public Task<IDistributedLock> TryLockAsync(string account, string lockId, string lockOwnerId, string proposedLeaseId, TimeSpan lockPeriod, CancellationToken cancellationToken)
            {
                throw new NotImplementedException();
            }
        }

        public class BasicTest
        {
            public static bool Called = false;

            [NoAutomaticTrigger]
            public static void Method(TextWriter log, string value)
            {
                log.Write("val={0}", value);
                Called = true;
            }
        }
    }
}