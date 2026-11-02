using System;
using System.Collections.Generic;
using System.Linq;

namespace Mahjong.Rules
{
    /// <summary>
    /// A full game session: four rounds of four hands, dealer rotating counter-clockwise
    /// after every hand, game points accumulated and converted into table points (3.9.2, 3.10).
    /// </summary>
    public sealed class GameSession
    {
        public const int RoundsPerGame = 4;
        public const int HandsPerRound = 4;
        public const int TotalHands = RoundsPerGame * HandsPerRound;

        private readonly List<Settlement> history = new();
        private readonly int[] scores = new int[4];

        public IReadOnlyList<int> Scores => scores;
        public IReadOnlyList<Settlement> History => history;

        /// <summary>Number of hands already completed.</summary>
        public int HandsPlayed { get; private set; }
        public bool IsFinished => HandsPlayed >= TotalHands;

        /// <summary>Rule 3.4.8: the dealer passes the dice on after every hand, win or draw.</summary>
        public int Dealer => HandsPlayed % 4;
        public Wind RoundWind => (Wind)Math.Min(HandsPlayed / HandsPerRound, RoundsPerGame - 1);
        public GameState? State { get; private set; }
        public Settlement? LastSettlement { get; private set; }

        public void StartNextHand(int? seed = null)
        {
            if (IsFinished) throw new InvalidOperationException("All sixteen hands have been played.");
            State = new GameState();
            State.StartHand(Dealer, seed, RoundWind);
        }

        /// <summary>Applies the finished hand to the score sheet and moves to the next hand.</summary>
        public Settlement CompleteHand()
        {
            if (State == null) throw new InvalidOperationException("No hand is in progress.");
            if (State.Phase != GamePhase.HandFinished) throw new InvalidOperationException("The hand is still running.");

            var settlement = State.Result != null ? Settlements.ForWin(State.Result) : Settlements.ForDraw();
            Apply(settlement);
            HandsPlayed++;
            return settlement;
        }

        public void Apply(Settlement settlement)
        {
            foreach (var payment in settlement.Payments)
            {
                scores[payment.FromPlayer] -= payment.Points;
                scores[payment.ToPlayer] += payment.Points;
            }
            LastSettlement = settlement;
            history.Add(settlement);
        }

        /// <summary>Rule 3.9.2: 4 / 2 / 1 / 0 table points; ties share the points of their places.</summary>
        public double[] TablePoints()
        {
            var points = new double[4];
            var order = Enumerable.Range(0, 4).OrderByDescending(player => scores[player]).ToList();
            var place = 0;
            while (place < order.Count)
            {
                var player = order[place];
                var tied = new List<int> { player };
                var next = place + 1;
                while (next < order.Count && scores[order[next]] == scores[player])
                {
                    tied.Add(order[next]);
                    next++;
                }

                var split = 0.0;
                for (var i = place; i < next; i++) split += PlacePoints(i);
                foreach (var tiedPlayer in tied) points[tiedPlayer] = split / tied.Count;
                place = next;
            }
            return points;
        }

        private static int PlacePoints(int place) => place switch
        {
            0 => 4,
            1 => 2,
            2 => 1,
            _ => 0
        };

        public string Describe()
        {
            var points = TablePoints();
            return string.Join(Environment.NewLine, Enumerable.Range(0, 4).Select(player =>
                $"P{player}: {scores[player]} game points, {points[player]:0.##} table points"));
        }
    }
}
