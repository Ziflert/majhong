using System;
using Mahjong.Rules.Diagnostics;

namespace Mahjong.RulesCheck
{
    /// <summary>
    /// Headless runner for the shared rules self-test. The same checks run inside Unity
    /// through Tests/Editor/MahjongRulesTests.cs.
    ///
    ///   dotnet run --project Tools/RulesCheck
    /// </summary>
    public static class Program
    {
        public static int Main()
        {
            var report = RulesSelfTest.RunAll();

            foreach (var name in report.Passed) Console.WriteLine($"  ok   {name}");
            foreach (var failure in report.Failures) Console.WriteLine($"  FAIL {failure}");

            Console.WriteLine();
            Console.WriteLine(report.PassedAll
                ? $"OK - {report.Checks} checks passed in {report.Passed.Count} test groups"
                : $"FAILED - {report.Failures.Count} of {report.Passed.Count + report.Failures.Count} test groups failed");

            return report.PassedAll ? 0 : 1;
        }
    }
}
