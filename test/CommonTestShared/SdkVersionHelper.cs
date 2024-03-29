﻿namespace Microsoft.ApplicationInsights.CommonTestShared
{
    using System;
    using System.Linq;
    using System.Reflection;

    public static class SdkVersionHelper
    {
        public static string GetExpectedSdkVersion(string prefix, Type loggerType)
        {
#if NET452 || NET46
            var versionStr = loggerType.Assembly.GetCustomAttributes(false).OfType<AssemblyFileVersionAttribute>().First().Version;
#else
            var versionStr = loggerType.GetTypeInfo().Assembly.GetCustomAttributes<AssemblyFileVersionAttribute>().First().Version;
#endif
            var versionParts = new Version(versionStr).ToString().Split('.');

            return prefix + string.Join(".", versionParts[0], versionParts[1], versionParts[2]) + "-" + versionParts[3];
        }
    }
}
