// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.﻿

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// The state of the JobHost.
    /// </summary>
    public enum JobHostState
    {
        /// <summary>
        /// The JobHost has not started.
        /// </summary>
        NotStarted = 0,

        /// <summary>
        /// The JobHost is starting.
        /// </summary>
        Starting = 1,
        
        /// <summary>
        /// The JobHost has started.
        /// </summary>
        Started = 2,
        
        /// <summary>
        /// The JobHost is stopping or stopped.
        /// </summary>
        StoppingOrStopped = 3
    }
}
