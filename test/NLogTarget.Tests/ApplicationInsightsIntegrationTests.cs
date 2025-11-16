namespace Microsoft.ApplicationInsights.NLogTarget.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.NLogTarget;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using NLog;
    using NLog.Config;
    using WireMock.RequestBuilders;
    using WireMock.ResponseBuilders;
    using WireMock.Server;

    [TestClass]
    public class ApplicationInsightsIntegrationTests
    {
        private WireMockServer mockServer;
        private string mockIngestionEndpoint;

        [TestInitialize]
        public void Initialize()
        {
            this.mockServer = WireMockServer.Start();
            this.mockIngestionEndpoint = this.mockServer.Url;

            // Set up default response for ingestion endpoint
            this.mockServer
                .Given(Request.Create().WithPath("/v2.1/track").UsingPost())
                .RespondWith(Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("{\"itemsReceived\":1,\"itemsAccepted\":1,\"errors\":[]}"));
        }

        [TestCleanup]
        public void Cleanup()
        {
            this.mockServer?.Stop();
            this.mockServer?.Dispose();
            NLog.GlobalDiagnosticsContext.Clear();
        }

        [TestMethod]
        [TestCategory("Integration")]
        public async Task LogMessageIsSentToIngestionEndpoint()
        {
            var connectionString = $"InstrumentationKey=12345678-1234-1234-1234-123456789012;IngestionEndpoint={this.mockIngestionEndpoint}/";
            var logger = this.CreateLoggerWithRealChannel(connectionString);

            logger.Info("Integration test message");

            // Flush to ensure message is sent
            logger.Factory.Flush();

            // Wait a bit for async processing
            await Task.Delay(1000).ConfigureAwait(false);

            // Verify request was received
            var requests = this.mockServer.LogEntries.ToList();
            Assert.IsTrue(requests.Count > 0, "No requests received by mock server");

            var trackRequest = requests.FirstOrDefault(r => r.RequestMessage.Path.Contains("track"));
            Assert.IsNotNull(trackRequest, "No track request found");
            Assert.AreEqual("POST", trackRequest.RequestMessage.Method);
        }

        [TestMethod]
        [TestCategory("Integration")]
        public async Task LogMessageContentIsCorrect()
        {
            var connectionString = $"InstrumentationKey=12345678-1234-1234-1234-123456789012;IngestionEndpoint={this.mockIngestionEndpoint}/";
            var logger = this.CreateLoggerWithRealChannel(connectionString);

            var testMessage = "Test log message with unique content";
            logger.Info(testMessage);

            // Flush to ensure message is sent
            logger.Factory.Flush();

            // Wait a bit for async processing
            await Task.Delay(1000).ConfigureAwait(false);

            // Verify request was received
            var requests = this.mockServer.LogEntries.ToList();
            var trackRequest = requests.FirstOrDefault(r => r.RequestMessage.Path.Contains("track"));
            Assert.IsNotNull(trackRequest, "No track request found");

            // Parse the request body
            var body = trackRequest.RequestMessage.Body;
            Assert.IsNotNull(body, "Request body is null");
            Assert.IsTrue(body.Contains(testMessage, StringComparison.Ordinal), $"Request body does not contain expected message: {testMessage}");
        }

        [TestMethod]
        [TestCategory("Integration")]
        public async Task LogWithDifferentSeverityLevelsAreSent()
        {
            var connectionString = $"InstrumentationKey=12345678-1234-1234-1234-123456789012;IngestionEndpoint={this.mockIngestionEndpoint}/";
            var logger = this.CreateLoggerWithRealChannel(connectionString);

            logger.Trace("Trace message");
            logger.Debug("Debug message");
            logger.Info("Info message");
            logger.Warn("Warning message");
            logger.Error("Error message");

            // Flush to ensure messages are sent
            logger.Factory.Flush();

            // Wait a bit for async processing
            await Task.Delay(1000).ConfigureAwait(false);

            // Verify requests were received
            var requests = this.mockServer.LogEntries.ToList();
            var trackRequests = requests.Where(r => r.RequestMessage.Path.Contains("track")).ToList();
            
            // Should have received multiple track requests (may be batched)
            Assert.IsTrue(trackRequests.Count > 0, "No track requests received");

            // Verify all log messages are in the requests
            var allBodies = string.Join(" ", trackRequests.Select(r => r.RequestMessage.Body));
            Assert.IsTrue(allBodies.Contains("Trace message", StringComparison.Ordinal), "Trace message not found");
            Assert.IsTrue(allBodies.Contains("Debug message", StringComparison.Ordinal), "Debug message not found");
            Assert.IsTrue(allBodies.Contains("Info message", StringComparison.Ordinal), "Info message not found");
            Assert.IsTrue(allBodies.Contains("Warning message", StringComparison.Ordinal), "Warning message not found");
            Assert.IsTrue(allBodies.Contains("Error message", StringComparison.Ordinal), "Error message not found");
        }

        [TestMethod]
        [TestCategory("Integration")]
        public async Task LogWithExceptionIsSent()
        {
            var connectionString = $"InstrumentationKey=12345678-1234-1234-1234-123456789012;IngestionEndpoint={this.mockIngestionEndpoint}/";
            var logger = this.CreateLoggerWithRealChannel(connectionString);

            var exceptionMessage = "Test exception message";
            try
            {
                throw new InvalidOperationException(exceptionMessage);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exception occurred during test");
            }

            // Flush to ensure message is sent
            logger.Factory.Flush();

            // Wait a bit for async processing
            await Task.Delay(1000).ConfigureAwait(false);

            // Verify request was received
            var requests = this.mockServer.LogEntries.ToList();
            var trackRequest = requests.FirstOrDefault(r => r.RequestMessage.Path.Contains("track"));
            Assert.IsNotNull(trackRequest, "No track request found");

            var body = trackRequest.RequestMessage.Body;
            Assert.IsTrue(body.Contains(exceptionMessage, StringComparison.Ordinal), $"Request body does not contain exception message: {exceptionMessage}");
            Assert.IsTrue(body.Contains("InvalidOperationException", StringComparison.Ordinal), "Request body does not contain exception type");
        }

        [TestMethod]
        [TestCategory("Integration")]
        public async Task LogWithCustomPropertiesAreSent()
        {
            var connectionString = $"InstrumentationKey=12345678-1234-1234-1234-123456789012;IngestionEndpoint={this.mockIngestionEndpoint}/";
            var logger = this.CreateLoggerWithRealChannel(connectionString);

            var eventInfo = new LogEventInfo(LogLevel.Info, "TestLogger", "Message with properties");
            eventInfo.Properties["CustomProperty1"] = "CustomValue1";
            eventInfo.Properties["CustomProperty2"] = "CustomValue2";
            logger.Log(eventInfo);

            // Flush to ensure message is sent
            logger.Factory.Flush();

            // Wait a bit for async processing
            await Task.Delay(1000).ConfigureAwait(false);

            // Verify request was received
            var requests = this.mockServer.LogEntries.ToList();
            var trackRequest = requests.FirstOrDefault(r => r.RequestMessage.Path.Contains("track"));
            Assert.IsNotNull(trackRequest, "No track request found");

            var body = trackRequest.RequestMessage.Body;
            Assert.IsTrue(body.Contains("CustomProperty1", StringComparison.Ordinal), "Request body does not contain CustomProperty1");
            Assert.IsTrue(body.Contains("CustomValue1", StringComparison.Ordinal), "Request body does not contain CustomValue1");
            Assert.IsTrue(body.Contains("CustomProperty2", StringComparison.Ordinal), "Request body does not contain CustomProperty2");
            Assert.IsTrue(body.Contains("CustomValue2", StringComparison.Ordinal), "Request body does not contain CustomValue2");
        }

        [TestMethod]
        [TestCategory("Integration")]
        public async Task LogRequestContainsInstrumentationKey()
        {
            var instrumentationKey = "12345678-1234-1234-1234-123456789012";
            var connectionString = $"InstrumentationKey={instrumentationKey};IngestionEndpoint={this.mockIngestionEndpoint}/";
            var logger = this.CreateLoggerWithRealChannel(connectionString);

            logger.Info("Test message");

            // Flush to ensure message is sent
            logger.Factory.Flush();

            // Wait a bit for async processing
            await Task.Delay(1000).ConfigureAwait(false);

            // Verify request was received
            var requests = this.mockServer.LogEntries.ToList();
            var trackRequest = requests.FirstOrDefault(r => r.RequestMessage.Path.Contains("track"));
            Assert.IsNotNull(trackRequest, "No track request found");

            var body = trackRequest.RequestMessage.Body;
            Assert.IsTrue(body.Contains(instrumentationKey, StringComparison.Ordinal), $"Request body does not contain instrumentation key: {instrumentationKey}");
        }

        private Logger CreateLoggerWithRealChannel(string connectionString)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope - Caller is responsible for disposal
            var target = new ApplicationInsightsTarget
            {
                ConnectionString = connectionString
            };
#pragma warning restore CA2000 // Dispose objects before losing scope - Caller is responsible for disposal

            var rule = new LoggingRule("*", LogLevel.Trace, target);
            var config = new LoggingConfiguration();
            config.AddTarget("AITarget", target);
            config.LoggingRules.Add(rule);

            LogFactory logFactory = new()
            {
                Configuration = config
            };

            return logFactory.GetLogger("IntegrationTestLogger");
        }
    }
}
