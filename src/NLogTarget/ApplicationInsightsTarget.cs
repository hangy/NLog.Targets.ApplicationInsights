// -----------------------------------------------------------------------
// <copyright file="ApplicationInsightsTarget.cs" company="Microsoft">
// Copyright (c) Microsoft Corporation. 
// All rights reserved.  2013
// </copyright>
// -----------------------------------------------------------------------

namespace Microsoft.ApplicationInsights.NLogTarget
{
    using System;
    using System.Globalization;
    using System.Diagnostics;

    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;
    using Microsoft.ApplicationInsights.Implementation;

    using NLog;
    using NLog.Common;
    using NLog.Targets;

    /// <summary>
    /// NLog Target that routes all logging output to the Application Insights logging framework.
    /// The messages will be uploaded to the Application Insights cloud service.
    /// </summary>
    [Target("ApplicationInsightsTarget")]
    public sealed class ApplicationInsightsTarget : TargetWithContext
    {
        private TelemetryClient? telemetryClient;
        private TelemetryConfiguration? telemetryConfiguration;
        private readonly NLog.Layouts.Layout instrumentationKeyLayout = string.Empty;
        private NLog.Layouts.Layout connectionStringLayout = string.Empty;

        /// <summary>
        /// Initializers a new instance of ApplicationInsightsTarget type.
        /// </summary>
        public ApplicationInsightsTarget()
        {
            this.Layout = @"${message}";
            this.IncludeEventProperties = true;
        }

        /// <summary>
        /// Gets or sets the Application Insights connectionstring for your application.
        /// </summary>
        public string? ConnectionString
        {
            get => (this.connectionStringLayout as NLog.Layouts.SimpleLayout)?.Text ?? null;
            set => this.connectionStringLayout = value ?? string.Empty;
        }

        /// <summary>
        /// Gets or sets a value indicating whether to send <see cref="Activity.Current"/> information to Application Insights.
        /// </summary>
        public bool IncludeActivity { get; set; } = false;

        /// <summary>
        /// Gets or sets the factory for creating TelemetryConfiguration, so unit-tests can override in-memory-channel.
        /// </summary>
        internal Func<TelemetryConfiguration>? TelemetryConfigurationFactory { get; set; }

        internal void BuildPropertyBag(LogEventInfo logEvent, ITelemetry trace)
        {
            trace.Timestamp = logEvent.TimeStamp;
            trace.Sequence = logEvent.SequenceID.ToString(CultureInfo.InvariantCulture);

            if (trace is not ISupportProperties traceWithProperties)
            {
                return;
            }

            var propertyBag = traceWithProperties.Properties;

            if (!string.IsNullOrEmpty(logEvent.LoggerName))
            {
                propertyBag.Add("LoggerName", logEvent.LoggerName);
            }

            if (logEvent.UserStackFrame != null)
            {
                propertyBag.Add("UserStackFrame", logEvent.UserStackFrame.ToString());
                propertyBag.Add("UserStackFrameNumber", logEvent.UserStackFrameNumber.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                var callsiteClassName = logEvent.CallerClassName;
                if (!string.IsNullOrEmpty(callsiteClassName))
                    propertyBag.Add("UserStackClassName", callsiteClassName);
                var callsiteMemberName = logEvent.CallerMemberName;
                if (!string.IsNullOrEmpty(callsiteMemberName))
                    propertyBag.Add("UserStackMemberName", callsiteMemberName);
                var callsiteSourceFilePath = logEvent.CallerFilePath;
                if (!string.IsNullOrEmpty(callsiteSourceFilePath))
                    propertyBag.Add("UserStackSourceFile", callsiteSourceFilePath);
                var callsiteSourceLineNumber = logEvent.CallerLineNumber;
                if (callsiteSourceLineNumber != 0)
                    propertyBag.Add("UserStackSourceLine", callsiteSourceLineNumber.ToString());
            }

            if (this.ShouldIncludeProperties(logEvent) || this.ContextProperties.Count > 0)
            {
                this.GetAllProperties(logEvent, new StringDictionaryConverter(propertyBag));
            }
        }

        /// <summary>
        /// Initializes the Target and configures TelemetryClient.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:DisposeObjectsBeforeLosingScope", Justification = "ApplicationInsightsTarget class handles ownership of TelemetryConfiguration with Dispose.")]
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            string connectionString = this.connectionStringLayout.Render(LogEventInfo.CreateNullEvent());

            // Check if nlog application insights target has connectionstring in config file then
            // configure new telemetryclient with the connectionstring otherwise using legacy instrumentationkey.
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                this.telemetryConfiguration = this.TelemetryConfigurationFactory?.Invoke() ?? TelemetryConfiguration.CreateDefault();
                this.telemetryConfiguration.ConnectionString = connectionString;
                this.telemetryClient = new TelemetryClient(this.telemetryConfiguration);
            }
            else
            {
#pragma warning disable CS0618 // Type or member is obsolete: TelemtryConfiguration.Active is used in TelemetryClient constructor.
                this.telemetryClient = new TelemetryClient();
#pragma warning restore CS0618 // Type or member is obsolete
                string instrumentationKey = this.instrumentationKeyLayout.Render(LogEventInfo.CreateNullEvent());
                if (!string.IsNullOrWhiteSpace(instrumentationKey))
                {
                    this.telemetryClient.Context.InstrumentationKey = instrumentationKey;
                }
            }

            this.telemetryClient.Context.GetInternalContext().SdkVersion = SdkVersionUtils.GetSdkVersion("nlog:");
        }

        /// <summary>
        /// Closes the target and releases resources used by the current instance of the <see cref="ApplicationInsightsTarget"/> class.
        /// </summary>
        protected override void CloseTarget()
        {
            this.telemetryConfiguration?.Dispose();
            this.telemetryConfiguration = null;

            base.CloseTarget();
        }

        /// <summary>
        /// Send the log message to Application Insights.
        /// </summary>
        /// <exception cref="ArgumentNullException">If <paramref name="logEvent"/> is null.</exception>
        protected override void Write(LogEventInfo logEvent)
        {
            if (logEvent == null)
            {
                throw new ArgumentNullException(nameof(logEvent));
            }

            if (logEvent.Exception != null)
            {
                this.SendException(logEvent);
            }
            else
            {
                this.SendTrace(logEvent);
            }
        }

        /// <summary>
        /// Flush any pending log messages.
        /// </summary>
        /// <param name="asyncContinuation">The asynchronous continuation.</param>
        protected override void FlushAsync(AsyncContinuation asyncContinuation)
        {
            if (asyncContinuation == null)
            {
                throw new ArgumentNullException(nameof(asyncContinuation));
            }

            try
            {
                this.telemetryClient?.FlushAsync(default).ContinueWith(t => asyncContinuation(t.Exception));
            }
            catch (Exception ex)
            {
                asyncContinuation(ex);
            }
        }

        /// <summary>
        /// Releases resources used by the current instance of the <see cref="ApplicationInsightsTarget"/> class.
        /// </summary>
        /// <param name="disposing">Dispose managed state (managed objects).</param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                this.telemetryConfiguration?.Dispose();
                this.telemetryConfiguration = null;
            }
        }

        private static SeverityLevel? GetSeverityLevel(LogLevel logEventLevel)
        {
            if (logEventLevel == null)
            {
                return null;
            }

            if (logEventLevel.Ordinal == LogLevel.Trace.Ordinal ||
                logEventLevel.Ordinal == LogLevel.Debug.Ordinal)
            {
                return SeverityLevel.Verbose;
            }

            if (logEventLevel.Ordinal == LogLevel.Info.Ordinal)
            {
                return SeverityLevel.Information;
            }

            if (logEventLevel.Ordinal == LogLevel.Warn.Ordinal)
            {
                return SeverityLevel.Warning;
            }

            if (logEventLevel.Ordinal == LogLevel.Error.Ordinal)
            {
                return SeverityLevel.Error;
            }

            if (logEventLevel.Ordinal == LogLevel.Fatal.Ordinal)
            {
                return SeverityLevel.Critical;
            }

            // The only possible value left if OFF but we should never get here in this case
            return null;
        }

        private void SendException(LogEventInfo logEvent)
        {
            var exceptionTelemetry = new ExceptionTelemetry(logEvent.Exception)
            {
                SeverityLevel = GetSeverityLevel(logEvent.Level),
                Message = $"{logEvent.Exception.GetType()}: {logEvent.Exception.Message}",
            };

            var logMessage = this.RenderLogEvent(this.Layout, logEvent);
            if (!string.IsNullOrEmpty(logMessage))
            {
                exceptionTelemetry.Properties.Add("Message", logMessage);
            }

            this.AddActivityIfEnabled(exceptionTelemetry);
            this.BuildPropertyBag(logEvent, exceptionTelemetry);
            this.telemetryClient?.Track(exceptionTelemetry);
        }

        private void SendTrace(LogEventInfo logEvent)
        {
            var logMessage = this.RenderLogEvent(this.Layout, logEvent);
            var trace = new TraceTelemetry(logMessage)
            {
                SeverityLevel = GetSeverityLevel(logEvent.Level),
            };

            this.AddActivityIfEnabled(trace);
            this.BuildPropertyBag(logEvent, trace);
            this.telemetryClient?.Track(trace);
        }

        private void AddActivityIfEnabled(ITelemetry trace)
        {
            if (!this.IncludeActivity)
            {
                return;
            }

            var activity = Activity.Current;
            if (activity != null)
            {
                trace.Context.Operation.Id = activity.TraceId.ToHexString();
                trace.Context.Operation.ParentId = activity.ParentSpanId.ToHexString();
            }
        }
    }
}
