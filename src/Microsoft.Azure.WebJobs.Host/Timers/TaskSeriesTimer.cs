// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host.Timers
{
    /// <summary>Represents a timer that executes one task after another in a series.</summary>
    internal sealed class TaskSeriesTimer : ITaskSeriesTimer
    {
        private readonly ITaskSeriesCommand _command;
        private readonly IWebJobsExceptionHandler _exceptionHandler;
        private readonly Task _initialWait;
        private readonly CancellationTokenSource _cancellationTokenSource;

        private bool _started;
        private bool _stopped;
        private Task _run;
        private bool _disposed;
        private TaskFactory _taskFactory;

        public TaskSeriesTimer(ITaskSeriesCommand command, IWebJobsExceptionHandler exceptionHandler,
            Task initialWait, TaskFactory taskFactory = null)
        {
            if (command == null)
            {
                throw new ArgumentNullException("command");
            }

            if (exceptionHandler == null)
            {
                throw new ArgumentNullException("exceptionHandler");
            }

            if (initialWait == null)
            {
                throw new ArgumentNullException("initialWait");
            }

            _command = command;
            _exceptionHandler = exceptionHandler;
            _initialWait = initialWait;
            _cancellationTokenSource = new CancellationTokenSource();

            _taskFactory = taskFactory ?? new TaskFactory();
        }

        public void Start()
        {
            ThrowIfDisposed();

            if (_started)
            {
                throw new InvalidOperationException("The timer has already been started; it cannot be restarted.");
            }

            _run = _taskFactory.StartNew(() => RunAsync(_cancellationTokenSource.Token)).Unwrap();
            _started = true;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (!_started)
            {
                throw new InvalidOperationException("The timer has not yet been started.");
            }

            if (_stopped)
            {
                throw new InvalidOperationException("The timer has already been stopped.");
            }

            _cancellationTokenSource.Cancel();
            return StopAsyncCore(cancellationToken);
        }

        private async Task StopAsyncCore(CancellationToken cancellationToken)
        {
            await Task.Delay(0);
            TaskCompletionSource<object> cancellationTaskSource = new TaskCompletionSource<object>();

            using (cancellationToken.Register(() => cancellationTaskSource.SetCanceled()))
            {
                // Wait for all pending command tasks to complete (or cancellation of the token) before returning.
                await Task.WhenAny(_run, cancellationTaskSource.Task);
            }

            _stopped = true;
        }

        public void Cancel()
        {
            ThrowIfDisposed();
            _cancellationTokenSource.Cancel();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // Running callers might still be using the cancellation token.
                // Mark it canceled but don't dispose of the source while the callers are running.
                // Otherwise, callers would receive ObjectDisposedException when calling token.Register.
                // For now, rely on finalization to clean up _cancellationTokenSource's wait handle (if allocated).
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();

                _disposed = true;
            }
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Allow Start to return immediately without waiting for any initial iteration work to start.
                await Task.Yield();

                Task wait = _initialWait;

                // Execute tasks one at a time (in a series) until stopped.
                while (!cancellationToken.IsCancellationRequested)
                {
                    TaskCompletionSource<object> cancellationTaskSource = new TaskCompletionSource<object>();

                    using (cancellationToken.Register(() => cancellationTaskSource.SetCanceled()))
                    {
                        try
                        {
                            Stopwatch sw = new Stopwatch();
                            if (_command is SingletonManager.RenewLeaseCommand)
                            {
                                sw.Start();
                            }

                            await _taskFactory.ContinueWhenAny(new[] { wait, cancellationTaskSource.Task }, (t) =>
                            {
                                if (_command is SingletonManager.RenewLeaseCommand)
                                {
                                    sw.Stop();
                                    if (sw.ElapsedMilliseconds > 10000)
                                    {
                                        bool match = wait == t;
                                    }
                                }
                            }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Current);
                        }
                        catch (OperationCanceledException)
                        {
                            // When Stop fires, don't make it wait for wait before it can return.
                        }
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    try
                    {
                        TaskSeriesCommandResult result = await _command.ExecuteAsync(cancellationToken);
                        wait = result.Wait;
                    }
                    catch (OperationCanceledException)
                    {
                        // Don't fail the task, throw a background exception, or stop looping when a task cancels.
                    }
                }
            }
            catch (Exception exception)
            {
                // Immediately report any unhandled exception from this background task.
                // (Don't capture the exception as a fault of this Task; that would delay any exception reporting until
                // Stop is called, which might never happen.)
                _exceptionHandler.OnUnhandledExceptionAsync(ExceptionDispatchInfo.Capture(exception)).GetAwaiter().GetResult();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(null);
            }
        }
    }
}
