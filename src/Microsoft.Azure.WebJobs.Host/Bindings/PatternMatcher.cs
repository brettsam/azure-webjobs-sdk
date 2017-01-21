// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Bindings
{    
    // Find a Convert() method on a class that matches the type parameters. 
    internal abstract class PatternMatcher
    {
        public static PatternMatcher New(Type typeBuilder, params object[] constructorArgs)
        {
            return new CreateViaType(typeBuilder, constructorArgs);
        }

        public static PatternMatcher New(object instance)
        {
            return new CreateViaInstance(instance);
        }

        // Attempt to get a matcher for the given types. 
        // Return null if can't match. 
        public abstract Func<object, object> TryGetConverterFunc(Type typeSource, Type typeDest);

        // Find a Convert* method that is compatible with the given source and destiation types. 
        // Signature will be like:
        //    TIn Convert*(TOut) 
        // Where TIn, TOut may be generic. This will infer the generics and return a Method 
        // for the correct convert function and on the properly resolved generic type. 
        public static MethodInfo FindConverterMethod(Type typeConverter, Type typeSource, Type typeDest)
        {
            var allMethods = typeConverter.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            foreach (var method in allMethods)
            {
                if (!method.Name.StartsWith("Convert", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Dictionary<string, Type> genericArgs = new Dictionary<string, Type>();

                var retType = TypeUtility.UnwrapTaskType(method.ReturnType);
                if (!CheckArg(retType, typeDest, genericArgs))
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
                    var resolvedMethod = ResolveMethod(finalType, method);
                    return resolvedMethod;
                }
                return method;
            }

            throw new InvalidOperationException("No Convert method on type " + typeConverter.Name + " to convert from " +
                typeSource.Name + " to " + typeDest.Name);
        }

        // Create an instance for method.DeclaringType, passing constructorArgs.
        public static Func<object, object> CreateInstanceAndGetConverterFunc(
            object[] constructorArgs, 
            MethodInfo method)
        {
            // typeConverter may have open generic types.  method has now resolved those types. 
            var declaringType = method.DeclaringType;

            object converterInstance = null;
            if (!method.IsStatic)
            {
                try
                {
                    // common for constructor to throw validation errors.          
                    converterInstance = Activator.CreateInstance(declaringType, constructorArgs);
                }
                catch (TargetInvocationException e)
                {
                    throw e.InnerException;
                }
            }

            return PatternMatcher.GetConverterFunc(converterInstance, method);
        }

        // Once we've selected a converter Method and instance to call it on, get a delegate for the method. 
        public static Func<object, object> GetConverterFunc(object instance, MethodInfo method)
        {
            // It shouldn't be just Task since we should be returning something
            // Expect Task<T>.
            if (typeof(Task).IsAssignableFrom(method.ReturnType))
            {
                var t = TypeUtility.UnwrapTaskType(method.ReturnType);
                var wrapper = typeof(GetAsyncConverterFunc<>).MakeGenericType(t).GetMethod("Work", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                var converter = (Func<object, object>)wrapper.Invoke(null, new object[] { instance, method });
                return converter;
            }
            else
            {
                // Non-task
                Func<object, object> converter = (input) =>
                {
                    var result = method.Invoke(instance, new object[] { input });
                    return result;
                };
                return converter;
            }
        }

        // Given a method info on the open generic; resolve it on a closed generic. 
        // Beware that methods can be overloaded on name, so we can't just do a name match. 
        // Nop if method is not generic. 
        private static MethodInfo ResolveMethod(Type type, MethodInfo method)
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

        // Helper for invoking a methodInfo that returns Task<T>
        private class GetAsyncConverterFunc<T>
        {
            private static Func<object, object> Work(object instance, MethodInfo method)
            {
                Func<object, object> converter = (input) =>
                {
                    Task<T> resultTask = Task.Run(() => (Task<T>)method.Invoke(instance, new object[] { input }));

                    T result = resultTask.GetAwaiter().GetResult();
                    return result;
                };
                return converter;
            }
        }

        // Wrapper for matching against a static type. 
        private class CreateViaType : PatternMatcher
        {
            private readonly Type _builderType;
            private readonly object[] _constructorArgs;

            public CreateViaType(Type builderType, object[] constructorArgs)
            {
                _builderType = builderType;
                _constructorArgs = constructorArgs;
            }

            public override Func<object, object> TryGetConverterFunc(Type typeSource, Type typeDest)
            {
                var method = PatternMatcher.FindConverterMethod(_builderType, typeSource, typeDest);
                if (method == null)
                {
                    return null;
                }
                var converter = PatternMatcher.CreateInstanceAndGetConverterFunc(_constructorArgs, method);
                return converter;
            }
        }

        // Wrapper for matching against an instance. 
        private class CreateViaInstance : PatternMatcher
        {
            private readonly object _instance;

            public CreateViaInstance(object instance)
            {
                _instance = instance;
            }

            public override Func<object, object> TryGetConverterFunc(Type typeSource, Type typeDest)
            {
                var typeConverter = _instance.GetType();

                var method = PatternMatcher.FindConverterMethod(typeConverter, typeSource, typeDest);
                if (method == null)
                {
                    return null;
                }
                return PatternMatcher.GetConverterFunc(_instance, method);
            }
        }
    }
}
