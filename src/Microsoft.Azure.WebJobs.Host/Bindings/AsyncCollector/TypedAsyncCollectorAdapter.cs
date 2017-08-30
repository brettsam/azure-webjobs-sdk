// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Host.Bindings
{
    // IAsyncCollector<TSrc> --> IAsyncCollector<TDest>
    internal class TypedAsyncCollectorAdapter<TSrc, TDest, TAttribute> : IAsyncCollector<TSrc>
        where TAttribute : Attribute
    {
        private const string CategoryPrefix = "Host.Bindings";
        private readonly IAsyncCollector<TDest> _inner;
        private readonly FuncConverter<TSrc, TAttribute, TDest> _convert;
        private readonly TAttribute _attrResolved;
        private readonly ValueBindingContext _context;
        private readonly ILogger _logger;

        public TypedAsyncCollectorAdapter(
            IAsyncCollector<TDest> inner,
            FuncConverter<TSrc, TAttribute, TDest> convert,
            TAttribute attrResolved,
            ValueBindingContext context,
            ILoggerFactory loggerFactory)
        {
            if (convert == null)
            {
                throw new ArgumentNullException("convert");
            }

            _inner = inner;
            _convert = convert;
            _attrResolved = attrResolved;
            _context = context;

            _logger = loggerFactory.CreateLogger(GetCategoryName());
        }

        private static string GetCategoryName()
        {
            string bindingName = typeof(TAttribute).Name;
            string attributeString = nameof(Attribute);
            if (bindingName.EndsWith(attributeString, StringComparison.Ordinal))
            {
                int index = bindingName.LastIndexOf(attributeString, StringComparison.Ordinal);
                bindingName = bindingName.Remove(index);
            }

            return $"{CategoryPrefix}.{bindingName}";
        }

        public async Task AddAsync(TSrc item, CancellationToken cancellationToken = default(CancellationToken))
        {
            TDest x = _convert(item, _attrResolved, _context);
            using (_logger.BeginLogLevelScope(LogLevel.Debug))
            {
                await _inner.AddAsync(x, cancellationToken);
            }
        }

        public async Task FlushAsync(CancellationToken cancellationToken)
        {
            using (_logger.BeginLogLevelScope(LogLevel.Debug))
            {
                await _inner.FlushAsync(cancellationToken);
            }
        }
    }
}