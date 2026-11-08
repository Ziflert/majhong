using System;
using System.Collections.Generic;
using System.Linq;
using Mahjong.Bots;
using Mahjong.Rules;

namespace Mahjong.BotBench
{
    /// <summary>
    /// Plays many sessions with mixed bot levels and reports what each level is worth: game points,
    /// hands won and hands dealt into. Used to tune the danger weights and to keep the thresholds of
    /// the regression tests honest.
    ///
    ///   dotnet run --project Tools/BotBench -- [sessions]
    ///
    /// Points are zero-sum and swing with one big hand, so the interesting columns are wins and
    /// deals-ins; points are printed as a sanity check only.
    /// </summary>
    public static class Program
    {
        private const int HandsPerSession = GameSession.TotalHands;

        public static int Main(string[] args)
        {
            var sessions = args.Length > 0 ? int.Parse(args[0]) : 40;

            var matches = new (string Label, BotLevel[] Levels)[]
            {
                ("cautious (0,2) vs fast (1,3)", new[] { BotLevel.Cautious, BotLevel.Fast, BotLevel.Cautious, BotLevel.Fast }),
                ("strategic (0,2) vs fast (1,3)", new[] { BotLevel.Strategic, BotLevel.Fast, BotLevel.Strategic, BotLevel.Fast }),
                ("strategic (0,2) vs cautious (1,3)", new[] { BotLevel.Strategic, BotLevel.Cautious, BotLevel.Strategic, BotLevel.Cautious }),
                ("every seat fast (baseline)", new[] { BotLevel.Fast, BotLevel.Fast, BotLevel.Fast, BotLevel.Fast })
            };

            foreach (var (label, levels) in matches)
            {
                Console.WriteLine($"{label} — {sessions} sessions");
                Report(Run(sessions, levels));
                Console.WriteLine();
            }

            return 0;
        }

        private static List<SeatResult> Run(int sessions, BotLevel[] levels)
        {
            var totals = Enumerable.Range(0, 4)
                .Select(seat => new SeatResult(seat, levels[seat], 0, 0, 0))
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
                    BotTable.UpdateMatchContext(session, bots);
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
                Console.WriteLine($"  seat {result.Seat} {result.Level,-10} points {result.Points,7}  " +
                                  $"wins {result.Wins,4}  dealt in {result.DealtIn,4}");

            foreach (var level in results.Select(r => r.Level).Distinct())
            {
                var seats = results.Where(r => r.Level == level).ToList();
                Console.WriteLine($"  {level,-10} total wins {seats.Sum(r => r.Wins),4}  " +
                                  $"dealt in {seats.Sum(r => r.DealtIn),4}  points {seats.Sum(r => r.Points),7}");
            }
        }

        private sealed record SeatResult(int Seat, BotLevel Level, int Points, int Wins, int DealtIn);
    }
}
