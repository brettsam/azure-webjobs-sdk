// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Temporary class to provide methods removed during the DI work
    /// </summary>
    public static class TempJobHostOptionsExtensions
    {
        public static void AddExtension(this JobHostOptions options, object service) => throw new NotSupportedException("Using removed/unsupported API");

        public static void UseServiceBus(this JobHostOptions options) => throw new NotSupportedException("Using removed/unsupported API");

        public static void UseServiceBus(this JobHostOptions options, object o) => throw new NotSupportedException("Using removed/unsupported API");
    }
}
