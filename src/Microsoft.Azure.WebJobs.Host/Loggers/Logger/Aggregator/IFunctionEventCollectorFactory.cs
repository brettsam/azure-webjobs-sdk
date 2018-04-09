// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Loggers;

namespace Microsoft.Azure.WebJobs.Logging
{
    public interface IFunctionEventCollectorFactory
    {
        IAsyncCollector<FunctionInstanceLogEntry> Create();
    }
}
