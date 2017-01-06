﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Xunit;
using System.Threading.Tasks;
using System.Reflection;
using Microsoft.Azure.WebJobs.Host.Bindings;

namespace Microsoft.Azure.WebJobs.Host.UnitTests.Common
{
    // Test BindingFactory's BindToGenericItem rule.
    public class BindToGenericItemTests
    {
        // Some custom type to bind to. 
        public class Widget
        {
            public static Widget New(TestAttribute attrResolved)
            {
                return new Widget { _value = attrResolved.Path };
            }

            public string _value;
        }

        public class TestAttribute : Attribute
        {
            public TestAttribute(string path)
            {
                this.Path = path;
            }

            [AutoResolve]
            public string Path { get; set; }
        }

        // Represents the native type from this resource's SDK.  
        // for example, CloudMessage, ITableEntity, CloudBlob. 
        // ConverterManager will then convert to the type in the user's signature. 
        private class NativeSdkType
        {
            public TestAttribute _attr;
            public FakeExtClient _client;
        }

        public class FakeExtClient : IExtensionConfigProvider
        {

            private Widget Convert(NativeSdkType i)
            {
                return Widget.New(i._attr);
            }

            public void Initialize(ExtensionConfigContext context)
            {
                var bf = context.Config.BindingFactory;

                bf.ConverterManager.AddConverterBuilder<NativeSdkType, Widget, TestAttribute>(this);

                // Add [Test] support                
                var rule = bf.BindToGeneralAsyncType<TestAttribute, NativeSdkType>(
                    (attr) => Task.FromResult(new NativeSdkType { _attr = attr, _client = this }));
                context.RegisterBindingRules<TestAttribute>(rule);
            }            
        }

        public class Program
        {
            public string _value;

            public void Func([Test("abc-{k}")] Widget w)
            {
                _value = w._value;
            }
        }

        [Fact]
        public void Test()
        {
            var prog = new Program();
            var jobActivator = new FakeActivator();
            jobActivator.Add(prog);

            var host = TestHelpers.NewJobHost<Program>(jobActivator, new FakeExtClient());
            host.Call("Func", new { k = 1 });

            // Skipped first rule, applied second 
            Assert.Equal(prog._value, "abc-1");
        }


        // Unit test that we can properly extract TMessage from a parameter type. 
        [Fact]
        public void GetCoreType()
        {
            Assert.Equal(null, BindingFactoryHelpers.GetAsyncCollectorCoreType(typeof(Widget))); // Not an AsyncCollector type

            Assert.Equal(typeof(Widget), BindingFactoryHelpers.GetAsyncCollectorCoreType(typeof(IAsyncCollector<Widget>)));
            Assert.Equal(typeof(Widget), BindingFactoryHelpers.GetAsyncCollectorCoreType(typeof(ICollector<Widget>)));
            Assert.Equal(typeof(Widget), BindingFactoryHelpers.GetAsyncCollectorCoreType(typeof(Widget).MakeByRefType()));
            Assert.Equal(typeof(Widget), BindingFactoryHelpers.GetAsyncCollectorCoreType(typeof(Widget[]).MakeByRefType()));

            // Verify that 'out' takes precedence over generic. 
            Assert.Equal(typeof(IFoo<Widget>), BindingFactoryHelpers.GetAsyncCollectorCoreType(typeof(IFoo<Widget>).MakeByRefType()));
        }

        // Random generic type to use in tests. 
        interface IFoo<T>
        {
        }
    }
}
