﻿namespace Microsoft.ApplicationInsights.NLogTarget.Tests
{
    using System;
    using System.Diagnostics;
    using System.Linq;

    using Microsoft.ApplicationInsights.CommonTestShared;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;
    using Microsoft.ApplicationInsights.NLogTarget;
    using Microsoft.ApplicationInsights.Tracing.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using NLog;
    using NLog.Config;
    using NLog.Targets;
    using NLog.Targets.Wrappers;

    [TestClass]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "Disposing the object on the TestCleanup method")]
    public class ApplicationInsightsTargetTests
    {
        private AdapterHelper adapterHelper;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void Initialize()
        {
            this.adapterHelper = new AdapterHelper();
        }

        [TestCleanup]
        public void Cleanup()
        {
            NLog.GlobalDiagnosticsContext.Clear();
            this.adapterHelper.Dispose();
        }

        [TestMethod]
        [TestCategory("NLogTarget")]
        public void InitializeTargetNotThrowsWhenConnectionStringIsNull()
        {
            try
            {
                this.CreateTargetWithGivenConnectionString(null);
            }
            catch (NLogConfigurationException ex)
            {
                Assert.Fail("Not expecting to get NLogConfigurationException but was thrown {0}", ex.Message);
            }
        }

        [TestMethod]
        [TestCategory("NLogTarget")]
        public void InitializeTargetNotThrowsWhenConnectionStringIsEmptyString()
        {
            try
            {
                this.CreateTargetWithGivenConnectionString(string.Empty);
            }
            catch (NLogConfigurationException ex)
            {
                Assert.Fail("Expected NLogConfigurationException but none was thrown with message:{0}", ex.Message);
            }
        }

        [TestMethod]
        [TestCategory("NLogTarget")]
        public void ExceptionsDoNotEscapeNLog()
        {
            var connectionString = "InstrumentationKey=93d9c2b7-e633-4571-8520-d391511a1df5;IngestionEndpoint=https://westeurope.in.applicationinsights.azure.example.com/;LiveEndpoint=https://westeurope.livediagnostics.monitor.azure.example.com/";

            static void loggerAction(Logger aiLogger) => aiLogger.Trace("Hello World");
            this.CreateTargetWithGivenConnectionString(connectionString, loggerAction);
        }

        [TestMethod]
        [TestCategory("NLogTarget")]
        public void TracesAreEnqueuedInChannel()
        {
            var connectionString = "InstrumentationKey=93d9c2b7-e633-4571-8520-d391511a1df5;IngestionEndpoint=https://westeurope.in.applicationinsights.azure.example.com/;LiveEndpoint=https://westeurope.livediagnostics.monitor.azure.example.com/";

            var aiLogger = this.CreateTargetWithGivenConnectionString(connectionString);
            this.VerifyMessagesInMockChannel(aiLogger, "93d9c2b7-e633-4571-8520-d391511a1df5");
        }

        [TestMethod]
        [TestCategory("NLogTarget")]
        public void ConnectionStringIsReadFromEnvironment()
        {
            var connectionString = "InstrumentationKey=F8474271-D231-45B6-8DD4-D344C309AE69;IngestionEndpoint=https://westeurope.in.applicationinsights.azure.example.com/;LiveEndpoint=https://westeurope.livediagnostics.monitor.azure.example.com/";

            var aiLogger = this.CreateTargetWithGivenConnectionString(connectionString);
            this.VerifyMessagesInMockChannel(aiLogger, "F8474271-D231-45B6-8DD4-D344C309AE69");
        }

        [TestMethod]
        [TestCategory("NLogTarget")]
        public void ConnectionStringIsReadFromLayout()
        {
            var connectionString = "InstrumentationKey=F8474271-D231-45B6-8DD4-D344C309AE69;IngestionEndpoint=https://westeurope.in.applicationinsights.azure.example.com/;LiveEndpoint=https://westeurope.livediagnostics.monitor.azure.example.com/";

            var gdcKey = Guid.NewGuid().ToString();
            GlobalDiagnosticsContext.Set(gdcKey, connectionString);

            var aiLogger = this.CreateTargetWithGivenConnectionString($"${{gdc:item={gdcKey}}}");
            this.VerifyMessagesInMockChannel(aiLogger, "F8474271-D231-45B6-8DD4-D344C309AE69");
        }

        [TestMethod]
        [TestCategory("NLogTarget")]
        public void TraceAreEnqueuedInChannelAndContainAllProperties()
        {
            var connectionString = "InstrumentationKey=F8474271-D231-45B6-8DD4-D344C309AE69;IngestionEndpoint=https://westeurope.in.applicationinsights.azure.example.com/;LiveEndpoint=https://westeurope.livediagnostics.monitor.azure.example.com/";

#pragma warning disable CA5394 // Do not use insecure randomness - Test message has no security impact
            var random = new Random();
            var number = random.Next();
#pragma warning restore CA5394 // Do not use insecure randomness - Test message has no security impact

            var aiLogger = this.CreateTargetWithGivenConnectionString(connectionString);

            aiLogger.Debug("Message {0}, using instrumentation key:{1}", number, connectionString);

            var telemetry = this.adapterHelper.Channel.SentItems.FirstOrDefault() as TraceTelemetry;
            Assert.IsNotNull(telemetry, "Didn't get the log event from the channel");

            telemetry.Properties.TryGetValue("LoggerName", out var loggerName);
            Assert.AreEqual("AITarget", loggerName);
        }

        [TestMethod]
        [TestCategory("NLogTarget")]
        public void SdkVersionIsCorrect()
        {
            var aiLogger = this.CreateTargetWithGivenConnectionString();

            aiLogger.Debug("Message");

            var telemetry = (TraceTelemetry)this.adapterHelper.Channel.SentItems.First();

            var expectedVersion = SdkVersionHelper.GetExpectedSdkVersion(prefix: "nlog:", loggerType: typeof(ApplicationInsightsTarget));
            Assert.AreEqual(expectedVersion, telemetry.Context.GetInternalContext().SdkVersion);
        }

        [TestMethod]
        [Ignore("This test requires a valid connection string or instrumentation key to run")]
        [TestCategory("NLogTarget")]
        public async Task TelemetryIsAcceptedByValidateEndpoint()
        {
            var aiLogger = this.CreateTargetWithGivenConnectionString();

            aiLogger.Debug("Message");

            var telemetry = (TraceTelemetry)this.adapterHelper.Channel.SentItems.First();

            Assert.IsNull(await TelemetrySender.ValidateEndpointSend(telemetry).ConfigureAwait(true));
        }

        [TestMethod]
        [TestCategory("NLogTarget")]
        public void TraceHasTimestamp()
        {
            var aiLogger = this.CreateTargetWithGivenConnectionString();

            aiLogger.Debug("Message");

            var telemetry = this.adapterHelper.Channel.SentItems.FirstOrDefault() as TraceTelemetry;
            Assert.IsNotNull(telemetry, "Didn't get the log event from the channel");

            Assert.AreNotEqual(default, telemetry.Timestamp);
        }

        [TestMethod]
        [TestCategory("NLogTarget")]
        public void TraceMessageCanBeFormedUsingLayout()
        {
            using ApplicationInsightsTarget target = new();
            target.Layout = @"${uppercase:${level}} ${message}";

            var aiLogger = this.CreateTargetWithGivenConnectionString(target: target);

            aiLogger.Debug("Message");

            var telemetry = this.adapterHelper.Channel.SentItems.FirstOrDefault() as TraceTelemetry;
            Assert.IsNotNull(telemetry, "Didn't get the log event from the channel");

            Assert.AreEqual("DEBUG Message", telemetry.Message);
        }

        [TestMethod]
        [TestCategory("NLogTarget")]
        public void TraceMessageWithoutLayoutDefaultsToMessagePassed()
        {
            var aiLogger = this.CreateTargetWithGivenConnectionString();

            aiLogger.Debug("My Message");

            var telemetry = this.adapterHelper.Channel.SentItems.FirstOrDefault() as TraceTelemetry;
            Assert.IsNotNull(telemetry, "Didn't get the log event from the channel");

            Assert.AreEqual("My Message", telemetry.Message);
        }

        [TestMethod]
        [TestCategory("NLogTarget")]
        public void TraceHasSequenceId()
        {
            var aiLogger = this.CreateTargetWithGivenConnectionString();

            aiLogger.Debug("Message");

            var telemetry = this.adapterHelper.Channel.SentItems.FirstOrDefault() as TraceTelemetry;
            Assert.IsNotNull(telemetry, "Didn't get the log event from the channel");

            Assert.AreNotEqual("0", telemetry.Sequence);
        }

        [TestMethod]
        [TestCategory("NLogTarget")]
        public void TraceHasCustomProperties()
        {
            var aiLogger = this.CreateTargetWithGivenConnectionString();

            var eventInfo = new LogEventInfo(LogLevel.Trace, "TestLogger", "Hello!");
            eventInfo.Properties["Name"] = "Value";
            aiLogger.Log(eventInfo);

            var telemetry = this.adapterHelper.Channel.SentItems.FirstOrDefault() as TraceTelemetry;
            Assert.IsNotNull(telemetry, "Didn't get the log event from the channel");
            Assert.AreEqual("Value", telemetry.Properties["Name"]);
        }


        [TestMethod]
        [TestCategory("NLogTarget")]
        public void GlobalDiagnosticContextPropertiesAreAddedToProperties()
        {
            using ApplicationInsightsTarget target = new()
            {
                IncludeGdc = true,
                ContextProperties = { new("global_prop", "${gdc:item=global_prop}") }
            };
            var aiLogger = this.CreateTargetWithGivenConnectionString(target: target);

            NLog.GlobalDiagnosticsContext.Set("global_prop", "global_value");
            aiLogger.Debug("Message");

            var telemetry = this.adapterHelper.Channel.SentItems.FirstOrDefault() as TraceTelemetry;
            Assert.AreEqual("global_value", telemetry.Properties["global_prop"]);
        }

        [TestMethod]
        [TestCategory("NLogTarget")]
        public void GlobalDiagnosticContextPropertiesSupplementEventProperties()
        {
            using ApplicationInsightsTarget target = new()
            {
                IncludeGdc = true,
                ContextProperties = { new("global_prop", "${gdc:item=global_prop}") }
            };
            var aiLogger = this.CreateTargetWithGivenConnectionString(target: target);

            NLog.GlobalDiagnosticsContext.Set("global_prop", "global_value");

            var eventInfo = new LogEventInfo(LogLevel.Trace, "TestLogger", "Hello!");
            eventInfo.Properties["Name"] = "Value";
            aiLogger.Log(eventInfo);

            var telemetry = this.adapterHelper.Channel.SentItems.FirstOrDefault() as TraceTelemetry;
            Assert.AreEqual("global_value", telemetry.Properties["global_prop"]);
            Assert.AreEqual("Value", telemetry.Properties["Name"]);
        }

        [TestMethod]
        [Ignore("NLog behaviour seems to have changed, this test is no longer valid")]
        [TestCategory("NLogTarget")]
#pragma warning disable CA1707 // Identifiers should not contain underscores
        public void EventPropertyKeyNameIsAppendedWith_1_IfSameAsGlobalDiagnosticContextKeyName()
#pragma warning restore CA1707 // Identifiers should not contain underscores
        {
            using ApplicationInsightsTarget target = new()
            {
                IncludeGdc = true,
                IncludeEventProperties = true,
                IncludeScopeNested = true,
                IncludeScopeProperties = true
            };
            target.ContextProperties.Add(new TargetPropertyWithContext("Name", "${gdc:item=Name}"));
            var aiLogger = this.CreateTargetWithGivenConnectionString(target: target);

            GlobalDiagnosticsContext.Set("Name", "Global Value");
            var eventInfo = new LogEventInfo(LogLevel.Trace, "TestLogger", "Hello!");
            eventInfo.Properties["Name"] = "Value";
            aiLogger.Log(eventInfo);

            var telemetry = this.adapterHelper.Channel.SentItems.FirstOrDefault() as TraceTelemetry;
            Assert.IsTrue(telemetry.Properties.ContainsKey("Name"), "Key name altered");
            Assert.AreEqual("Value", telemetry.Properties["Name"]);
            Assert.IsTrue(telemetry.Properties.ContainsKey("Name_1"));
            Assert.AreEqual("Global Value", telemetry.Properties["Name_1"]);
        }

        [TestMethod]
        [TestCategory("NLogTarget")]
        public void TraceAreEnqueuedInChannelAndContainExceptionMessage()
        {
            var connectionString = "InstrumentationKey=F8474271-D231-45B6-8DD4-D344C309AE69;IngestionEndpoint=https://westeurope.in.applicationinsights.azure.example.com/;LiveEndpoint=https://westeurope.livediagnostics.monitor.azure.example.com/";
            var aiLogger = this.CreateTargetWithGivenConnectionString(connectionString);
            Exception expectedException;

            try
            {
                throw new Exception("Test logging exception");
            }
            catch (Exception exception)
            {
                expectedException = exception;
                aiLogger.Debug(exception, "testing exception scenario");
            }

            var telemetry = (ExceptionTelemetry)this.adapterHelper.Channel.SentItems.First();
            Assert.AreEqual("System.Exception: Test logging exception", telemetry.Message);
            Assert.AreEqual(expectedException.Message, telemetry.Exception.Message);
        }

        [TestMethod]
        [TestCategory("NLogTarget")]
        public void CustomMessageIsAddedToExceptionTelemetryCustomProperties()
        {
            var aiLogger = this.CreateTargetWithGivenConnectionString();

            try
            {
                throw new Exception("Test logging exception");
            }
            catch (Exception exception)
            {
                aiLogger.Debug(exception, "custom message");
            }

            var telemetry = (ExceptionTelemetry)this.adapterHelper.Channel.SentItems.First();
            Assert.AreEqual("System.Exception: Test logging exception", telemetry.Message);
            Assert.IsTrue(telemetry.Properties["Message"].StartsWith("custom message", StringComparison.Ordinal));
        }

        [TestMethod]
        [TestCategory("NLogTarget")]
        public void NLogTraceIsSentAsVerboseTraceItem()
        {
            var aiLogger = this.CreateTargetWithGivenConnectionString("InstrumentationKey=F8474271-D231-45B6-8DD4-D344C309AE69;IngestionEndpoint=https://westeurope.in.applicationinsights.azure.example.com/;LiveEndpoint=https://westeurope.livediagnostics.monitor.azure.example.com/");

            aiLogger.Trace("trace");

            var telemetry = this.adapterHelper.Channel.SentItems.FirstOrDefault() as TraceTelemetry;
            Assert.AreEqual(SeverityLevel.Verbose, telemetry.SeverityLevel);
        }

        [TestMethod]
        [TestCategory("NLogTarget")]
        public void NLogDebugIsSentAsVerboseTraceItem()
        {
            var aiLogger = this.CreateTargetWithGivenConnectionString("InstrumentationKey=F8474271-D231-45B6-8DD4-D344C309AE69;IngestionEndpoint=https://westeurope.in.applicationinsights.azure.example.com/;LiveEndpoint=https://westeurope.livediagnostics.monitor.azure.example.com/");

            aiLogger.Debug("trace");

            var telemetry = this.adapterHelper.Channel.SentItems.FirstOrDefault() as TraceTelemetry;
            Assert.AreEqual(SeverityLevel.Verbose, telemetry.SeverityLevel);
        }

        [TestMethod]
        [TestCategory("NLogTarget")]
        public void NLogInfoIsSentAsInformationTraceItem()
        {
            var aiLogger = this.CreateTargetWithGivenConnectionString("InstrumentationKey=F8474271-D231-45B6-8DD4-D344C309AE69;IngestionEndpoint=https://westeurope.in.applicationinsights.azure.example.com/;LiveEndpoint=https://westeurope.livediagnostics.monitor.azure.example.com/");

            aiLogger.Info("trace");

            var telemetry = this.adapterHelper.Channel.SentItems.FirstOrDefault() as TraceTelemetry;
            Assert.AreEqual(SeverityLevel.Information, telemetry.SeverityLevel);
        }

        [TestMethod]
        [TestCategory("NLogTarget")]
        public void NLogWarnIsSentAsWarningTraceItem()
        {
            var aiLogger = this.CreateTargetWithGivenConnectionString("InstrumentationKey=F8474271-D231-45B6-8DD4-D344C309AE69;IngestionEndpoint=https://westeurope.in.applicationinsights.azure.example.com/;LiveEndpoint=https://westeurope.livediagnostics.monitor.azure.example.com/");

            aiLogger.Warn("trace");

            var telemetry = this.adapterHelper.Channel.SentItems.FirstOrDefault() as TraceTelemetry;
            Assert.AreEqual(SeverityLevel.Warning, telemetry.SeverityLevel);
        }

        [TestMethod]
        [TestCategory("NLogTarget")]
        public void NLogErrorIsSentAsErrorTraceItem()
        {
            var aiLogger = this.CreateTargetWithGivenConnectionString("InstrumentationKey=F8474271-D231-45B6-8DD4-D344C309AE69;IngestionEndpoint=https://westeurope.in.applicationinsights.azure.example.com/;LiveEndpoint=https://westeurope.livediagnostics.monitor.azure.example.com/");

            aiLogger.Error("trace");

            var telemetry = this.adapterHelper.Channel.SentItems.FirstOrDefault() as TraceTelemetry;
            Assert.AreEqual(SeverityLevel.Error, telemetry.SeverityLevel);
        }

        [TestMethod]
        [TestCategory("NLogTarget")]
        public void NLogFatalIsSentAsCriticalTraceItem()
        {
            var aiLogger = this.CreateTargetWithGivenConnectionString("InstrumentationKey=F8474271-D231-45B6-8DD4-D344C309AE69;IngestionEndpoint=https://westeurope.in.applicationinsights.azure.example.com/;LiveEndpoint=https://westeurope.livediagnostics.monitor.azure.example.com/");

            aiLogger.Fatal("trace");

            var telemetry = this.adapterHelper.Channel.SentItems.FirstOrDefault() as TraceTelemetry;
            Assert.AreEqual(SeverityLevel.Critical, telemetry.SeverityLevel);
        }

        [TestMethod]
        [TestCategory("NLogTarget")]
        public void NLogPropertyDuplicateKeyDuplicateValue()
        {
            using var aiTarget = new ApplicationInsightsTarget();
            var logEventInfo = new LogEventInfo();
            var loggerNameVal = "thisisaloggername";

            logEventInfo.LoggerName = loggerNameVal;
            logEventInfo.Properties.Add("LoggerName", loggerNameVal);

            var traceTelemetry = new TraceTelemetry();

            aiTarget.BuildPropertyBag(logEventInfo, traceTelemetry);

            Assert.IsTrue(traceTelemetry.Properties.ContainsKey("LoggerName"));
            Assert.AreEqual(loggerNameVal, traceTelemetry.Properties["LoggerName"]);
        }

        [TestMethod]
        [TestCategory("NLogTarget")]
        public void NLogPropertyDuplicateKeyDifferentValue()
        {
            using var aiTarget = new ApplicationInsightsTarget();
            var logEventInfo = new LogEventInfo();
            var loggerNameVal = "thisisaloggername";
            var loggerNameVal2 = "thisisadifferentloggername";

            logEventInfo.LoggerName = loggerNameVal;
            logEventInfo.Properties.Add("LoggerName", loggerNameVal2);

            var traceTelemetry = new TraceTelemetry();

            aiTarget.BuildPropertyBag(logEventInfo, traceTelemetry);

            Assert.IsTrue(traceTelemetry.Properties.ContainsKey("LoggerName"));
            Assert.AreEqual(loggerNameVal, traceTelemetry.Properties["LoggerName"]);

            Assert.IsTrue(traceTelemetry.Properties.ContainsKey("LoggerName_1"));
            Assert.AreEqual(loggerNameVal2, traceTelemetry.Properties["LoggerName_1"]);
        }


        [TestMethod]
        [TestCategory("NLogTarget")]
        public void NLogTargetFlushesTelemetryClient()
        {
            var aiLogger = this.CreateTargetWithGivenConnectionString();

            using var flushEvent = new System.Threading.ManualResetEvent(false);
            Exception flushException = null;
            void asyncContinuation(Exception ex) { flushException = ex; flushEvent.Set(); }
            aiLogger.Factory.Flush(asyncContinuation, 5000);
            Assert.IsTrue(flushEvent.WaitOne(5000));
        }

        [TestMethod]
        [TestCategory("NLogTarget")]
        public void NLogInfoIsSentAsInformationTraceItemWithAIConnectionString()
        {
            var aiLogger = this.CreateTargetWithGivenConnectionString("InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://westeurope.in.applicationinsights.azure.example.com/;LiveEndpoint=https://westeurope.livediagnostics.monitor.azure.example.com/");
            aiLogger.Info("Info message");

            var telemetry = (TraceTelemetry)this.adapterHelper.Channel.SentItems.First();
            Assert.AreEqual($"Info message", telemetry.Message);
        }

        [TestMethod]
        [TestCategory("NLogTarget")]
        public void NLogTraceIsSentAsVerboseTraceItemWithAIConnectionString()
        {
            var aiLogger = this.CreateTargetWithGivenConnectionString("InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://westeurope.in.applicationinsights.azure.example.com/;LiveEndpoint=https://westeurope.livediagnostics.monitor.azure.example.com/");
            aiLogger.Trace("Trace message");

            var telemetry = this.adapterHelper.Channel.SentItems.FirstOrDefault() as TraceTelemetry;
            Assert.AreEqual("Trace message", telemetry.Message);
        }

        [TestMethod]
        [TestCategory("NLogTarget")]
        public void NLogDebugIsSentAsVerboseTraceItemWithAIConnectionString()
        {
            var aiLogger = this.CreateTargetWithGivenConnectionString("InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://westeurope.in.applicationinsights.azure.example.com/;LiveEndpoint=https://westeurope.livediagnostics.monitor.azure.example.com/");
            aiLogger.Debug("Debug Message");

            var telemetry = this.adapterHelper.Channel.SentItems.FirstOrDefault() as TraceTelemetry;
            Assert.AreEqual("Debug Message", telemetry.Message);
        }

        [TestMethod]
        [TestCategory("NLogTarget")]
        public void NLogWarnIsSentAsWarningTraceItemWithAIConnectionString()
        {
            var aiLogger = this.CreateTargetWithGivenConnectionString("InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://westeurope.in.applicationinsights.azure.example.com/;LiveEndpoint=https://westeurope.livediagnostics.monitor.azure.example.com/");

            aiLogger.Warn("Warn message");

            var telemetry = this.adapterHelper.Channel.SentItems.FirstOrDefault() as TraceTelemetry;
            Assert.AreEqual("Warn message", telemetry.Message);
        }

        [TestMethod]
        [TestCategory("NLogTarget")]
        public void NLogErrorIsSentAsVerboseTraceItemWithAIConnectionString()
        {
            var aiLogger = this.CreateTargetWithGivenConnectionString("InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://westeurope.in.applicationinsights.azure.example.com/;LiveEndpoint=https://westeurope.livediagnostics.monitor.azure.example.com/");
            aiLogger.Error("Error Message");

            var telemetry = this.adapterHelper.Channel.SentItems.FirstOrDefault() as TraceTelemetry;
            Assert.AreEqual("Error Message", telemetry.Message);
        }

        [TestMethod]
        [TestCategory("NLogTarget")]
        public void NLogInfoContainsCurrentActivity()
        {
            Activity activity = new(nameof(NLogInfoContainsCurrentActivity));
            activity.Start();
            var originalActivity = Activity.Current;
            Activity.Current = activity;

            try
            {
                var aiLogger = this.CreateTargetWithGivenConnectionString("InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://westeurope.in.applicationinsights.azure.example.com/;LiveEndpoint=https://westeurope.livediagnostics.monitor.azure.example.com/", includeActivity: true);
                aiLogger.Info("Info message");

                var telemetry = (TraceTelemetry)this.adapterHelper.Channel.SentItems.First();
                Assert.AreEqual($"Info message", telemetry.Message);
                Assert.AreEqual(activity.TraceId.ToString(), telemetry.Context.Operation.Id);
                Assert.AreEqual(activity.SpanId.ToHexString(), telemetry.Context.Operation.ParentId);
            }
            finally
            {
                Activity.Current = originalActivity;
            }
        }

        [TestMethod]
        [TestCategory("NLogTarget")]
        public void NLogErrorContainsCurrentActivity()
        {
            Activity activity = new(nameof(NLogErrorContainsCurrentActivity));
            activity.Start();
            var originalActivity = Activity.Current;
            Activity.Current = activity;

            try
            {
                var aiLogger = this.CreateTargetWithGivenConnectionString("InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://westeurope.in.applicationinsights.azure.example.com/;LiveEndpoint=https://westeurope.livediagnostics.monitor.azure.example.com/", includeActivity: true);
                aiLogger.Error("Error Message");

                var telemetry = this.adapterHelper.Channel.SentItems.FirstOrDefault() as TraceTelemetry;
                Assert.AreEqual("Error Message", telemetry.Message);
                Assert.AreEqual(activity.TraceId.ToString(), telemetry.Context.Operation.Id);
                Assert.AreEqual(activity.SpanId.ToHexString(), telemetry.Context.Operation.ParentId);
            }
            finally
            {
                Activity.Current = originalActivity;
            }
        }

        [TestMethod]
        [TestCategory("NLogTarget")]
        public void NLogInfoContainsCurrentActivityAsyncTarget()
        {
            Activity activity = new(nameof(NLogInfoContainsCurrentActivityAsyncTarget));
            activity.Start();
            var originalActivity = Activity.Current;
            Activity.Current = activity;

            try
            {
                var aiLogger = this.CreateAsyncTargetWithGivenConnectionString("InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://westeurope.in.applicationinsights.azure.example.com/;LiveEndpoint=https://westeurope.livediagnostics.monitor.azure.example.com/", includeActivity: true);
                aiLogger.Info("Info message");

                var telemetry = (TraceTelemetry)this.adapterHelper.Channel.SentItems.First();
                Assert.AreEqual($"Info message", telemetry.Message);
                Assert.AreEqual(activity.TraceId.ToString(), telemetry.Context.Operation.Id);
                Assert.AreEqual(activity.SpanId.ToHexString(), telemetry.Context.Operation.ParentId);
            }
            finally
            {
                Activity.Current = originalActivity;
            }
        }

        [TestMethod]
        [TestCategory("NLogTarget")]
        public void NLogErrorContainsCurrentActivityAsyncTarget()
        {
            Activity activity = new(nameof(NLogErrorContainsCurrentActivityAsyncTarget));
            activity.Start();
            var originalActivity = Activity.Current;
            Activity.Current = activity;

            try
            {
                var aiLogger = this.CreateAsyncTargetWithGivenConnectionString("InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://westeurope.in.applicationinsights.azure.example.com/;LiveEndpoint=https://westeurope.livediagnostics.monitor.azure.example.com/", includeActivity: true);
                aiLogger.Error("Error Message");

                var telemetry = this.adapterHelper.Channel.SentItems.FirstOrDefault() as TraceTelemetry;
                Assert.AreEqual("Error Message", telemetry.Message);
                Assert.AreEqual(activity.TraceId.ToString(), telemetry.Context.Operation.Id);
                Assert.AreEqual(activity.SpanId.ToHexString(), telemetry.Context.Operation.ParentId);
            }
            finally
            {
                Activity.Current = originalActivity;
            }
        }

        private void VerifyMessagesInMockChannel(Logger aiLogger, string instrumentationKey)
        {
            aiLogger.Trace("Sample trace message");
            aiLogger.Debug("Sample debug message");
            aiLogger.Info("Sample informational message");
            aiLogger.Warn("Sample warning message");
            aiLogger.Error("Sample error message");
            aiLogger.Fatal("Sample fatal error message");

            AdapterHelper.ValidateChannel(this.adapterHelper, instrumentationKey, 6);
        }

        private Logger CreateTargetWithGivenConnectionString(
            string connectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://westeurope.in.applicationinsights.azure.example.com/;LiveEndpoint=https://westeurope.livediagnostics.monitor.azure.example.com/",
            Action<Logger> loggerAction = null,
            ApplicationInsightsTarget target = null,
            bool includeActivity = false)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope - Caller is responsible for disposal
            target ??= new ApplicationInsightsTarget();
#pragma warning restore CA2000 // Dispose objects before losing scope - Caller is responsible for disposal

            target.TelemetryConfigurationFactory = () => new TelemetryConfiguration() { TelemetryChannel = this.adapterHelper.Channel };

            target.ConnectionString = connectionString;
            target.IncludeActivity = includeActivity;

            var rule = new LoggingRule("*", LogLevel.Trace, target);
            var config = new LoggingConfiguration();
            config.AddTarget("AITarget", target);
            config.LoggingRules.Add(rule);

            LogFactory logFactory = new()
            {
                Configuration = config
            };

            var aiLogger = logFactory.GetLogger("AITarget");

            if (loggerAction != null)
            {
                loggerAction(aiLogger);
                target.Dispose();
                return null;
            }

            return aiLogger;
        }

        private Logger CreateAsyncTargetWithGivenConnectionString(
            string connectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://westeurope.in.applicationinsights.azure.example.com/;LiveEndpoint=https://westeurope.livediagnostics.monitor.azure.example.com/",
            Action<Logger> loggerAction = null,
            ApplicationInsightsTarget target = null,
            bool includeActivity = false)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope - Caller is responsible for disposal
            target ??= new ApplicationInsightsTarget();
#pragma warning restore CA2000 // Dispose objects before losing scope - Caller is responsible for disposal

            target.TelemetryConfigurationFactory = () => new TelemetryConfiguration() { TelemetryChannel = this.adapterHelper.Channel };

            target.ConnectionString = connectionString;
            target.IncludeActivity = includeActivity;

#pragma warning disable CA2000 // Dispose objects before losing scope - Caller is responsible for disposal
            var asyncWrapper = new AsyncTargetWrapper
            {
                Name = "async",
                WrappedTarget = target,
                QueueLimit = 5000,
                BatchSize = 100,
                TimeToSleepBetweenBatches = 100,
                OverflowAction = AsyncTargetWrapperOverflowAction.Grow
            };
#pragma warning restore CA2000 // Dispose objects before losing scope - Caller is responsible for disposal

            var rule = new LoggingRule("*", LogLevel.Trace, asyncWrapper);
            var config = new LoggingConfiguration();
            config.AddTarget("AITarget", asyncWrapper);
            config.LoggingRules.Add(rule);

            LogFactory logFactory = new()
            {
                Configuration = config
            };

            var aiLogger = logFactory.GetLogger("AITarget");

            if (loggerAction != null)
            {
                loggerAction(aiLogger);
                target.Dispose();
                return null;
            }

            return aiLogger;
        }
    }
}
