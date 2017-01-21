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
        public void TestConcreteTypeNoConverter()
        {
            TestWorker<ConfigConcreteTypeNoConverter>();
        }
        
        public class ConfigConcreteTypeNoConverter : IExtensionConfigProvider, ITest<ConfigConcreteTypeNoConverter>
        {
            public void Initialize(ExtensionConfigContext context)
            {
                var bf = context.Config.BindingFactory;
                var rule = bf.BindToInput<TestAttribute, AlphaType>(false, typeof(AlphaBuilder));
                context.RegisterBindingRules<TestAttribute>(rule);
            }

            public void Test(TestJobHost<ConfigConcreteTypeNoConverter> host)
            {
                host.Call("Func", new { k = 1 });
                Assert.Equal("AlphaBuilder(1)", _log);
            }

            string _log;

            // Input Rule (exact match): --> Widget 
            public void Func([Test("{k}")] AlphaType w)
            {
                _log = w._value;
            }         
        }

        // Use OpenType (a general builder), still no converters. 
        [Fact]
        public void TestOpenTypeNoConverters()
        {
            TestWorker<ConfigOpenTypeNoConverters>();
        }
   
        public class ConfigOpenTypeNoConverters : IExtensionConfigProvider, ITest<ConfigOpenTypeNoConverters>
        {
            public void Initialize(ExtensionConfigContext context)
            {
                var bf = context.Config.BindingFactory;

                // Replaces BindToGeneric
                var rule = bf.BindToInput<TestAttribute, OpenType>(false, typeof(GeneralBuilder<>));
                                
                context.RegisterBindingRules<TestAttribute>(rule);
            }
            
            public void Test(TestJobHost<ConfigOpenTypeNoConverters> host)
            {
                host.Call("Func1", new { k = 1 });
                Assert.Equal("GeneralBuilder_AlphaType(1)", _log); 

                host.Call("Func2", new { k = 2 });
                Assert.Equal("GeneralBuilder_BetaType(2)", _log);
            }

            string _log;

            // Input Rule (generic match): --> Widget
            public void Func1([Test("{k}")] AlphaType w)
            {
                _log = w._value;
            }

            // Input Rule (generic match): --> OtherType
            public void Func2([Test("{k}")] BetaType w)
            {
                _log = w._value;
            }
        }

        [Fact]
        public void TestWithConverters()
        {
            TestWorker<ConfigWithConverters>();
        }

        public class ConfigWithConverters : IExtensionConfigProvider, ITest<ConfigWithConverters>
        {
            public void Initialize(ExtensionConfigContext context)
            {
                var bf = context.Config.BindingFactory;

                bf.ConverterManager.AddConverter<AlphaType, BetaType>(ConvertAlpha2Beta);
                
                // The AlphaType restriction here means that although we have a GeneralBuilder<> that *could*
                // directly build a BetaType, we can only use it to build AlphaTypes, and so we must invoke the converter.
                var rule = bf.BindToInput<TestAttribute, AlphaType>(true, typeof(GeneralBuilder<>));
                                
                context.RegisterBindingRules<TestAttribute>(rule);
            }

            public void Test(TestJobHost<ConfigWithConverters> host)
            {
                host.Call("Func1", new { k = 1 });
                Assert.Equal("GeneralBuilder_AlphaType(1)", _log);

                host.Call("Func2", new { k = 2 });
                Assert.Equal("A2B(GeneralBuilder_AlphaType(2))", _log);                
            }

            string _log;

            // Input Rule (exact match):  --> Widget
            public void Func1([Test("{k}")] AlphaType w)
            {
                _log = w._value;
            }

            // Input Rule (match w/ converter) : --> Widget
            // Converter: Widget --> OtherType
            public void Func2([Test("{k}")] BetaType w)
            {
                _log = w._value;
            }
        }

        // Test ordering. First rule wins. 
        [Fact]
        public void TestMultipleRules()
        {
            TestWorker<ConfigConcreteTypeNoConverter>();
        }

        public class ConfigMultipleRules : IExtensionConfigProvider, ITest<ConfigMultipleRules>
        {
            public void Initialize(ExtensionConfigContext context)
            {
                var bf = context.Config.BindingFactory;
                var rule1 = bf.BindToInput<TestAttribute, AlphaType>(false, typeof(AlphaBuilder));
                var rule2 = bf.BindToInput<TestAttribute, BetaType>(false, typeof(BetaBuilder));
                context.RegisterBindingRules<TestAttribute>(rule1, rule2);
            }

            public void Test(TestJobHost<ConfigMultipleRules> host)
            {
                host.Call("Func", new { k = 1 });
                Assert.Equal("AlphaBuilder(1)", _log);

                host.Call("Func2", new { k = 1 });
                Assert.Equal("BetaBuilder(1)", _log);
            }

            string _log;
                        
            public void Func([Test("{k}")] AlphaType w)
            {
                _log = w._value;
            }

            // Input Rule (exact match): --> Widget 
            public void Func2([Test("{k}")] BetaType w)
            {
                _log = w._value;
            }
        }

        // Error case. 
        [Fact]
        public void TestError1()
        {
            TestWorker<ConfigError1>();
        }

        public class ConfigError1 : IExtensionConfigProvider, ITest<ConfigError1>
        {
            public void Initialize(ExtensionConfigContext context)
            {
                var bf = context.Config.BindingFactory;
                
                var rule = bf.BindToInput<TestAttribute, OpenType>(true, typeof(AlphaBuilder));

                context.RegisterBindingRules<TestAttribute>(rule);
            }

            public void Test(TestJobHost<ConfigError1> host)
            {
                host.AssertIndexingError("Func", ErrorMessage(typeof(BetaType)));
            }
      
            // Fail to bind because: 
            // We only have an AlphaBuilder, and no registered converters from Alpha-->Beta
            public void Func([Test("{k}")] BetaType w)
            {
                Assert.False(true); // Method shouldn't have been invoked. 
            }
        }

        // Get standard error message for failing to bind an attribute to a given parameter type.
        static string ErrorMessage(Type parameterType)
        {
            return $"Can't bind Test to type '{parameterType.FullName}'.";
        }
     
        // Glue to initialize a JobHost with the correct config and invoke the Test method. 
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

        // Converter for building instances of RedType from an attribute
        class BetaBuilder
        {
            private AlphaType Convert(TestAttribute attr)
            {
                return AlphaType.New("BetaBuilder(" + attr.Path + ")");
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
