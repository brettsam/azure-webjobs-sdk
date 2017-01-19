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
        /// <typeparam name="TSource"></typeparam>
        /// <typeparam name="TDestination"></typeparam>
        /// <typeparam name="TAttribute"></typeparam>
        /// <param name="converterManager"></param>
        /// <param name="typeConverter"></param>
        /// <param name="constructorArgs"></param>
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
                // typeConverter may have open generic types.  method has now resolved those types. 
                var declaringType = method.DeclaringType;

                object converterInstance;
                try
                {
                    // common for constructor to throw validation errors.          
                    converterInstance = Activator.CreateInstance(declaringType, constructorArgs);
                }
                catch (TargetInvocationException e)
                {
                    throw e.InnerException;
                }

                return PatternMatcher.GetConverterFunc(converterInstance, method);
            });
        }

        /// <summary>
        /// Add a converter for the given Source to Destination conversion.
        /// </summary>
        /// <typeparam name="TSource"></typeparam>
        /// <typeparam name="TDestination"></typeparam>
        /// <typeparam name="TAttribute"></typeparam>
        /// <param name="converterManager"></param>
        /// <param name="converterInstance"></param>
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
