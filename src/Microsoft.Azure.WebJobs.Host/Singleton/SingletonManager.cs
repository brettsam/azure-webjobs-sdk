// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings.Path;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Azure.WebJobs.Host.Storage;
using Microsoft.Azure.WebJobs.Host.Storage.Blob;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Azure.WebJobs.Host
{
    /// <summary>
    /// Encapsulates and manages blob leases for Singleton locks.
    /// </summary>
    internal class SingletonManager
    {
        internal const string FunctionInstanceMetadataKey = "FunctionInstance";
        private readonly INameResolver _nameResolver;
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        private readonly IWebJobsExceptionHandler _exceptionHandler;
        private readonly SingletonConfiguration _config;
        private readonly IStorageAccountProvider _accountProvider;
        private ConcurrentDictionary<string, IStorageBlobDirectory> _lockDirectoryMap = new ConcurrentDictionary<string, IStorageBlobDirectory>(StringComparer.OrdinalIgnoreCase);
        private TimeSpan _minimumLeaseRenewalInterval = TimeSpan.FromSeconds(1);
        private TraceWriter _trace;
        private IHostIdProvider _hostIdProvider;
        private string _hostId;

        // For mock testing only
        internal SingletonManager()
        {
        }

        public SingletonManager(IStorageAccountProvider accountProvider, IWebJobsExceptionHandler exceptionHandler, SingletonConfiguration config, TraceWriter trace, IHostIdProvider hostIdProvider, INameResolver nameResolver = null)
        {
            _accountProvider = accountProvider;
            _nameResolver = nameResolver;
            _exceptionHandler = exceptionHandler;
            _config = config;
            _trace = trace;
            _hostIdProvider = hostIdProvider;
        }

        internal virtual SingletonConfiguration Config
        {
            get
            {
                return _config;
            }
        }

        internal string HostId
        {
            get
            {
                if (_hostId == null)
                {
                    _hostId = _hostIdProvider.GetHostIdAsync(CancellationToken.None).Result;
                }
                return _hostId;
            }
        }

        // for testing
        internal TimeSpan MinimumLeaseRenewalInterval
        {
            get
            {
                return _minimumLeaseRenewalInterval;
            }
            set
            {
                _minimumLeaseRenewalInterval = value;
            }
        }

        public async virtual Task<object> LockAsync(string lockId, string functionInstanceId, SingletonAttribute attribute, CancellationToken cancellationToken)
        {
            object lockHandle = await TryLockAsync(lockId, functionInstanceId, attribute, cancellationToken);

            if (lockHandle == null)
            {
                TimeSpan acquisitionTimeout = attribute.LockAcquisitionTimeout != null
                    ? TimeSpan.FromSeconds(attribute.LockAcquisitionTimeout.Value) :
                    _config.LockAcquisitionTimeout;
                throw new TimeoutException(string.Format("Unable to acquire singleton lock blob lease for blob '{0}' (timeout of {1} exceeded).", lockId, acquisitionTimeout.ToString("g")));
            }

            return lockHandle;
        }

        public async virtual Task<object> TryLockAsync(string lockId, string functionInstanceId, SingletonAttribute attribute, CancellationToken cancellationToken, bool retry = true)
        {
            _trace.Verbose(string.Format(CultureInfo.InvariantCulture, "Waiting for Singleton lock ({0})", lockId), source: TraceSource.Execution);

            IStorageBlobDirectory lockDirectory = GetLockDirectory(attribute.Account);
            IStorageBlockBlob lockBlob = lockDirectory.GetBlockBlobReference(lockId);
            TimeSpan lockPeriod = GetLockPeriod(attribute, _config);
            string leaseId = await TryAcquireLeaseAsync(lockBlob, lockPeriod, cancellationToken);
            if (string.IsNullOrEmpty(leaseId) && retry)
            {
                // Someone else has the lease. Continue trying to periodically get the lease for
                // a period of time
                TimeSpan acquisitionTimeout = attribute.LockAcquisitionTimeout != null
                    ? TimeSpan.FromSeconds(attribute.LockAcquisitionTimeout.Value) :
                    _config.LockAcquisitionTimeout;

                TimeSpan timeWaited = TimeSpan.Zero;
                while (string.IsNullOrEmpty(leaseId) && (timeWaited < acquisitionTimeout))
                {
                    await Task.Delay(_config.LockAcquisitionPollingInterval);
                    timeWaited += _config.LockAcquisitionPollingInterval;
                    leaseId = await TryAcquireLeaseAsync(lockBlob, lockPeriod, cancellationToken);
                }
            }

            if (string.IsNullOrEmpty(leaseId))
            {
                _trace.Verbose(string.Format(CultureInfo.InvariantCulture, "Unable to acquire Singleton lock ({0}).", lockId), source: TraceSource.Execution);
                return null;
            }

            _trace.Verbose(string.Format(CultureInfo.InvariantCulture, "Singleton lock acquired ({0})", lockId), source: TraceSource.Execution);

            if (!string.IsNullOrEmpty(functionInstanceId))
            {
                await WriteLeaseBlobMetadata(lockBlob, leaseId, functionInstanceId, cancellationToken);
            }

            SingletonLockHandle lockHandle = new SingletonLockHandle
            {
                LeaseId = leaseId,
                LockId = lockId,
                Blob = lockBlob
            };

            TimeSpan normalUpdateInterval = new TimeSpan(lockPeriod.Ticks / 2);
            IDelayStrategy speedupStrategy = new LinearSpeedupStrategy(normalUpdateInterval, MinimumLeaseRenewalInterval);
            new Thread(() =>
            {
                System.Timers.Timer timer = new System.Timers.Timer();
                timer.Interval = normalUpdateInterval.TotalMilliseconds;
                timer.AutoReset = false;
                timer.Elapsed += (sender, e) =>
                {
                    int a, b;
                    ThreadPool.GetAvailableThreads(out a, out b);
                    _trace.Verbose("----------");
                    _trace.Verbose(string.Format("Id: {0} ThreadPool: {1} {2} {3}", Thread.CurrentThread.ManagedThreadId, Thread.CurrentThread.IsThreadPoolThread, a, b));
                    _trace.Verbose("Timer    " + DateTime.Now.ToString("HH:mm:ss.ffff"));
                    Timer_Elapsed(sender, lockBlob, leaseId, lockId, speedupStrategy);
                    _trace.Verbose("----------");
                };

                _trace.Verbose(string.Format("Id: {0} ThreadPool: {1}", Thread.CurrentThread.ManagedThreadId, Thread.CurrentThread.IsThreadPoolThread));
                //Synchronized sync = new Synchronized();
                //timer.SynchronizingObject = sync;
                timer.Start();
            }).Start();

            return lockHandle;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "ThreadPool")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1305:SpecifyIFormatProvider", MessageId = "System.DateTime.ToString(System.String)")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1305:SpecifyIFormatProvider", MessageId = "System.String.Format(System.String,System.Object,System.Object)")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "lockId")]
        private void Timer_Elapsed(object sender, IStorageBlockBlob leaseBlob, string leaseId, string lockId, IDelayStrategy speedupStrategy)
        {
            TimeSpan delay = TimeSpan.MinValue;
            try
            {
                AccessCondition condition = new AccessCondition
                {
                    LeaseId = leaseId
                };
                _trace.Verbose(string.Format("Id: {0} ThreadPool: {1}", Thread.CurrentThread.ManagedThreadId, Thread.CurrentThread.IsThreadPoolThread));
                _trace.Verbose(string.Format(CultureInfo.InvariantCulture, "Renewing {0}", DateTime.Now.ToString("HH:mm:ss.ffff")));
                leaseBlob.RenewLeaseAsync(condition, null, null, CancellationToken.None).GetAwaiter().GetResult();
                _trace.Verbose(string.Format("Id: {0} ThreadPool: {1}", Thread.CurrentThread.ManagedThreadId, Thread.CurrentThread.IsThreadPoolThread));
                _trace.Verbose(string.Format(CultureInfo.InvariantCulture, "Renewed  {0}", DateTime.Now.ToString("HH:mm:ss.ffff")));

                // The next execution should occur after a normal delay.
                delay = speedupStrategy.GetNextDelay(executionSucceeded: true);
            }
            catch (StorageException exception)
            {
                if (exception.IsServerSideError())
                {
                    // The next execution should occur more quickly (try to renew the lease before it expires).
                    delay = speedupStrategy.GetNextDelay(executionSucceeded: false);
                }
                else
                {
                    // If we've lost the lease or cannot restablish it, we want to fail any
                    // in progress function execution
                    throw;
                }
            }

            var timer = (System.Timers.Timer)sender;
            timer.Interval = delay.TotalMilliseconds;
            timer.Start();
        }

        public async virtual Task ReleaseLockAsync(object lockHandle, CancellationToken cancellationToken)
        {
            SingletonLockHandle singletonLockHandle = (SingletonLockHandle)lockHandle;

            if (singletonLockHandle.LeaseRenewalTimer != null)
            {
                await singletonLockHandle.LeaseRenewalTimer.StopAsync(cancellationToken);
            }

            await ReleaseLeaseAsync(singletonLockHandle.Blob, singletonLockHandle.LeaseId, cancellationToken);

            _trace.Verbose(string.Format(CultureInfo.InvariantCulture, "Singleton lock released ({0})", singletonLockHandle.LockId), source: TraceSource.Execution);
        }

        public string FormatLockId(MethodInfo method, SingletonScope scope, string scopeId)
        {
            return FormatLockId(method, scope, HostId, scopeId);
        }

        public static string FormatLockId(MethodInfo method, SingletonScope scope, string hostId, string scopeId)
        {
            if (string.IsNullOrEmpty(hostId))
            {
                throw new ArgumentNullException("hostId");
            }

            string lockId = string.Empty;
            if (scope == SingletonScope.Function)
            {
                lockId += string.Format(CultureInfo.InvariantCulture, "{0}.{1}", method.DeclaringType.FullName, method.Name);
            }

            if (!string.IsNullOrEmpty(scopeId))
            {
                if (!string.IsNullOrEmpty(lockId))
                {
                    lockId += ".";
                }
                lockId += scopeId;
            }

            lockId = string.Format(CultureInfo.InvariantCulture, "{0}/{1}", hostId, lockId);

            return lockId;
        }

        public string GetBoundScopeId(string scopeId, IReadOnlyDictionary<string, object> bindingData = null)
        {
            if (_nameResolver != null)
            {
                scopeId = _nameResolver.ResolveWholeString(scopeId);
            }

            if (bindingData != null)
            {
                BindingTemplate bindingTemplate = BindingTemplate.FromString(scopeId);
                IReadOnlyDictionary<string, string> parameters = BindingDataPathHelper.ConvertParameters(bindingData);
                return bindingTemplate.Bind(parameters);
            }
            else
            {
                return scopeId;
            }
        }

        public static SingletonAttribute GetFunctionSingletonOrNull(MethodInfo method, bool isTriggered)
        {
            if (!isTriggered &&
                method.GetCustomAttributes<SingletonAttribute>().Any(p => p.Mode == SingletonMode.Listener))
            {
                throw new NotSupportedException("SingletonAttribute using mode 'Listener' cannot be applied to non-triggered functions.");
            }

            SingletonAttribute[] singletonAttributes = method.GetCustomAttributes<SingletonAttribute>().Where(p => p.Mode == SingletonMode.Function).ToArray();
            SingletonAttribute singletonAttribute = null;
            if (singletonAttributes.Length > 1)
            {
                throw new NotSupportedException("Only one SingletonAttribute using mode 'Function' is allowed.");
            }
            else if (singletonAttributes.Length == 1)
            {
                singletonAttribute = singletonAttributes[0];
                ValidateSingletonAttribute(singletonAttribute, SingletonMode.Function);
            }

            return singletonAttribute;
        }

        /// <summary>
        /// Creates and returns singleton listener scoped to the host.
        /// </summary>
        /// <param name="innerListener">The inner listener to wrap.</param>
        /// <param name="scopeId">The scope ID to use.</param>
        /// <returns>The singleton listener.</returns>
        public SingletonListener CreateHostSingletonListener(IListener innerListener, string scopeId)
        {
            SingletonAttribute singletonAttribute = new SingletonAttribute(scopeId, SingletonScope.Host)
            {
                Mode = SingletonMode.Listener
            };
            return new SingletonListener(null, singletonAttribute, this, innerListener);
        }

        public static SingletonAttribute GetListenerSingletonOrNull(Type listenerType, MethodInfo method)
        {
            // First check the method, then the listener class. This allows a method to override an implicit
            // listener singleton.
            SingletonAttribute singletonAttribute = null;
            SingletonAttribute[] singletonAttributes = method.GetCustomAttributes<SingletonAttribute>().Where(p => p.Mode == SingletonMode.Listener).ToArray();
            if (singletonAttributes.Length > 1)
            {
                throw new NotSupportedException("Only one SingletonAttribute using mode 'Listener' is allowed.");
            }
            else if (singletonAttributes.Length == 1)
            {
                singletonAttribute = singletonAttributes[0];
            }
            else
            {
                singletonAttribute = listenerType.GetCustomAttributes<SingletonAttribute>().SingleOrDefault(p => p.Mode == SingletonMode.Listener);
            }

            if (singletonAttribute != null)
            {
                ValidateSingletonAttribute(singletonAttribute, SingletonMode.Listener);
            }

            return singletonAttribute;
        }

        internal static void ValidateSingletonAttribute(SingletonAttribute attribute, SingletonMode mode)
        {
            if (attribute.Scope == SingletonScope.Host && string.IsNullOrEmpty(attribute.ScopeId))
            {
                throw new InvalidOperationException("A ScopeId value must be provided when using scope 'Host'.");
            }

            if (mode == SingletonMode.Listener && attribute.Scope == SingletonScope.Host)
            {
                throw new InvalidOperationException("Scope 'Host' cannot be used when the mode is set to 'Listener'.");
            }
        }

        public async virtual Task<string> GetLockOwnerAsync(SingletonAttribute attribute, string lockId, CancellationToken cancellationToken)
        {
            IStorageBlobDirectory lockDirectory = GetLockDirectory(attribute.Account);
            IStorageBlockBlob lockBlob = lockDirectory.GetBlockBlobReference(lockId);

            await ReadLeaseBlobMetadata(lockBlob, cancellationToken);

            // if the lease is Available, then there is no current owner
            // (any existing owner value is the last owner that held the lease)
            if (lockBlob.Properties.LeaseState == LeaseState.Available &&
                lockBlob.Properties.LeaseStatus == LeaseStatus.Unlocked)
            {
                return null;
            }

            string owner = string.Empty;
            lockBlob.Metadata.TryGetValue(FunctionInstanceMetadataKey, out owner);

            return owner;
        }

        internal IStorageBlobDirectory GetLockDirectory(string accountName)
        {
            if (string.IsNullOrEmpty(accountName))
            {
                accountName = ConnectionStringNames.Storage;
            }

            IStorageBlobDirectory storageDirectory = null;
            if (!_lockDirectoryMap.TryGetValue(accountName, out storageDirectory))
            {
                Task<IStorageAccount> task = _accountProvider.GetAccountAsync(accountName, CancellationToken.None);
                IStorageAccount storageAccount = task.Result;
                IStorageBlobClient blobClient = storageAccount.CreateBlobClient();
                storageDirectory = blobClient.GetContainerReference(HostContainerNames.Hosts)
                                       .GetDirectoryReference(HostDirectoryNames.SingletonLocks);
                _lockDirectoryMap[accountName] = storageDirectory;
            }

            return storageDirectory;
        }

        internal static TimeSpan GetLockPeriod(SingletonAttribute attribute, SingletonConfiguration config)
        {
            return attribute.Mode == SingletonMode.Listener ?
                    config.ListenerLockPeriod : config.LockPeriod;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
        private ITaskSeriesTimer CreateLeaseRenewalTimer(IStorageBlockBlob leaseBlob, string leaseId, string lockId, TimeSpan leasePeriod,
            IWebJobsExceptionHandler exceptionHandler)
        {
            // renew the lease when it is halfway to expiring   
            TimeSpan normalUpdateInterval = new TimeSpan(leasePeriod.Ticks / 2);

            IDelayStrategy speedupStrategy = new LinearSpeedupStrategy(normalUpdateInterval, MinimumLeaseRenewalInterval);
            ITaskSeriesCommand command = new RenewLeaseCommand(leaseBlob, leaseId, lockId, speedupStrategy, _trace);
            return new TaskSeriesTimer(command, exceptionHandler, Task.Delay(normalUpdateInterval), new TaskFactory(new SingleThreadTaskScheduler()));
        }

        private async Task<string> TryAcquireLeaseAsync(IStorageBlockBlob blob, TimeSpan leasePeriod, CancellationToken cancellationToken)
        {
            bool blobDoesNotExist = false;
            try
            {
                // Optimistically try to acquire the lease. The blob may not yet
                // exist. If it doesn't we handle the 404, create it, and retry below
                return await blob.AcquireLeaseAsync(leasePeriod, null, cancellationToken);
            }
            catch (StorageException exception)
            {
                if (exception.RequestInformation != null)
                {
                    if (exception.RequestInformation.HttpStatusCode == 409)
                    {
                        return null;
                    }
                    else if (exception.RequestInformation.HttpStatusCode == 404)
                    {
                        blobDoesNotExist = true;
                    }
                    else
                    {
                        throw;
                    }
                }
                else
                {
                    throw;
                }
            }

            if (blobDoesNotExist)
            {
                await TryCreateAsync(blob, cancellationToken);

                try
                {
                    return await blob.AcquireLeaseAsync(leasePeriod, null, cancellationToken);
                }
                catch (StorageException exception)
                {
                    if (exception.RequestInformation != null &&
                        exception.RequestInformation.HttpStatusCode == 409)
                    {
                        return null;
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            return null;
        }

        private async Task ReleaseLeaseAsync(IStorageBlockBlob blob, string leaseId, CancellationToken cancellationToken)
        {
            try
            {
                // Note that this call returns without throwing if the lease is expired. See the table at:
                // http://msdn.microsoft.com/en-us/library/azure/ee691972.aspx
                await blob.ReleaseLeaseAsync(
                    accessCondition: new AccessCondition { LeaseId = leaseId },
                    options: null,
                    operationContext: null,
                    cancellationToken: cancellationToken);
            }
            catch (StorageException exception)
            {
                if (exception.RequestInformation != null)
                {
                    if (exception.RequestInformation.HttpStatusCode == 404 ||
                        exception.RequestInformation.HttpStatusCode == 409)
                    {
                        // if the blob no longer exists, or there is another lease
                        // now active, there is nothing for us to release so we can
                        // ignore
                    }
                    else
                    {
                        throw;
                    }
                }
                else
                {
                    throw;
                }
            }
        }

        private async Task<bool> TryCreateAsync(IStorageBlockBlob blob, CancellationToken cancellationToken)
        {
            bool isContainerNotFoundException = false;

            try
            {
                await blob.UploadTextAsync(string.Empty, cancellationToken: cancellationToken);
                return true;
            }
            catch (StorageException exception)
            {
                if (exception.RequestInformation != null)
                {
                    if (exception.RequestInformation.HttpStatusCode == 404)
                    {
                        isContainerNotFoundException = true;
                    }
                    else if (exception.RequestInformation.HttpStatusCode == 409 ||
                             exception.RequestInformation.HttpStatusCode == 412)
                    {
                        // The blob already exists, or is leased by someone else
                        return false;
                    }
                    else
                    {
                        throw;
                    }
                }
                else
                {
                    throw;
                }
            }

            Debug.Assert(isContainerNotFoundException);
            await blob.Container.CreateIfNotExistsAsync(cancellationToken);

            try
            {
                await blob.UploadTextAsync(string.Empty, cancellationToken: cancellationToken);
                return true;
            }
            catch (StorageException exception)
            {
                if (exception.RequestInformation != null &&
                    (exception.RequestInformation.HttpStatusCode == 409 || exception.RequestInformation.HttpStatusCode == 412))
                {
                    // The blob already exists, or is leased by someone else
                    return false;
                }
                else
                {
                    throw;
                }
            }
        }

        private async Task WriteLeaseBlobMetadata(IStorageBlockBlob blob, string leaseId, string functionInstanceId, CancellationToken cancellationToken)
        {
            blob.Metadata.Add(FunctionInstanceMetadataKey, functionInstanceId);

            await blob.SetMetadataAsync(
                accessCondition: new AccessCondition { LeaseId = leaseId },
                options: null,
                operationContext: null,
                cancellationToken: cancellationToken);
        }

        private async Task ReadLeaseBlobMetadata(IStorageBlockBlob blob, CancellationToken cancellationToken)
        {
            try
            {
                await blob.FetchAttributesAsync(cancellationToken);
            }
            catch (StorageException exception)
            {
                if (exception.RequestInformation != null &&
                    exception.RequestInformation.HttpStatusCode == 404)
                {
                    // the blob no longer exists
                }
                else
                {
                    throw;
                }
            }
        }

        internal class SingletonLockHandle
        {
            public string LeaseId { get; set; }
            public string LockId { get; set; }
            public IStorageBlockBlob Blob { get; set; }
            public ITaskSeriesTimer LeaseRenewalTimer { get; set; }
        }

        internal class RenewLeaseCommand : ITaskSeriesCommand
        {
            private readonly IStorageBlockBlob _leaseBlob;
            private readonly string _leaseId;
            private readonly string _lockId;
            private readonly IDelayStrategy _speedupStrategy;
            private readonly TraceWriter _trace;

            public RenewLeaseCommand(IStorageBlockBlob leaseBlob, string leaseId, string lockId, IDelayStrategy speedupStrategy, TraceWriter trace)
            {
                _leaseBlob = leaseBlob;
                _leaseId = leaseId;
                _lockId = lockId;
                _speedupStrategy = speedupStrategy;
                _trace = trace;
            }

            public async Task<TaskSeriesCommandResult> ExecuteAsync(CancellationToken cancellationToken)
            {
                TimeSpan delay = TimeSpan.MinValue;

                try
                {
                    AccessCondition condition = new AccessCondition
                    {
                        LeaseId = _leaseId
                    };
                    await _leaseBlob.RenewLeaseAsync(condition, null, null, cancellationToken);
                    _trace.Verbose(string.Format(CultureInfo.InvariantCulture, "Renewed Singleton lock ({0}) -- {1} -- {2}", _lockId, DateTime.Now.ToLongTimeString(), Thread.CurrentThread.ManagedThreadId), source: TraceSource.Execution);

                    // The next execution should occur after a normal delay.
                    delay = _speedupStrategy.GetNextDelay(executionSucceeded: true);
                }
                catch (StorageException exception)
                {
                    if (exception.IsServerSideError())
                    {
                        // The next execution should occur more quickly (try to renew the lease before it expires).
                        delay = _speedupStrategy.GetNextDelay(executionSucceeded: false);
                    }
                    else
                    {
                        // If we've lost the lease or cannot restablish it, we want to fail any
                        // in progress function execution
                        throw;
                    }
                }

                return new TaskSeriesCommandResult(wait: Task.Delay(delay));
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1812:AvoidUninstantiatedInternalClasses")]
        private class Synchronized : ISynchronizeInvoke
        {
            private Thread _thread;
            private BlockingCollection<Tuple<Delegate, object[]>> _delegates = new BlockingCollection<Tuple<Delegate, object[]>>();

            public Synchronized()
            {
                _thread = new Thread(() =>
                {
                    foreach (Tuple<Delegate, object[]> t in _delegates.GetConsumingEnumerable())
                    {
                        t.Item1.DynamicInvoke(t.Item2);
                    }
                });

                _thread.Start();
            }

            public bool InvokeRequired
            {
                get
                {
                    return Thread.CurrentThread.ManagedThreadId != _thread.ManagedThreadId;
                }
            }

            public IAsyncResult BeginInvoke(Delegate method, object[] args)
            {
                Invoke(method, args);
                return null;
            }

            public object EndInvoke(IAsyncResult result)
            {
                throw new NotImplementedException();
            }

            [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1062:Validate arguments of public methods", MessageId = "0")]
            public object Invoke(Delegate method, object[] args)
            {
                _delegates.Add(new Tuple<Delegate, object[]>(method, args));
                return null;
            }
        }

        private class SingleThreadTaskScheduler : TaskScheduler, IDisposable
        {
            private BlockingCollection<Task> _tasks = new BlockingCollection<Task>();
            private List<Thread> _threads;

            public SingleThreadTaskScheduler()
            {
                _threads = Enumerable.Range(0, 10).Select(i =>
                {
                    var thread = new Thread(() =>
                    {
                        foreach (var t in _tasks.GetConsumingEnumerable())
                        {
                            TryExecuteTask(t);
                        }
                    });
                    thread.IsBackground = true;
                    thread.SetApartmentState(ApartmentState.STA);
                    return thread;
                }).ToList();

                _threads.ForEach(t => t.Start());
            }

            public void Dispose()
            {
                if (_tasks != null)
                {
                    _tasks.CompleteAdding();
                    _tasks.Dispose();
                    _tasks = null;
                }
            }

            protected override IEnumerable<Task> GetScheduledTasks()
            {
                return _tasks.ToArray();
            }

            protected override void QueueTask(Task task)
            {
                _tasks.Add(task);
            }

            protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
            {
                if (!taskWasPreviouslyQueued)
                {
                    return TryExecuteTask(task);
                }

                return false;
            }
        }
    }
}
