// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Host.Bindings
{
    /// <summary>
    /// Placeholder to use with converter manager for describing open types.     
    /// </summary>
    public abstract class OpenType
    {
        /// <summary>
        /// Return true if and only if given type matches. 
        /// </summary>
        /// <param name="type">Type to check</param>
        /// <returns></returns>
        public abstract bool IsMatch(Type type);
    }
}
