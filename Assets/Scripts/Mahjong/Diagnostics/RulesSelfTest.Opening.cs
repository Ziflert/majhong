using System;
using Mahjong.Controls;

namespace Mahjong.Rules.Diagnostics
{
    /// <summary>
    /// Checks of the opening of a hand — two dice throws, the break and then the deal (3.5.7.5) — as
    /// the table shows it. The engine deals at once, so the opening is timing and nothing else:
    /// <see cref="HandOpening"/> lives in <c>Controls</c>, away from Unity, and is checked here without
    /// a single frame. The play-mode tests then only have to look at the screen.
    /// </summary>
    public static partial class RulesSelfTest
    {
        private static void OpeningGroups(SelfTestReport report)
        {
            Run(report, "opening: two throws, the break and the deal keep their beats", () => OpeningBeats(report));
            Run(report, "opening: the opening and the engine break the same wall", () => OpeningMatchesEngine(report));
        }

        /// <summary>Seconds of the four beats from the rules' order: throw, throw, break, deal.</summary>
        private static void OpeningBeats(SelfTestReport report)
        {
            report.Equal(4, HandOpening.BeatSeconds.Length, "the opening has four beats");
            report.Equal(0.9, HandOpening.BeatSeconds[(int)OpeningStep.FirstRoll], "the first throw takes 0.9 s");
            report.Equal(0.9, HandOpening.BeatSeconds[(int)OpeningStep.SecondRoll], "the second throw takes 0.9 s");
            report.Equal(0.6, HandOpening.BeatSeconds[(int)OpeningStep.Break], "the break takes 0.6 s");
            report.Equal(1.1, HandOpening.BeatSeconds[(int)OpeningStep.Deal], "the deal takes 1.1 s");

            var first = new DiceRoll(3, 5);
            var second = new DiceRoll(2, 6);
            var opening = new HandOpening(first, second, 2, first.Sum + second.Sum);

            // A fresh opening is at its first throw and no tile may leave the wall yet.
            report.Equal(OpeningStep.FirstRoll, opening.Step, "the first throw opens the hand");
            report.Check(opening.IsRunning && !opening.IsDone, "a fresh opening is still running");
            report.Equal(0d, opening.Progress, "nothing of the first beat has passed");
            Near(report, 0.9, opening.StepLength, "the running beat has its own length");
            Near(report, 2.4, opening.TimeUntilDeal, "the deal waits for both throws and the break");

            // Time spent inside a beat does not change the beat; only leaving it does.
            report.Check(!opening.Advance(0.3), "a third of a beat is not a new beat");
            Near(report, 1d / 3d, opening.Progress, "the beat is a third done");
            Near(report, 2.1, opening.TimeUntilDeal, "and the deal is that much closer");

            // A beat is crossed, not hit exactly: the seconds of a frame never add up to 0.9.
            report.Check(opening.Advance(0.65), "finishing the first throw starts the second one");
            report.Equal(OpeningStep.SecondRoll, opening.Step, "the second throw follows the first");
            Near(report, 0.05, opening.ElapsedInStep, "the crossing carries into the next beat");
            Near(report, 1.45, opening.TimeUntilDeal, "only the rest of the break and the deal are ahead");

            report.Check(opening.Advance(0.9), "the second throw hands over to the break");
            report.Equal(OpeningStep.Break, opening.Step, "the break follows the throws");
            Near(report, 0.55, opening.TimeUntilDeal, "the deal waits for the rest of the break alone");

            report.Check(opening.Advance(0.6), "the break hands over to the deal");
            report.Equal(OpeningStep.Deal, opening.Step, "the deal is the last beat");
            Near(report, 0d, opening.TimeUntilDeal, "no tile is still waiting to be taken");
            report.Check(opening.IsRunning, "the deal itself is not the end of the opening");
            Near(report, 1.1, opening.StepLength, "the deal has a length of its own");

            report.Check(opening.Advance(1.1), "the last beat ends the opening");
            report.Check(opening.IsDone, "the opening is over");
            report.Check(!opening.IsRunning, "a finished opening is not running");
            report.Equal(1d, opening.Progress, "a finished opening is all the way through");
            Near(report, 0d, opening.StepRemaining, "nothing of it is left");
            report.Check(!opening.Advance(1d), "time after the end changes nothing");

            // One frame-sized tick per beat, as the table drives it: exactly one beat change each.
            var ticking = new HandOpening(first, second, 2, 11);
            var changes = 0;
            var beats = 0;
            for (var frame = 0; frame < 60 * 4 && ticking.IsRunning; frame++)
            {
                if (ticking.Advance(1d / 60d)) changes++;
                beats++;
            }

            report.Check(ticking.IsDone, "sixty ticks a second finish the opening");
            report.Equal(4, changes, "each of the four beats is reported once");
            // 3.5 seconds at sixty ticks a second, to within the one tick that rounds off the sum.
            report.Check(beats is >= 210 and <= 211, $"the opening lasts its four beats: {beats} ticks");

            // A clock that jumps (a stalled frame, a capture run) must land at the end, not somewhere
            // in the middle with a stale elapsed time.
            var jumped = new HandOpening(first, second, 2, 11);
            report.Check(jumped.Advance(100d), "a jump past the end changes a beat");
            report.Check(jumped.IsDone, "and it lands at the end of the opening");
            Near(report, 0d, jumped.ElapsedInStep, "a finished opening holds no leftover time");
            Near(report, 0d, jumped.TimeUntilDeal, "and nothing waits for the deal");

            // Negative and zero time is not time: a paused table must not move the opening.
            var paused = new HandOpening(first, second, 2, 11);
            paused.Advance(0.4);
            var before = paused.ElapsedInStep;
            report.Check(!paused.Advance(0d), "zero seconds is not a step");
            report.Check(!paused.Advance(-5d), "time does not run backwards in the opening");
            Near(report, before, paused.ElapsedInStep, "a paused opening stands still");

            // Pace: the beats are scaled, the order is not. A pace below one is a shorter opening.
            var fast = new HandOpening(first, second, 2, 11) { Pace = 0.5d };
            Near(report, 0.45, fast.StepLength, "half the pace halves the first beat");
            Near(report, 1.2, fast.TimeUntilDeal, "and halves what the deal waits for");
            // 1.75 seconds in tenths of a second: eighteen ticks, and the pace changes nothing else.
            var fastTicks = 0;
            while (fast.IsRunning && fastTicks++ < 100) fast.Advance(0.1);
            report.Check(fast.IsDone, "a faster opening still finishes");
            report.Check(fastTicks is >= 17 and <= 18, $"a faster opening keeps the four beats: {fastTicks} ticks");

            var slow = new HandOpening(first, second, 2, 11) { Pace = 2d };
            Near(report, 1.8, slow.StepLength, "twice the pace doubles the first beat");
            Near(report, 4.8, slow.TimeUntilDeal, "and doubles what the deal waits for");

            // MAHJONG_OPENING_SPEED is a speed, so the table's number goes the other way.
            Near(report, 1d, HandOpening.PaceFromSpeed(1d), "normal speed is the table's own pace");
            Near(report, 0.5d, HandOpening.PaceFromSpeed(2d), "twice the speed is half the pace");
            Near(report, 4d, HandOpening.PaceFromSpeed(0.25d), "a quarter of the speed is four times the pace");
            Near(report, 0d, HandOpening.PaceFromSpeed(0d), "zero speed means no opening at all");
            Near(report, 0d, HandOpening.PaceFromSpeed(-3d), "a negative speed is not a slower opening");

            // Skip is how a capture run and a resumed hand reach the end without waiting.
            var skipped = new HandOpening(first, second, 2, 11);
            skipped.Skip();
            report.Check(skipped.IsDone, "Skip ends the opening at once");
            Near(report, 0d, skipped.TimeUntilDeal, "a skipped opening waits for nothing");
            report.Equal(1d, skipped.Progress, "a skipped opening is all the way through");

            // And a table holds this state before its first hand: nothing that reads the opening has
            // to guard against a missing one.
            report.Check(HandOpening.Finished().IsDone, "a table without a hand holds a finished opening");
            Near(report, 0d, HandOpening.Finished().TimeUntilDeal, "and nothing waits for a deal");
        }

        /// <summary>
        /// The opening is built from the values the engine rolled; if the two ever disagree, the table
        /// would show a gap in the wall that is not where the tiles are actually being taken from.
        /// </summary>
        private static void OpeningMatchesEngine(SelfTestReport report)
        {
            for (var seed = 0; seed < 40; seed++)
            {
                var state = new GameState();
                state.StartHand(seed % 4, seed);

                var opening = new HandOpening(state.DealerRoll, state.BreakRoll, state.DiceThrower, state.BreakStacks);
                report.Equal(state.DealerRoll, opening.First, $"seed {seed}: the first throw is the engine's");
                report.Equal(state.BreakRoll, opening.Second, $"seed {seed}: the second throw is the engine's");
                report.Equal(state.DiceThrower, opening.Thrower, $"seed {seed}: the same seat throws again");
                report.Equal(state.DealerRoll.Sum + state.BreakRoll.Sum, opening.BreakStacks,
                    $"seed {seed}: the break counts both throws");

                // Rule 3.5.7.5: the break is counted in stacks from the second thrower's right end.
                // His own wall holds eighteen of them, so a longer count runs on into the wall to his
                // left — that is why the side is not always the thrower's own.
                var breakSide = WallGeometry.SlotAt(state.Wall.BreakIndex).Side;
                var ownWall = state.BreakStacks < WallGeometry.StacksPerSide;
                report.Equal(ownWall ? state.DiceThrower : (state.DiceThrower + 3) % 4, breakSide,
                    $"seed {seed}: the break is where counting from the thrower's right end lands");
                var breakIndex = state.Wall.BreakIndex;
                report.Equal(WallGeometry.BreakIndex(opening.Thrower, opening.BreakStacks), breakIndex,
                    $"seed {seed}: the opening points at the break the engine made");

                // The deal the opening makes the table wait for is the deal the engine has done: four
                // hands of thirteen, one more tile for the dealer, and one replacement per flower,
                // all walked clockwise from the break (3.5.7.4).
                var flowers = 0;
                for (var player = 0; player < 4; player++) flowers += state.Players[player].Flowers.Count;
                report.Equal(WallGeometry.TileCount - 53 - flowers, state.Wall.Remaining,
                    $"seed {seed}: the deal took 53 tiles plus a replacement per flower");
                // Flowers are among the dealt tiles: they are replaced from the far end, so the live
                // wall walks exactly the four hands and the dealer's extra tile.
                report.Equal(53,
                    (state.Wall.LiveIndex - breakIndex + WallGeometry.TileCount) % WallGeometry.TileCount,
                    $"seed {seed}: the live wall walked 53 tiles from the break");
                report.Check(!state.Wall.IsStanding(state.Players[0].Concealed[0]),
                    $"seed {seed}: a dealt tile is no longer standing in the wall");
            }
        }

        /// <summary>Doubles are compared with a tolerance: a beat is a sum of floating seconds.</summary>
        private static void Near(SelfTestReport report, double expected, double actual, string message) =>
            report.Check(Math.Abs(expected - actual) < 1e-9, $"{message}: expected {expected}, got {actual}");
    }
}
