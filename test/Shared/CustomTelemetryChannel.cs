//-----------------------------------------------------------------------------------
// <copyright file='CustomTelemetryChannel.cs' company='Microsoft Corporation'>
//     Copyright (c) Microsoft Corporation. All Rights Reserved.
// </copyright>
//-----------------------------------------------------------------------------------

namespace Microsoft.ApplicationInsights
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.DataContracts;

    internal sealed class CustomTelemetryChannel : ITelemetryChannel
    {
        private readonly EventWaitHandle waitHandle;

        private readonly object mutex = new();

        public CustomTelemetryChannel()
        {
            this.waitHandle = new AutoResetEvent(false);
#if NET462 || NET472 || NET48
            this.SentItems = new ITelemetry[0];
#else
            this.SentItems = [];
#endif
        }

        public bool? DeveloperMode { get; set; }

        public string EndpointAddress { get; set; } = "https://example.com/telemetry/";

        public ITelemetry[] SentItems { get; private set; }

        public void Send(ITelemetry item)
        {
            lock (this.mutex)
            {
                ITelemetry[] current = this.SentItems;
                List<ITelemetry> temp = [.. current, item];
                this.SentItems = [.. temp];
                this.waitHandle.Set();
            }
        }

        public Task<int?> WaitForItemsCaptured(TimeSpan timeout)
        {
            // Pattern for Wait Handles from: https://msdn.microsoft.com/en-us/library/hh873178%28v=vs.110%29.aspx#WaitHandles
            var tcs = new TaskCompletionSource<int?>();

            var rwh = ThreadPool.RegisterWaitForSingleObject(
                this.waitHandle, 
                (state, timedOut) =>
                {
                    if (timedOut)
                    {
                        tcs.SetResult(null);
                    }
                    else
                    {
                        lock (this.mutex)
                        {
                            tcs.SetResult(this.SentItems.Length);
                        }
                    }
                }, 
                state: null, 
                millisecondsTimeOutInterval: Convert.ToUInt32(timeout.TotalMilliseconds), 
                executeOnlyOnce: true);

            var t = tcs.Task;
            t.ContinueWith((previousTask) => rwh.Unregister(null));
            return t;
        }

        public void Flush()
        {
            throw new Exception("Flush called");
        }

        public void Dispose()
        {
        }

        public CustomTelemetryChannel Reset()
        {
            lock (this.mutex)
            {
#if NET452
                this.SentItems = new ITelemetry[0];
#else
                this.SentItems = [];
#endif
            }

            return this;
        }
    }
}
