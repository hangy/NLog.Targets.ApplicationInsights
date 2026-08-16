# NLog Target for Application Insights

This is a logger for sending your NLog logs to Application Insights. It is an inofficial fork of [Microsoft.ApplicationInsights.NLog](https://github.com/microsoft/ApplicationInsights-dotnet/tree/c9d420224a06d27ee74fba4b41cad7460bd63bd0/LOGGING/src/NLogTarget) with the addition of some community contributions that did not make it into the official distribution.

## Deprecation Notice

This package is **scheduled for deprecation**. The official [Microsoft.ApplicationInsights.NLogTarget](https://www.nuget.org/packages/Microsoft.ApplicationInsights.NLogTarget) package now includes equivalent functionality (currently in beta as 3.x). Once the official package is released as stable, this package will be deprecated and is no longer maintained.

For new projects, please use the official package instead. Existing projects are encouraged to migrate when the official stable release is available.

## Installation

To use the NLog target, follow these steps:

1. Add a reference to the [`hangy.NLog.Targets.ApplicationInsights`](https://www.nuget.org/packages/hangy.NLog.Targets.ApplicationInsights) NuGet package.
2. In your `NLog.config`, add the assembly reference to the extension, and set up a logger using your connectionstring

   ```xml
   <nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
       <extensions>
           <add assembly="hangy.NLog.Targets.ApplicationInsights" />
       </extensions>
       <targets async="true">
           <target xsi:type="ApplicationInsightsTarget" name="aiTarget">
               <connectionString>Your_ApplicationInsights_ConnectionString</connectionString> <!-- Only required if not using ApplicationInsights.config -->
               <contextproperty name="threadid" layout="${threadid}" /> <!-- Can be repeated with more context -->
           </target>
       </targets>
       <rules>
           <logger name="*" minlevel="Trace" writeTo="aiTarget" />
       </rules>
   </nlog>
   ```

## Known Limitations

1. Complex objects are flattened when written to `customDimensions`. [#8](https://github.com/hangy/NLog.Targets.ApplicationInsights/issues/8)
1. Properties with the same name are overwritten. For example, if you have a GDC value named `Name`, and a log event property of the same name, the GDC value will be logged. [#10](https://github.com/hangy/NLog.Targets.ApplicationInsights/issues/10)

## Activity Tracing

By default, the ApplicationInsightsTarget logs the trace and span information from the current. If you want to disable this behaviour, set the layout as follows:

```xml
<target xsi:type="ApplicationInsightsTarget" name="aiTarget" spanId="" traceId="">
```
