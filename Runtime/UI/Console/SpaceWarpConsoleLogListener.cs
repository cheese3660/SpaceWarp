﻿using System;
using System.Collections.Generic;
using ReduxLib.Logging;
using ILogger = ReduxLib.Logging.ILogger;

namespace SpaceWarp.UI.Console;

internal sealed class SpaceWarpConsoleLogListener
{	
    internal static readonly List<string> DebugMessages = new();
    internal static readonly List<LogInfo> LogMessages = new();   
    private readonly UI _uiModule;

    public static event Action<string> OnNewMessage;
    public static event Action<LogInfo> OnNewLog;

    public SpaceWarpConsoleLogListener(UI uiModule)
    {
        _uiModule = uiModule;
        // We now need to add this as a log listener
        ReduxLib.ReduxLib.ReduxLogProvider.OnLog += LogEvent;
    }	

    public void LogEvent(LogLevel level, ILogger source, object logged)
    {
        var info = new LogInfo { DateTime = DateTime.Now, Level = level, Source = source, Data = logged};
        DebugMessages.Add(BuildMessage(TimestampMessage(), level, logged, source));
        LogMessages.Add(info);
        // Notify all listeners that a new message has been added
        OnNewMessage?.Invoke(DebugMessages[^1]);
        OnNewLog?.Invoke(info);

        LogMessageJanitor();
    }	

    public struct LogInfo
    {
        public DateTime DateTime;
        public LogLevel Level;
        public ILogger Source;
        public object Data;
    }

    public void Dispose()	
    {	
        DebugMessages.Clear();	
    }	

    private void LogMessageJanitor()	
    {	
        var configDebugMessageLimit = _uiModule.ConfigDebugMessageLimit.Value;	
        if (DebugMessages.Count > configDebugMessageLimit)	
            DebugMessages.RemoveRange(0, DebugMessages.Count - configDebugMessageLimit);
        if (LogMessages.Count > configDebugMessageLimit)
            LogMessages.RemoveRange(0, LogMessages.Count - configDebugMessageLimit);
    }	

    private string TimestampMessage()	
    {	
        return _uiModule.ConfigShowTimeStamps.Value	
            ? "[" + DateTime.Now.ToString((string)_uiModule.ConfigTimeStampFormat.Value) + "] "	
            : "";	
    }	

    private static string BuildMessage(string timestamp, LogLevel level, object data, ILogger source)	
    {	
        return level == LogLevel.None	
            ? $"{timestamp}[{source.Name}] {data}"	
            : $"{timestamp}[{level} : {source.Name}] {data}";	
    }	
}