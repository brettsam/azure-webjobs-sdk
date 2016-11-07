// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Host
{
    internal static class JobHostDispatcher
    {
        private static SingleThreadSynchronizationContext _context;

        static JobHostDispatcher()
        {
            _context = new SingleThreadSynchronizationContext();
            _context.Run();
        }

        public static void BeginInvoke(Func<Task> action)
        {
            SynchronizationContext.SetSynchronizationContext(_context);
            var scheduler = TaskScheduler.FromCurrentSynchronizationContext();
            Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, scheduler);
        }

        public static void BeginInvoke(Action action)
        {
            SynchronizationContext.SetSynchronizationContext(_context);
            var scheduler = TaskScheduler.FromCurrentSynchronizationContext();
            Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.None, scheduler);
        }

        private sealed class SingleThreadSynchronizationContext : SynchronizationContext, IDisposable
        {
            private readonly BlockingCollection<Tuple<SendOrPostCallback, object>> _workItems = new BlockingCollection<Tuple<SendOrPostCallback, object>>();

            public override void Post(SendOrPostCallback d, object state)
            {
                _workItems.Add(new Tuple<SendOrPostCallback, object>(d, state));
            }

            public override void Send(SendOrPostCallback d, object state)
            {
                throw new NotSupportedException();
            }

            public void Run()
            {
                Thread t = new Thread(() =>
                {
                    Tuple<SendOrPostCallback, object> workItem;
                    while (_workItems.TryTake(out workItem, Timeout.Infinite))
                    {
                        workItem.Item1(workItem.Item2);
                    }
                });
                t.Priority = ThreadPriority.Highest;
                t.Start();
            }

            public void Complete()
            {
                _workItems.CompleteAdding();
            }

            public void Dispose()
            {
                Complete();
                _workItems.Dispose();
            }
        }
    }
}
