using System;
using UnityEngine;

public static class ParkingLightLogFilter
{
    private sealed class FilterHandler : ILogHandler
    {
        private readonly ILogHandler innerHandler;

        public FilterHandler(ILogHandler innerHandler)
        {
            this.innerHandler = innerHandler;
        }

        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
        {
            string message = string.Format(format, args);
            if (IsLightMessage(message))
            {
                return;
            }

            innerHandler.LogFormat(logType, context, format, args);
        }

        public void LogException(Exception exception, UnityEngine.Object context)
        {
            if (exception != null && IsLightMessage(exception.Message))
            {
                return;
            }

            innerHandler.LogException(exception, context);
        }

        private static bool IsLightMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return false;
            }

            string value = message.ToLowerInvariant();
            return value.Contains("lightmap")
                || value.Contains("lighting")
                || value.Contains("light probe")
                || value.Contains("shadow map")
                || value.Contains("라이트")
                || value.Contains("조명");
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (Debug.unityLogger.logHandler is FilterHandler)
        {
            return;
        }

        Debug.unityLogger.logHandler = new FilterHandler(Debug.unityLogger.logHandler);
    }
}
