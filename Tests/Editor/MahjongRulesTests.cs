using NUnit.Framework;
using Mahjong.Rules.Diagnostics;

public class MahjongRulesTests
{
    [Test]
    public void RulesSelfTestPasses()
    {
        var report = RulesSelfTest.RunAll();
        Assert.IsEmpty(report.Failures,
            "Rules self-test failures:\n" + string.Join("\n", report.Failures));
    }
}
