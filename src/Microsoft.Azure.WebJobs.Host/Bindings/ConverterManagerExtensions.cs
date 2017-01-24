// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Bindings
{
    /// <summary>
    /// Extensions for <see cref="IConverterManager"/> 
    /// </summary>
    public static class IConverterManagerExtensions
    {
        /// <summary>
        /// Add a converter for the given Source to Destination conversion.
        /// The typeConverter type is instantiated with the type arguments and constructorArgs is passed. 
        /// </summary>
        /// <typeparam name="TSource">Source type.</typeparam>
        /// <typeparam name="TDestination">Destination type.</typeparam>
        /// <typeparam name="TAttribute">Attribute on the binding. </typeparam>
        /// <param name="converterManager">Instance of Converter Manager.</param>
        /// <param name="typeConverter">A type with conversion methods. This can be generic and will get instantiated with the 
        /// appropriate type parameters. </param>
        /// <param name="constructorArgs">Constructor Arguments to pass to the constructor when instantiated. This can pass configuration and state.</param>
        public static void AddConverterBuilder<TSource, TDestination, TAttribute>(
            this IConverterManager converterManager,
            Type typeConverter, 
            params object[] constructorArgs)
            where TAttribute : Attribute
        {
            if (converterManager == null)
            {
                throw new ArgumentNullException("converterManager");
            }
            converterManager.AddConverterBuilder<TSource, TDestination, TAttribute>((typeSource, typeDest) =>
            {
                var method = PatternMatcher.FindConverterMethod(typeConverter, typeSource, typeDest);

                var converter = PatternMatcher.CreateInstanceAndGetConverterFunc(constructorArgs, method);
                return converter; 
            });
        }

        /// <summary>
        /// Add a converter for the given Source to Destination conversion.
        /// </summary>
        /// <typeparam name="TSource">Source type.</typeparam>
        /// <typeparam name="TDestination">Destination type.</typeparam>
        /// <typeparam name="TAttribute">Attribute on the binding. </typeparam>
        /// <param name="converterManager">Instance of Converter Manager.</param>
        /// <param name="converterInstance">Instance of an object with convert methods on it.</param>
        public static void AddConverterBuilder<TSource, TDestination, TAttribute>(
          this IConverterManager converterManager,
          object converterInstance)
          where TAttribute : Attribute
        {
            if (converterManager == null)
            {
                throw new ArgumentNullException("converterManager");
            }
            converterManager.AddConverterBuilder<TSource, TDestination, TAttribute>((typeSource, typeDest) =>
            {
                var typeConverter = converterInstance.GetType();
                var method = PatternMatcher.FindConverterMethod(typeConverter, typeSource, typeDest);

                return PatternMatcher.GetConverterFunc(converterInstance, method);
            });
        }        
    }
}
