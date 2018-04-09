﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.TestCommon
{
    public static class TestHelpers
    {
        public static async Task Await(Func<Task<bool>> condition, int timeout = 60 * 1000, int pollingInterval = 2 * 1000)
        {
            DateTime start = DateTime.Now;
            while (!await condition())
            {
                await Task.Delay(pollingInterval);

                if ((DateTime.Now - start).TotalMilliseconds > timeout)
                {
                    throw new ApplicationException("Condition not reached within timeout.");
                }
            }
        }

        public static async Task Await(Func<bool> condition, int timeout = 60 * 1000, int pollingInterval = 2 * 1000)
        {
            await Await(() => Task.FromResult(condition()), timeout, pollingInterval);
        }

        public static void WaitOne(WaitHandle handle, int timeout = 60 * 1000)
        {
            bool ok = handle.WaitOne(timeout);
            if (!ok)
            {
                // timeout. Event not signaled in time. 
                throw new ApplicationException("Condition not reached within timeout.");
            }
        }

        public static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                field = target.GetType().GetField($"<{fieldName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            }
            field.SetValue(target, value);
        }

        public static T New<T>()
        {
            var constructor = typeof(T).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new Type[] { }, null);
            return (T)constructor.Invoke(null);
        }

        // Test that we get an indexing error (FunctionIndexingException)  
        // functionName - the function name that has the indexing error. 
        // expectedErrorMessage - inner exception's message with details.
        // Invoking func() should cause an indexing error. 
        public static void AssertIndexingError(Action func, string functionName, string expectedErrorMessage)
        {
            try
            {
                func(); // expected indexing error
            }
            catch (FunctionIndexingException e)
            {
                Assert.Equal("Error indexing method '" + functionName + "'", e.Message);
                Assert.StartsWith(expectedErrorMessage, e.InnerException.Message);
                return;
            }
            Assert.True(false, "Invoker should have failed");
        }

        public static JobHost<TProgram> NewJobHost<TProgram>(params object[] services)
        {
            return null;
        }

        public static IHostBuilder ConfigureDefaultTestHost(this IHostBuilder builder, params Type[] types)
        {
            return builder.ConfigureWebJobsHost()
               .ConfigureServices(services =>
               {
                   services.AddSingleton<ITypeLocator>(new FakeTypeLocator(types));

                   // Register this to fail a test if a background exception is thrown
                   services.AddSingleton<IWebJobsExceptionHandlerFactory, TestExceptionHandlerFactory>();
               })
               .ConfigureLogging(logging =>
               {
                   logging.AddProvider(new TestLoggerProvider());
               });
        }

        public static IHostBuilder ConfigureDefaultTestHost<TProgram>(this IHostBuilder builder)
        {
            return builder.ConfigureDefaultTestHost(typeof(TProgram));
        }

        public static TestLoggerProvider GetTestLoggerProvider(this IHost host)
        {
            return host.Services.GetServices<ILoggerProvider>().OfType<TestLoggerProvider>().Single();
        }

        public static TExtension GetExtension<TExtension>(this IHost host)
        {
            return host.Services.GetServices<IExtensionConfigProvider>().OfType<TExtension>().SingleOrDefault();
        }

        public static JobHost GetJobHost(this IHost host)
        {
            return host.Services.GetService<IJobHost>() as JobHost;
        }

        public static JobHostOptions NewConfig<TProgram>(params object[] services)
        {
            return NewConfig(typeof(TProgram), services);
        }

        // Helper to create a JobHostConfiguraiton from a set of services. 
        // Default config, pure-in-memory
        // Default is pure-in-memory, good for unit testing. 
        public static JobHostOptions NewConfig(Type functions, params object[] services)
        {
            var config = NewConfig(services);
            if (!services.OfType<ITypeLocator>().Any())
            {
                // TODO: DI: This needs to be updated to perform proper service registration
                //config.AddServices(new FakeTypeLocator(functions));
            }
            return config;
        }

        public static JobHostOptions NewConfig(
            params object[] services
            )
        {
            var loggerFactory = new LoggerFactory();
            ILoggerProvider loggerProvider = services.OfType<ILoggerProvider>().SingleOrDefault() ?? new TestLoggerProvider();
            loggerFactory.AddProvider(loggerProvider);

            var config = new JobHostOptions()
            {
                // Pure in-memory, no storage. 
                HostId = Guid.NewGuid().ToString("n")
            };

            // TODO: DI: This needs to be updated to perform proper service registration
            //config.AddServices(services);
            return config;
        }

        // TODO: DI: This needs to be updated to perform proper service registration
        //public static void AddWebJobsServices(this IServiceCollection services, params object[] servicesToRegister)
        //{
        //    // Set extensionRegistry first since other services may depend on it. 
        //    foreach (var obj in services)
        //    {
        //        IExtensionRegistry extensionRegistry = obj as IExtensionRegistry;
        //        if (extensionRegistry != null)
        //        {
        //            services.AddSingleton<IExtensionRegistry>(extensionRegistry);
        //            break;
        //        }
        //    }

        //    IExtensionRegistry extensions = config.GetService<IExtensionRegistry>();

        //    var types = new Type[] {
        //        typeof(IAsyncCollector<FunctionInstanceLogEntry>),
        //        typeof(IHostInstanceLoggerProvider),
        //        typeof(IFunctionInstanceLoggerProvider),
        //        typeof(IFunctionOutputLoggerProvider),
        //        typeof(IConsoleProvider),
        //        typeof(ITypeLocator),
        //        typeof(IWebJobsExceptionHandler),
        //        typeof(INameResolver),
        //        typeof(IJobActivator),
        //        typeof(IExtensionTypeLocator),
        //        typeof(SingletonManager),
        //        typeof(IHostIdProvider),
        //        typeof(IQueueConfiguration),
        //        typeof(IExtensionRegistry),
        //        typeof(IDistributedLockManager),
        //        typeof(ILoggerFactory),
        //        typeof(IFunctionIndexProvider) // set to unit test indexing. 
        //    };

        //    foreach (var obj in services)
        //    {
        //        if (obj == null ||
        //            obj is ILoggerProvider)
        //        {
        //            continue;
        //        }

        //        IStorageAccountProvider storageAccountProvider = obj as IStorageAccountProvider;
        //        IStorageAccount account = obj as IStorageAccount;
        //        if (account != null)
        //        {
        //            storageAccountProvider = new FakeStorageAccountProvider
        //            {
        //                StorageAccount = account
        //            };
        //        }
        //        if (storageAccountProvider != null)
        //        {
        //            config.AddService<IStorageAccountProvider>(storageAccountProvider);
        //            continue;
        //        }

        //        // A new extension 
        //        IExtensionConfigProvider extension = obj as IExtensionConfigProvider;
        //        if (extension != null)
        //        {
        //            extensions.RegisterExtension<IExtensionConfigProvider>(extension);
        //            continue;
        //        }

        //        // A function filter
        //        if (obj is IFunctionInvocationFilter)
        //        {
        //            extensions.RegisterExtension<IFunctionInvocationFilter>((IFunctionInvocationFilter)obj);
        //            continue;
        //        }

        //        if (obj is IFunctionExceptionFilter)
        //        {
        //            extensions.RegisterExtension<IFunctionExceptionFilter>((IFunctionExceptionFilter)obj);
        //            continue;
        //        }

        //        // basic pattern. 
        //        bool ok = false;
        //        foreach (var type in types)
        //        {
        //            if (type.IsAssignableFrom(obj.GetType()))
        //            {
        //                config.AddService(type, obj);
        //                ok = true;
        //                break;
        //            }
        //        }
        //        if (ok)
        //        {
        //            continue;
        //        }

        //        throw new InvalidOperationException("Test bug: Unrecognized type: " + obj.GetType().FullName);
        //    }
        //}

        private class FakeStorageAccountProvider : IStorageAccountProvider
        {
            public IStorageAccount StorageAccount { get; set; }

            public IStorageAccount DashboardAccount { get; set; }

            public string StorageConnectionString => throw new NotImplementedException();

            public string DashboardConnectionString => throw new NotImplementedException();

            public Task<IStorageAccount> TryGetAccountAsync(string connectionStringName, CancellationToken cancellationToken)
            {
                IStorageAccount account;

                if (connectionStringName == ConnectionStringNames.Storage)
                {
                    account = StorageAccount;
                }
                else if (connectionStringName == ConnectionStringNames.Dashboard)
                {
                    account = DashboardAccount;
                }
                else
                {
                    account = null;
                }

                return Task.FromResult(account);
            }
        }

        public static IJobHostMetadataProvider CreateMetadataProvider(this JobHost host)
        {
            throw new NotImplementedException();
            // return host.Services.GetService<IJobHostMetadataProvider>();
        }
    }
}
