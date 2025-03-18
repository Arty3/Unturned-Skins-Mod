using System;

using static SDG.Unturned.Logs;

namespace SkinsModule
{
    internal static class ModuleLogger
    {
        private const string _tag = "[SKIN MODULE]";

        public static void Log(string message)
        {
            printLine($"{_tag} INFO: {message}");
        }

        public static void Warn(string message)
        {
            printLine($"{_tag} WARN: {message}");
        }

        public static void Error(string message)
        {
            printLine($"{_tag} ERROR: {message}");
        }

        public static void MissingReference(string message, Exception e)
        {
            Error($"{_tag} ERROR: {message} " +
                  "Possibly outdated assembly.\n" +
                  $"Exception info: {e.Message}");
        }
    }
}
