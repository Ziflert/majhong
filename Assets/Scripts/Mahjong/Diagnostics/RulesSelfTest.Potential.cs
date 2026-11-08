using System;
using System.Linq;
using Mahjong.Rules;

namespace Mahjong.Rules.Diagnostics
{
    public static partial class RulesSelfTest
    {
        private static void HandPotentialRules(SelfTestReport report)
        {
            var session = new GameSession(null, RuleVariant.Sports);
            session.StartNextHand(12345);
            var state = session.State!;
            var line = HandPotential.Evaluate(state, state.Dealer);
            report.Check(line.MaximumPoints >= 0, "potential maximum is never negative");
            report.Check(line.ExpectedPoints >= 0, "potential forecast is never negative");
            report.Check(line.LiveCopies >= 0 && line.LiveCopies <= 136,
                "potential live copies stay within the physical wall");
            report.Check(line.WinningFaces.All(type => TileTypes.All.Contains(type)),
                "potential waits are real tile faces");

            var noHidden = HandPotential.Evaluate(state, state.Dealer, Array.Empty<TileType>());
            report.Equal(line.MaximumPoints, noHidden.MaximumPoints,
                "potential does not inspect hidden opponent hands");

            var settings = new Controls.GameSettings();
            report.Equal(HandPotentialMode.Off, settings.Potential, "potential overlay defaults off");
            settings.Potential = HandPotentialMode.Forecast;
            var text = Controls.SettingsFile.Save(settings);
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mahjong-potential-settings.txt");
            System.IO.File.WriteAllText(path, text);
            var restored = Controls.SettingsFile.Read(path);
            report.Equal(HandPotentialMode.Forecast, restored.Potential,
                "potential display mode survives the settings file");
            try { System.IO.File.Delete(path); } catch { }
        }
    }
}
