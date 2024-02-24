// <copyright file="AdapterHelper.cs" company="Microsoft">
// Copyright © Microsoft. All Rights Reserved.
// </copyright>

namespace Microsoft.ApplicationInsights.Tracing.Tests
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    using static System.Globalization.CultureInfo;

    public class AdapterHelper : IDisposable
    {
        public string InstrumentationKey { get; }

        public string ConnectionString { get; }

#if NET462 || NET472 || NET48
        private static readonly string applicationInsightsConfigFilePath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ApplicationInsights.config");
#else
        private static readonly string applicationInsightsConfigFilePath =
            Path.Combine(Path.GetDirectoryName(typeof(AdapterHelper).GetTypeInfo().Assembly.Location)!, "ApplicationInsights.config");
#endif

        public AdapterHelper(string connectionString = "InstrumentationKey=F8474271-D231-45B6-8DD4-D344C309AE69;IngestionEndpoint=https://westeurope.in.applicationinsights.azure.example.com/;LiveEndpoint=https://westeurope.livediagnostics.monitor.azure.example.com/")
        {
            this.ConnectionString = connectionString;
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                this.InstrumentationKey = (from item in connectionString.Split(';')
                                           let parts = item.Split('=')
                                           where parts.Length == 2 && parts[0].Equals("InstrumentationKey", StringComparison.OrdinalIgnoreCase)
                                           select parts[1]).FirstOrDefault();
            }

            string configuration = string.Format(InvariantCulture,
                                    @"<?xml version=""1.0"" encoding=""utf-8"" ?>
                                     <ApplicationInsights xmlns=""http://schemas.microsoft.com/ApplicationInsights/2013/Settings"">
                                        <ConnectionString>{0}</ConnectionString>
                                     </ApplicationInsights>",
                                     connectionString);

            File.WriteAllText(applicationInsightsConfigFilePath, configuration);
            this.Channel = new CustomTelemetryChannel();
        }

        internal CustomTelemetryChannel Channel { get; private set; }

        public static void ValidateChannel(AdapterHelper adapterHelper, string instrumentationKey, int expectedTraceCount)
        {
            if (adapterHelper == null)
            {
                throw new ArgumentNullException(nameof(adapterHelper));
            }

            // Validate that the channel received traces
            ITelemetry[] sentItems = null;
            int totalMillisecondsToWait = (int)TimeSpan.FromMinutes(1).TotalMilliseconds;
            const int IterationMilliseconds = 250;

            while (totalMillisecondsToWait > 0)
            {
                sentItems = adapterHelper.Channel.SentItems;
                if (sentItems.Length > 0)
                {
                    ITelemetry telemetry = sentItems.FirstOrDefault();

                    Assert.AreEqual(expectedTraceCount, sentItems.Length, "All messages are received by the channel");
                    Assert.IsNotNull(telemetry, "telemetry collection is not null");
                    Assert.AreEqual(instrumentationKey, telemetry.Context.InstrumentationKey, "The correct instrumentation key was used");
                    break;
                }

                Thread.Sleep(IterationMilliseconds);
                totalMillisecondsToWait -= IterationMilliseconds;
            }

            Assert.IsNotNull(sentItems);
            Assert.IsTrue(sentItems.Length > 0);
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.Channel.Dispose();

                if (File.Exists(applicationInsightsConfigFilePath))
                {
                    File.Delete(applicationInsightsConfigFilePath);
                }
            }
        }
    }
}
