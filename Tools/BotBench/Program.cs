using System;
using System.Collections.Generic;
using System.Linq;
using Mahjong.Bots;
using Mahjong.Rules;

namespace Mahjong.BotBench
{
    /// <summary>
    /// Plays many sessions with mixed bot levels and reports what the levels are worth:
    /// game points, hands won and hands dealt into. Used to tune the danger weights and to
    /// keep the threshold of the regression test honest.
    ///
    ///   dotnet run --project Tools/BotBench -- [sessions]
    /// </summary>
    public static class Program
    {
        private const int HandsPerSession = GameSession.TotalHands;

        public static int Main(string[] args)
        {
            var sessions = args.Length > 0 ? int.Parse(args[0]) : 40;

            Console.WriteLine($"Cautious bots on seats 0 and 2, fast bots on seats 1 and 3 ({sessions} sessions)");
            var mixed = Run(sessions, new[] { BotLevel.Cautious, BotLevel.Fast, BotLevel.Cautious, BotLevel.Fast });
            Report(mixed);

            Console.WriteLine();
            Console.WriteLine($"Every seat plays fast ({sessions} sessions) — baseline");
            var baseline = Run(sessions, new[] { BotLevel.Fast, BotLevel.Fast, BotLevel.Fast, BotLevel.Fast });
            Report(baseline);

            Console.WriteLine();
            var cautiousPoints = mixed.Where(r => r.Level == BotLevel.Cautious).Sum(r => r.Points);
            var fastPoints = mixed.Where(r => r.Level == BotLevel.Fast).Sum(r => r.Points);
            Console.WriteLine($"cautious vs fast: points {cautiousPoints} : {fastPoints} " +
                              $"(ratio {(fastPoints == 0 ? 0 : (double)cautiousPoints / fastPoints):0.00})");
            return 0;
        }

        private static List<SeatResult> Run(int sessions, BotLevel[] levels)
        {
            var totals = Enumerable.Range(0, 4)
                .Select(seat => new SeatResult(seat, levels[seat], 0, 0, 0, 0))
                .ToList();

            for (var sessionIndex = 0; sessionIndex < sessions; sessionIndex++)
            {
                var session = new GameSession();
                var random = new Random(9000 + sessionIndex);
                var bots = Enumerable.Range(0, 4)
                    .Select(seat => (IMahjongBot)new HeuristicBot(sessionIndex * 31 + seat, levels[seat]))
                    .ToArray();

                for (var hand = 0; hand < HandsPerSession; hand++)
                {
                    session.StartNextHand(random.Next());
                    BotTable.PlayHand(session.State!, bots, random);

                    var result = session.State!.Result;
                    session.CompleteHand();

                    if (result == null) continue;
                    totals[result.PlayerIndex] = totals[result.PlayerIndex] with
                    {
                        Wins = totals[result.PlayerIndex].Wins + 1
                    };
                    if (result.DiscarderPlayer is { } discarder)
                        totals[discarder] = totals[discarder] with { DealtIn = totals[discarder].DealtIn + 1 };
                }

                for (var seat = 0; seat < 4; seat++)
                    totals[seat] = totals[seat] with { Points = totals[seat].Points + session.Scores[seat] };
            }

            return totals;
        }

        private static void Report(List<SeatResult> results)
        {
            foreach (var result in results)
                Console.WriteLine($"  seat {result.Seat} {result.Level,-8} points {result.Points,7}  " +
                                  $"wins {result.Wins,4}  dealt in {result.DealtIn,4}");
        }

        private sealed record SeatResult(int Seat, BotLevel Level, int Points, int Wins, int DealtIn, int Unused);
    }
}
