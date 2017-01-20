// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Protocols;
namespace Microsoft.Azure.WebJobs.Host.Bindings
{
    internal class ClassX<TAttribute, TType> : IBindingProvider
        where TAttribute : Attribute
    {
        private readonly INameResolver _nameResolver;
        private readonly IConverterManager _converterManager;
        private readonly Type _typeBuilder;
        private readonly object[] _constructorArgs;

        public ClassX(
            INameResolver nameResolver,
            IConverterManager converterManager,
            Type typeBuilder,
            object[] constructorArgs)
        {
            this._nameResolver = nameResolver;
            this._converterManager = converterManager; // could be null 
            this._typeBuilder = typeBuilder;
            this._constructorArgs = constructorArgs;
        }

        public Task<IBinding> TryCreateAsync(BindingProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            var parameter = context.Parameter;
            var typeUser = parameter.ParameterType;

            if (typeUser.IsByRef)
            {
                return Task.FromResult<IBinding>(null);
            }

            var type = typeof(ExactBinding<>).MakeGenericType(typeof(TAttribute), typeof(TType), typeUser);
            var method = type.GetMethod("TryBuild", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            var binding = (IBinding)method.Invoke(null, new object[] { this, context });

            return Task.FromResult<IBinding>(binding);
        }

        private class ExactBinding<TUserType> : BindingBase<TAttribute>
        {
            private readonly Func<object, object> _buildFromAttribute;

            private readonly FuncConverter<TType, TAttribute, TUserType> _converter;

            public ExactBinding(
                AttributeCloner<TAttribute> cloner,
                ParameterDescriptor param,
                Func<object, object> buildFromAttribute,
                FuncConverter<TType, TAttribute, TUserType> converter) : base(cloner, param)
            {
                this._buildFromAttribute = buildFromAttribute;
                this._converter = converter;
            }

            public static ExactBinding<TUserType> TryBuild(
                ClassX<TAttribute, TType> parent,
                BindingProviderContext context)
            {
                var cm = parent._converterManager;
                Type typeBuilder = parent._typeBuilder;
                object[] constructorArgs = parent._constructorArgs;

                var parameter = context.Parameter;
                TAttribute attributeSource = parameter.GetCustomAttribute<TAttribute>(inherit: false);

                var cloner = new AttributeCloner<TAttribute>(attributeSource, context.BindingDataContract, parent._nameResolver);

                Func<object, object> buildFromAttribute;
                FuncConverter<TType, TAttribute, TUserType> converter = null;

                if (cm == null)
                {
                    // Must be an exact match 
                    // Builder must provide: TAttribute --> TUserType

                    // Use TType as a type-filter. 
                    var checker = ConverterManager.GetTypeValidator<TType>();
                    if (!checker.IsMatch(typeof(TUserType)))
                    {
                        return null;
                    }

                    var method = PatternMatcher.FindConverterMethod(typeBuilder, typeof(TAttribute), typeof(TUserType));
                    if (method == null)
                    {
                        return null;
                    }
                    buildFromAttribute = PatternMatcher.CreateInstanceAndGetConverterFunc(constructorArgs, method);
                }
                else
                {
                    // Find a builder for :   TAttribute --> TType
                    // and then coupole with a converter:  TType --> TParameterType
                    converter = cm.GetConverter<TType, TUserType, TAttribute>();
                    if (converter == null)
                    {
                        return null;
                    }

                    var method = PatternMatcher.FindConverterMethod(typeBuilder, typeof(TAttribute), typeof(TType));
                    if (method == null)
                    {
                        return null;
                    }

                    buildFromAttribute = PatternMatcher.CreateInstanceAndGetConverterFunc(constructorArgs, method);
                }

                var param = new ParameterDescriptor
                {
                    Name = parameter.Name,
                    DisplayHints = new ParameterDisplayHints
                    {
                        Description = "input"
                    }
                };

                return new ExactBinding<TUserType>(cloner, param, buildFromAttribute, converter);
            }

            protected override Task<IValueProvider> BuildAsync(
                TAttribute attrResolved, 
                ValueBindingContext context)
            {
                string invokeString = Cloner.GetInvokeString(attrResolved);

                object obj = _buildFromAttribute(attrResolved);
                TUserType finalObj;
                if (_converter == null)
                {
                    finalObj = (TUserType)obj;
                }
                else
                {
                    var intermediateObj = (TType)obj;
                    finalObj = _converter(intermediateObj, attrResolved, context);
                }

                IValueProvider vp = new ConstantValueProvider(finalObj, typeof(TUserType), invokeString);

                return Task.FromResult(vp);
            }
        }
    }
}
