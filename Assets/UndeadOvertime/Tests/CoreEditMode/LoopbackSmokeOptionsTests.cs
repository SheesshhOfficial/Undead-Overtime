using NUnit.Framework;

namespace UndeadOvertime.Core.Tests
{
    public sealed class LoopbackSmokeOptionsTests
    {
        private static string[] Args(string role = "host", string port = "7777", string run = "0123456789abcdef0123456789abcdef") =>
            new[] { "game.exe", "-batchmode", "-uo-smoke-role", role, "-uo-smoke-port", port,
                "-uo-smoke-run", run, "-uo-smoke-revision", "0123456789abcdef0123456789abcdef01234567", "-logFile", "example.log" };
        [TestCase("host", true)] [TestCase("client", false)]
        public void ExplicitRoleFlagsParseWithoutAHostAddressOrOutputPath(string role, bool host)
        {
            Assert.That(LoopbackSmokeOptions.TryParse(Args(role), out var options, out _), Is.True);
            Assert.That(options.Mission, Is.False); Assert.That(options.Host, Is.EqualTo(host)); Assert.That(options.Port, Is.EqualTo(7777));
        }
        [TestCase("range", false)] [TestCase("mission", true)]
        public void ExplicitSupportedScenarioIsBounded(string mode, bool mission)
        {
            var args = new System.Collections.Generic.List<string>(Args()); args.Add("-uo-smoke-mode"); args.Add(mode);
            Assert.That(LoopbackSmokeOptions.TryParse(args.ToArray(), out var options, out _), Is.True);
            Assert.That(options.Mission, Is.EqualTo(mission));
        }
        [TestCase("anything")] [TestCase("Mission")] [TestCase("../file")]
        public void InvalidScenarioIsRejected(string mode)
        {
            var args = new System.Collections.Generic.List<string>(Args()); args.Add("-uo-smoke-mode"); args.Add(mode);
            Assert.That(LoopbackSmokeOptions.TryParse(args.ToArray(), out _, out _), Is.False);
        }
        [Test]
        public void NormalPlayerArgumentsDoNotEnableAutomation()
        {
            Assert.That(LoopbackSmokeOptions.Requested(new[] { "game.exe", "-batchmode", "-logFile", "log.txt" }), Is.False);
            Assert.That(LoopbackSmokeOptions.Requested(null), Is.False);
            Assert.That(LoopbackSmokeOptions.TryParse(null, out _, out _), Is.False);
        }
        [TestCase("server", "7777", "0123456789abcdef0123456789abcdef")]
        [TestCase("host", "80", "0123456789abcdef0123456789abcdef")]
        [TestCase("host", "65536", "0123456789abcdef0123456789abcdef")]
        [TestCase("host", "7777", "../unsafe")]
        public void InvalidRolePortAndRunIdentifiersAreRejected(string role, string port, string run)
        { Assert.That(LoopbackSmokeOptions.TryParse(Args(role, port, run), out _, out _), Is.False); }
        [Test]
        public void DuplicateAndUnknownFlagsDoNotStartATest()
        {
            var args = new System.Collections.Generic.List<string>(Args()); args.Add("-uo-smoke-role"); args.Add("client");
            Assert.That(LoopbackSmokeOptions.TryParse(args.ToArray(), out _, out _), Is.False);
            args = new System.Collections.Generic.List<string>(Args()); args.Add("-uo-smoke-address"); args.Add("8.8.8.8");
            Assert.That(LoopbackSmokeOptions.TryParse(args.ToArray(), out _, out _), Is.False);
        }
    }
}
