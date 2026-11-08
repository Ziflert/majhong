using System;
using Mahjong.Rules.Diagnostics;

namespace Mahjong.RulesCheck
{
    /// <summary>
    /// Headless runner for the shared rules self-test. The same checks run inside Unity
    /// through Assets/Tests/Editor/MahjongRulesTests.cs.
    ///
    ///   dotnet run --project Tools/RulesCheck                # every group
    ///   dotnet run --project Tools/RulesCheck -- "net: "     # only the groups whose name matches
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            var filter = args.Length > 0 ? string.Join(' ', args) : null;
            var report = RulesSelfTest.RunAll(filter);

            foreach (var name in report.Passed) Console.WriteLine($"  ok   {name}");
            foreach (var failure in report.Failures) Console.WriteLine($"  FAIL {failure}");

            Console.WriteLine();
            if (report.Passed.Count == 0 && report.Failures.Count == 0)
            {
                Console.WriteLine($"nothing matched the filter '{filter}'");
                return 2;
            }

            Console.WriteLine(report.PassedAll
                ? $"OK - {report.Checks} checks passed in {report.Passed.Count} test groups"
                : $"FAILED - {report.Failures.Count} of {report.Passed.Count + report.Failures.Count} test groups failed "
                  + $"({report.Checks} checks ran)");

            return report.PassedAll ? 0 : 1;
        }
    }
}
