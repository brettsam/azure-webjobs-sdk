// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Extensions.Storage
{
    internal class ResponseListener : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object>>, IDisposable
    {
        private readonly ConcurrentDictionary<string, HttpContent> _responseContents = new ConcurrentDictionary<string, HttpContent>();
        private readonly IDisposable _listenerSubscription;
        private readonly ILogger<ResponseListener> _logger;

        public ResponseListener(ILogger<ResponseListener> logger)
        {
            _listenerSubscription = DiagnosticListener.AllListeners.Subscribe(this);
            _logger = logger;
        }

        public void OnNext(DiagnosticListener value)
        {
            if (value.Name == "HttpHandlerDiagnosticListener")
            {
                value.Subscribe(this);
            }
        }

        public async Task<T> TrackRequestAsync<T>(string operationName, string clientRequestId, Func<CancellationToken, Task<T>> storageOperation)
        {
            _responseContents[clientRequestId] = null;
            T result = default;

            using (ResponseTracker tracker = new ResponseTracker(this, clientRequestId))
            {
                using (var cts = new CancellationTokenSource())
                {
                    Task<T> storageOperationTask = storageOperation(cts.Token);

                    Task delayTask = Task.Delay(TimeSpan.FromMinutes(2), cts.Token);

                    Task firstCompleted = await Task.WhenAny(delayTask, storageOperationTask);

                    if (Equals(firstCompleted, delayTask))
                    {
                        _logger.LogDebug("A storage operation for '{operationName}' appears to have deadlocked. Disposing response for request '{clientRequestId}'.",
                            operationName, clientRequestId);

                        cts.Cancel();
                        tracker.DisposeResponse(clientRequestId);

                        try
                        {
                            result = await storageOperationTask;
                        }
                        catch (Exception)
                        {
                        }

                        _logger.LogDebug("Request '{clientRequestId}' has been disposed.", clientRequestId);

                        return result;
                    }

                    cts.Cancel();
                    result = await storageOperationTask;

                    return result;
                }
            }
        }

        public void OnNext(KeyValuePair<string, object> value)
        {
            if (value.Key == "System.Net.Http.HttpRequestOut.Stop")
            {
                // The value of this event is an anonymous type with the property "Response".
                PropertyInfo prop = value.Value?.GetType().GetProperty("Response");

                if (prop?.GetValue(value.Value) is HttpResponseMessage response &&
                    response.RequestMessage.Headers.TryGetValues("x-ms-client-request-id", out IEnumerable<string> headerValues))
                {
                    string clientRequestId = headerValues.SingleOrDefault();

                    if (!string.IsNullOrEmpty(clientRequestId) && _responseContents.Keys.Contains(clientRequestId))
                    {
                        _responseContents[clientRequestId] = response.Content;
                    }
                }
            }
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }


        public void Dispose()
        {
            _listenerSubscription?.Dispose();
        }

        private class ResponseTracker : IDisposable
        {
            private readonly string _clientRequestId;
            private readonly ResponseListener _parent;

            public ResponseTracker(ResponseListener parent, string clientRequestId)
            {
                _clientRequestId = clientRequestId;
                _parent = parent;
            }

            public void DisposeResponse(string clientRequestId)
            {
                if (_parent._responseContents.TryGetValue(clientRequestId, out HttpContent content))
                {
                    _parent._logger.LogDebug("Disposing of response for '{clientRequestId}'.");
                    content.Dispose();
                }
                else
                {
                    _parent._logger.LogDebug("Could not find response for '{clientRequestId}' in the cache.");
                }
            }

            public void Dispose()
            {
                if (!_parent._responseContents.TryRemove(_clientRequestId, out _))
                {
                    _parent._logger.LogDebug("Unable to remove response '{clientRequestId}' from the cache. Current cache size is '{cacheSize}'.",
                        _clientRequestId, _parent._responseContents.Count);
                }
            }
        }
    }
}
