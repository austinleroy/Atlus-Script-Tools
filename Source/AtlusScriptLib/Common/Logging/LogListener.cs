﻿using System;

namespace AtlusScriptLib.Common.Logging
{
    public abstract class LogListener
    {
        public string ChannelName { get; }

        public LogListener()
        {

        }

        public LogListener( string channelName )
        {
            ChannelName = channelName;
        }

        public void Subscribe( Logger logger )
        {
            logger.LogEvent += OnLog;
        }

        public void Unsubscribe( Logger logger )
        {
            logger.LogEvent -= OnLog;
        }

        protected abstract void OnLog( object sender, LogEventArgs e );
    }

    public class ConsoleLogListener : LogListener
    {
        public bool UseColors { get; set; }

        public ConsoleLogListener( bool useColors ) : base()
        {
            UseColors = useColors;
        }

        public ConsoleLogListener( string channelName, bool useColors ) : base( channelName )
        {
            UseColors = useColors;
        }

        protected override void OnLog( object sender, LogEventArgs e )
        {
            ConsoleColor prevColor = 0;

            if ( UseColors )
            {
                prevColor = Console.ForegroundColor;
                Console.ForegroundColor = GetConsoleColorForSeverityLevel( e.Level );
            }

            Console.WriteLine($"{DateTime.Now} {e.ChannelName} {e.Level}: {e.Message}");

            if ( UseColors )
            {
                Console.ForegroundColor = prevColor;
            }
        }

        private ConsoleColor GetConsoleColorForSeverityLevel( LogLevel level )
        {
            switch ( level )
            {
                case LogLevel.Debug:
                    return ConsoleColor.White;
                case LogLevel.Info:
                    return ConsoleColor.Green;
                case LogLevel.Warning:
                    return ConsoleColor.Yellow;
                case LogLevel.Error:
                    return ConsoleColor.Red;
                case LogLevel.Fatal:
                    return ConsoleColor.DarkRed;
                default:
                    return ConsoleColor.White;
            }
        }
    }
}
