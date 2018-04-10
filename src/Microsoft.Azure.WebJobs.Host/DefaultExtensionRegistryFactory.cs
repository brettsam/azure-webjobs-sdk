// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Host
{
    internal class DefaultExtensionRegistryFactory : IExtensionRegistryFactory
    {
        private readonly IEnumerable<IExtensionConfigProvider> _registeredExtensions;
        private readonly IConverterManager _converterManager;
        private readonly IWebHookProvider _webHookProvider;
        private readonly JobHostOptions _jobHostOptions;


        public DefaultExtensionRegistryFactory(IEnumerable<IExtensionConfigProvider> registeredExtensions, IConverterManager converterManager,
             IOptions<JobHostOptions> jobHostOptions, IWebHookProvider webHookProvider = null)
        {
            _registeredExtensions = registeredExtensions;
            _converterManager = converterManager;
            _webHookProvider = webHookProvider;
            _jobHostOptions = jobHostOptions.Value;
        }

        public IExtensionRegistry Create()
        {
            IExtensionRegistry registry = new DefaultExtensionRegistry();

            ExtensionConfigContext context = new ExtensionConfigContext(_converterManager, _webHookProvider, registry)
            {
                Config = _jobHostOptions
            };

            foreach (IExtensionConfigProvider extension in _registeredExtensions)
            {
                registry.RegisterExtension<IExtensionConfigProvider>(extension);
                context.Current = extension;
                extension.Initialize(context);
            }

            context.ApplyRules();

            return registry;
        }
    }
}
