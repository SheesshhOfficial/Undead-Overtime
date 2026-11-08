using System;
using System.Collections.Generic;
using System.Globalization;

namespace UndeadOvertime.Core
{
    /// <summary>Explicit standalone-test flags only. Does not accept an address, executable or output path.</summary>
    public sealed class LoopbackSmokeOptions
    {
        public bool Host { get; private set; }
        public bool Mission { get; private set; }
        public ushort Port { get; private set; }
        public string RunId { get; private set; }
        public string Revision { get; private set; }
        public static bool Requested(string[] args)
        {
            if (args == null) return false;
            foreach (string arg in args) if (arg != null && arg.StartsWith("-uo-smoke-", StringComparison.Ordinal)) return true;
            return false;
        }
        public static bool TryParse(string[] args, out LoopbackSmokeOptions options, out string error)
        {
            options = null; error = null;
            if (!Requested(args)) { error = "No smoke-test flags."; return false; }
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < args.Length; i++)
            {
                string key = args[i];
                if (key == null || !key.StartsWith("-uo-smoke-", StringComparison.Ordinal)) continue;
                if (key != "-uo-smoke-role" && key != "-uo-smoke-port" && key != "-uo-smoke-run" && key != "-uo-smoke-revision" && key != "-uo-smoke-mode")
                { error = "Unknown smoke-test flag."; return false; }
                if (values.ContainsKey(key) || i + 1 >= args.Length || string.IsNullOrEmpty(args[i + 1]) || args[i + 1].StartsWith("-", StringComparison.Ordinal))
                { error = "Duplicate or missing smoke-test value."; return false; }
                values.Add(key, args[++i]);
            }
            if ((values.Count != 4 && values.Count != 5) || !values.TryGetValue("-uo-smoke-role", out string role) || (role != "host" && role != "client") ||
                !values.TryGetValue("-uo-smoke-port", out string portText) || !ushort.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out ushort port) || port < 1024 ||
                !values.TryGetValue("-uo-smoke-run", out string run) || !Hex(run, 32) ||
                !values.TryGetValue("-uo-smoke-revision", out string revision) || !Hex(revision, 40))
            { error = "Expected host/client role, port 1024-65535, 32-hex run ID and exact 40-hex revision."; return false; }
            if (values.TryGetValue("-uo-smoke-mode", out string mode) && mode != "range" && mode != "mission")
            { error = "Smoke mode must be range or mission."; return false; }
            options = new LoopbackSmokeOptions { Mission = mode == "mission", Host = role == "host", Port = port, RunId = run.ToLowerInvariant(), Revision = revision.ToLowerInvariant() };
            return true;
        }
        private static bool Hex(string value, int length)
        {
            if (value == null || value.Length != length) return false;
            foreach (char c in value) if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) return false;
            return true;
        }
    }
}
