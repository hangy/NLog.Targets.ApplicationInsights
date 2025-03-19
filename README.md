# NLog Target for Application Insights

This is a logger for sending your NLog logs to Application Insights. It is an inofficial fork of [Microsoft.ApplicationInsights.NLog](https://github.com/microsoft/ApplicationInsights-dotnet/tree/c9d420224a06d27ee74fba4b41cad7460bd63bd0/LOGGING/src/NLogTarget) with the addition of some community contributions that did not make it into the official distribution.

## Installation

To use the NLog target, follow these steps:

1. Add a reference to the [`hangy.NLog.Targets.ApplicationInsights`](https://www.nuget.org/packages/hangy.NLog.Targets.ApplicationInsights) NuGet package.
2. In your `NLog.config`, add the assembly reference to the extension, and set up a logger using your connectionstring

   ```xml
   <nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
       <extensions>
           <add assembly="hangy.NLog.Targets.ApplicationInsights" />
       </extensions>
       <targets>
           <target xsi:type="ApplicationInsightsTarget" name="aiTarget" includeActivity="true">
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
1. `includeActivity=true` (default: `false`) will _not_ work as expected if the target is wrapped in an asynchronous target wrapper (ie. using `<targets async="true">` du to the `Activity` being scoped to a thread.
