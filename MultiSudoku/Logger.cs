using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Serilog;

namespace MultiSudoku
{
    public static class Logger
    {
        public static Serilog.Core.Logger Log;

        public static void Init()
        {
            if (!Directory.Exists("Logs"))
                Directory.CreateDirectory("Logs");
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Log = new LoggerConfiguration().WriteTo.File($"Logs/App{timestamp}.log").CreateLogger();
        }
    }
}