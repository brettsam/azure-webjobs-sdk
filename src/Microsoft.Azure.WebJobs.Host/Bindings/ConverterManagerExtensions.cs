// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;

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
            converterManager.AddConverterBuilder<TSource, TDestination, TAttribute>((typeSrc, typeDest) =>
            {
                var tuple = FindConverterMethod(typeConverter, constructorArgs, typeSrc, typeDest);
                object instance = tuple.Item1;
                MethodInfo method = tuple.Item2;

                Func<object, object> converter = (input) =>
                {
                    return method.Invoke(instance, new object[] { input });
                };

                return converter;
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
            converterManager.AddConverterBuilder<TSource, TDestination, TAttribute>((typeSrc, typeDest) =>
            {
                Func<object, object> converter = GetConverter(converterInstance, typeSrc, typeDest);
                return converter;
            });
        }

        // Find a Convert* method that is compatible with the given source and destiation types. 
        // Signature will be like:
        //    TIn Convert*(TOut) 
        // Where TIn, TOut may be generic. This will infer the generics and return a tuple of 
        // a) instantiated converter object, method info for that instance for the converter method.  
        private static Tuple<object, MethodInfo> FindConverterMethod(
            Type typeConverter, 
            object[] constructorArgs,
            Type typeSource, Type typeDest)
        {     
            var allMethods = typeConverter.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var method in allMethods)
            {
                if (!method.Name.StartsWith("Convert", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Dictionary<string, Type> genericArgs = new Dictionary<string, Type>();

                if (!CheckArg(method.ReturnType, typeDest, genericArgs))
                {
                    continue;
                }

                var parameters = method.GetParameters();
                if (!CheckArg(parameters[0].ParameterType, typeSource, genericArgs))
                {
                    continue;
                }

                // Possible match 
                var typeArgs = typeConverter.GetGenericArguments();
                int len = typeArgs.Length;
                var actualTypeArgs = new Type[len];
                for (int i = 0; i < len; i++)
                {
                    actualTypeArgs[i] = genericArgs[typeArgs[i].Name];
                }

                Type finalType = typeConverter;
                if (typeConverter.IsGenericTypeDefinition)
                {
                    finalType = typeConverter.MakeGenericType(actualTypeArgs);
                }

                try
                {
                    var instance = Activator.CreateInstance(finalType, constructorArgs);
                    var resolvedMethod = Resolve(finalType, method);
                    return Tuple.Create(instance, resolvedMethod);
                }
                catch (TargetInvocationException e)
                {
                    throw e.InnerException;
                }
            }

            throw new InvalidOperationException("No Convert method on type " + typeConverter.Name + " to convert from " +
                typeSource.Name + " to " + typeDest.Name);
        }

        // Given a method info on the open generic; resolve it on a closed generic. 
        // Beware that methods can be overloaded on name, so we can't just do a name match. 
        // Nop if method is not generic. 
        private static MethodInfo Resolve(Type type, MethodInfo method)
        {
            foreach (var candidate in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
            {
                if (candidate.MetadataToken == method.MetadataToken)
                {
                    return candidate;                    
                }
            }
            return null;
        }

        // Name can only map to a single type. If try to map to difference types, then it's a failed match. 
        private static bool AddGenericArg(Dictionary<string, Type> genericArgs, string name, Type type)
        {
            Type typeExisting;
            if (genericArgs.TryGetValue(name, out typeExisting))
            {
                return typeExisting == type;
            }
            genericArgs[name] = type;
            return true;
        }

        // Return truen if the types are compatible. 
        // If openType has generic args, then add a [Name,Type] entry to the genericArgs dictionary. 
        private static bool CheckArg(Type openType, Type specificType, Dictionary<string, Type> genericArgs)
        {
            if (openType == specificType)
            {
                return true;
            }
            // Is it a generic match?
            // T, string
            if (openType.IsGenericParameter)
            {
                string name = openType.Name;
                return AddGenericArg(genericArgs, name, specificType);
            }

            // IFoo<T>, IFoo<string> 
            if (openType.IsGenericType)
            {
                if (specificType.GetGenericTypeDefinition() != openType.GetGenericTypeDefinition())
                {
                    return false;
                }

                var typeArgs = openType.GetGenericArguments();
                var specificTypeArgs = specificType.GetGenericArguments();

                int len = typeArgs.Length;

                for (int i = 0; i < len; i++)
                {
                    if (!AddGenericArg(genericArgs, typeArgs[i].Name, specificTypeArgs[i]))
                    {
                        return false;
                    }
                }
                return true;
            }

            return false;
        }

        // Find the "Convert" method on the instance 
        private static Func<object, object> GetConverter(object instance, Type typeSource, Type typeDest)
        {
            Type t = instance.GetType();
            var allMethods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var method in allMethods)
            {
                if (method.Name.StartsWith("Convert", StringComparison.OrdinalIgnoreCase))
                {
                    if (method.ReturnType == typeDest)
                    {
                        var parameters = method.GetParameters();
                        if (parameters.Length == 1 && parameters[0].ParameterType == typeSource)
                        {
                            // Type mismatch 
                            // var func = (Func<object, object>) method.CreateDelegate(typeof(Func<object, object>));
                            Func<object, object> func = (input) =>
                            {
                                return method.Invoke(instance, new object[] { input });
                            };
                            return func;
                        }
                    }
                }
            }
            throw new InvalidOperationException("No Convert method on type " + t.Name + " to convert from " +
                typeSource.Name + " to " + typeDest.Name);
        }        
    }
}
