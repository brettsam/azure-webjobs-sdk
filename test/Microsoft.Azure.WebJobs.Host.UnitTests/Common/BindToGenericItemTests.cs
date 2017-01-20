// Copyright (c) .NET Foundation. All rights reserved.
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
    // Test BindingFactory's BindToInput rule.
    // Provide some basic types, converters, builders and make it very easy to test a
    // variety of configuration permutations. 
    // Each Client configuration is its own test case. 
    public class BindToGenericItemTests
    {
        // Each of the TestConfigs below implement this. 
        interface ITest<TConfig>
        {
            void Test(TestJobHost<TConfig> host);
        }

        // Simple case. 
        // Test with concrete types, no converters.
        // Attr-->Widget 
        [Fact]
        public void Test1()
        {
            TestWorker<FakeExtClient>();
        }
        
        public class FakeExtClient : IExtensionConfigProvider, ITest<FakeExtClient>
        {
            public void Initialize(ExtensionConfigContext context)
            {
                var bf = context.Config.BindingFactory;
                var rule = bf.BindToInput<TestAttribute, AlphaType>(false, typeof(AlphaBuilder));
                context.RegisterBindingRules<TestAttribute>(rule);
            }

            string _value;

            // Input Rule (exact match): --> Widget 
            public void Func([Test("{k}")] AlphaType w)
            {
                _value = w._value;
            }

            public void Test(TestJobHost<FakeExtClient> host)
            {
                host.Call("Func", new { k = 1 });
                Assert.Equal("AlphaBuilder(1)", _value);
            }
        }

        [Fact]
        public void Test2()
        {
            TestWorker<FakeExtClient2>();
        }
   
        public class FakeExtClient2 : IExtensionConfigProvider, ITest<FakeExtClient2>
        {
            public void Initialize(ExtensionConfigContext context)
            {
                var bf = context.Config.BindingFactory;

                // Replaces BindToGeneric
                var rule = bf.BindToInput<TestAttribute, OpenType>(false, typeof(GeneralBuilder<>));
                                
                context.RegisterBindingRules<TestAttribute>(rule);
            }
            
            public void Test(TestJobHost<FakeExtClient2> host)
            {
                host.Call("Func1", new { k = 1 });
                Assert.Equal("GeneralBuilder_AlphaType(1)", _value); 

                host.Call("Func2", new { k = 2 });
                Assert.Equal("GeneralBuilder_BetaType(2)", _value);
            }

            string _value;

            // Input Rule (generic match): --> Widget
            public void Func1([Test("{k}")] AlphaType w)
            {
                _value = w._value;
            }

            // Input Rule (generic match): --> OtherType
            public void Func2([Test("{k}")] BetaType w)
            {
                _value = w._value;
            }
        }

        [Fact]
        public void Test3()
        {
            TestWorker<FakeExtClient3>();
        }

        public class FakeExtClient3 : IExtensionConfigProvider, ITest<FakeExtClient3>
        {
            public void Initialize(ExtensionConfigContext context)
            {
                var bf = context.Config.BindingFactory;

                bf.ConverterManager.AddConverter<AlphaType, BetaType>(ConvertAlpha2Beta);
                
                // $$$ If it's OpenType, then we'd short-circuit the converter manager . 
                var rule = bf.BindToInput<TestAttribute, AlphaType>(true, typeof(GeneralBuilder<>));
                                
                context.RegisterBindingRules<TestAttribute>(rule);
            }

            public void Test(TestJobHost<FakeExtClient3> host)
            {
                host.Call("Func1", new { k = 1 });
                Assert.Equal("GeneralBuilder_AlphaType(1)", _value);

                host.Call("Func2", new { k = 2 });
                Assert.Equal("A2B(GeneralBuilder_AlphaType(2))", _value);                
            }

            string _value;

            // Input Rule (exact match):  --> Widget
            public void Func1([Test("{k}")] AlphaType w)
            {
                _value = w._value;
            }

            // Input Rule (match w/ converter) : --> Widget
            // Converter: Widget --> OtherType
            public void Func2([Test("{k}")] BetaType w)
            {
                _value = w._value;
            }
        }
    
        // Config also has the program on it.         
        private void TestWorker<TConfig>() where TConfig : IExtensionConfigProvider, ITest<TConfig>, new() 
        {
            var prog = new TConfig();
            var jobActivator = new FakeActivator();
            jobActivator.Add(prog);

            IExtensionConfigProvider ext = prog;
            var host = TestHelpers.NewJobHost<TConfig>(jobActivator, ext);

            ITest<TConfig> test = prog;
            test.Test(host);
        }
                
        // Unit test that we can properly extract TMessage from a parameter type. 
        [Fact]
        public void GetCoreType()
        {
            Assert.Equal(null, BindingFactoryHelpers.GetAsyncCollectorCoreType(typeof(AlphaType))); // Not an AsyncCollector type

            Assert.Equal(typeof(AlphaType), BindingFactoryHelpers.GetAsyncCollectorCoreType(typeof(IAsyncCollector<AlphaType>)));
            Assert.Equal(typeof(AlphaType), BindingFactoryHelpers.GetAsyncCollectorCoreType(typeof(ICollector<AlphaType>)));
            Assert.Equal(typeof(AlphaType), BindingFactoryHelpers.GetAsyncCollectorCoreType(typeof(AlphaType).MakeByRefType()));
            Assert.Equal(typeof(AlphaType), BindingFactoryHelpers.GetAsyncCollectorCoreType(typeof(AlphaType[]).MakeByRefType()));

            // Verify that 'out' takes precedence over generic. 
            Assert.Equal(typeof(IFoo<AlphaType>), BindingFactoryHelpers.GetAsyncCollectorCoreType(typeof(IFoo<AlphaType>).MakeByRefType()));
        }

        // Random generic type to use in tests. 
        interface IFoo<T>
        {
        }

        // Some custom type to bind to. 
        public class AlphaType
        {
            public static AlphaType New(string value)
            {
                return new AlphaType { _value = value };
            }

            public string _value;
        }

        // Another custom type, not related to the first type. 
        public class BetaType
        {
            public static BetaType New(string value)
            {
                return new BetaType { _value = value };
            }

            public string _value;
        }

        static BetaType ConvertAlpha2Beta(AlphaType x)
        {
            return BetaType.New($"A2B({x._value})");
        }


        // A test attribute for binding.  
        public class TestAttribute : Attribute
        {
            public TestAttribute(string path)
            {
                this.Path = path;
            }

            [AutoResolve]
            public string Path { get; set; }
        }

        // Converter for building instances of RedType from an attribute
        class AlphaBuilder
        {
            private AlphaType Convert(TestAttribute attr)
            {
                return AlphaType.New("AlphaBuilder(" + attr.Path + ")");
            }
        }

        // Can build Widgets or OtherType
        class GeneralBuilder<T>
        {
            private readonly MethodInfo _builder;

            public GeneralBuilder()
            {
                _builder = typeof(T).GetMethod("New", BindingFlags.Public | BindingFlags.Static);
                if (_builder == null)
                {
                    throw new InvalidOperationException($"Type  {typeof(T).Name} should have a static New() method");
                }
            }

            private T Convert(TestAttribute attr)
            {
                var value = $"GeneralBuilder_{typeof(T).Name}({attr.Path})";
                return (T)_builder.Invoke(null, new object[] { value});
            }
        }

    }
}
