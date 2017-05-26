// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Logging
{
    /// <summary>
    /// Constants for logging events.
    /// </summary>    
    public static class LogEvents
    {
        /// <summary>
        /// The event id for a dependency event (1).
        /// </summary>
        public const int Dependency = 1;

        /// <summary>
        /// The event id for a metric event (2).
        /// </summary>
        public const int Metric = 2;
    }
}
