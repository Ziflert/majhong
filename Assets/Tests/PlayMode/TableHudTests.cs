using System.Collections;
using Mahjong.Controls;
using Mahjong.Localization;
using Mahjong.Rules;
using Mahjong.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mahjong.PlayTests
{
    public sealed class TableHudTests
    {
        [UnityTest]
        public IEnumerator StructuredHudReadsTheSameStateAndKeepsDebugOptIn()
        {
            var camera = new GameObject("HudTestCamera").AddComponent<Camera>();
            var hud = TableHud.Create(camera.transform);
            var language = Loc.Current;
            try
            {
                Loc.Current = Language.Ru;
                var session = new GameSession();
                session.StartNextHand(12345);
                var settings = new GameSettings();
                var clock = new PlayClock();
                clock.Restart(ClockWindow.Discard);
                hud.Refresh(camera, session, settings, 0, true, clock, "", "diagnostic");
                yield return null;
                StringAssert.Contains(session.State.Wall.Remaining.ToString(), hud.WallCounter.text);
                StringAssert.Contains(Loc.Get("hud.you"), hud.PlayerLabel(0).text);
                StringAssert.Contains(Text.ClockLabel(clock.Window), hud.Hint.text);
                Assert.IsFalse(hud.DebugOpen);
                Assert.IsFalse(hud.DebugText.gameObject.activeSelf);
                Assert.IsFalse(hud.Header.text.Contains("P1="));
                hud.ToggleDebug();
                Assert.IsTrue(hud.DebugText.gameObject.activeSelf);
                Assert.AreEqual("diagnostic", hud.DebugText.text);
                Loc.Current = Language.En;
                hud.Refresh(camera, session, settings, 0, true, clock, "saved", "diagnostic");
                StringAssert.Contains("Wall", hud.WallCounter.text);
                Assert.AreEqual("saved", hud.Notice.text);
                StringAssert.Contains("You", hud.PlayerLabel(0).text);
            }
            finally
            {
                Loc.Current = language;
                Object.Destroy(camera.gameObject);
            }
            yield return null;
        }
    }
}
