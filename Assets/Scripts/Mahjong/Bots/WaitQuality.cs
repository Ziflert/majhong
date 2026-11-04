using System;
using System.Collections.Generic;
using System.Linq;
using Mahjong.Rules;

namespace Mahjong.Bots
{
    /// <summary>
    /// What a finished hand would pay, read from a waiting hand.
    ///
    /// Unlike <see cref="HandShape"/> this uses the real scorer: every face that completes the hand
    /// is run through <see cref="Scoring"/>, so the bot knows that one wait pays a full flush and the
    /// other only a chicken hand. The scorer also applies the eight-point minimum, so a wait that
    /// cannot win at all scores nothing here — which is exactly what the bot needs to know.
    /// </summary>
    public readonly struct WaitQuality
    {
        public WaitQuality(int waits, double averagePoints, int liveCopies = 0)
        {
            Waits = waits;
            AveragePoints = averagePoints;
            LiveCopies = liveCopies;
        }

        /// <summary>How many faces complete the hand.</summary>
        public int Waits { get; }

        /// <summary>Average points of the completed hand, counted only over waits that may win.</summary>
        public double AveragePoints { get; }

        /// <summary>Copies of the waiting faces that nobody has shown yet: how likely the wait is to come.</summary>
        public int LiveCopies { get; }

        /// <summary>
        /// Rough worth of the waiting hand: a wide and rich wait beats a narrow cheap one.
        /// The width is counted in copies that are still unseen, so a wait on a face the table has
        /// already shown three times is worth almost nothing.
        /// </summary>
        public double Value => LiveCopies * 8 + AveragePoints * 1.5;

        public static readonly WaitQuality None = new(0, 0);

        /// <summary>Reads the waits of a hand that is one tile short of a win.</summary>
        public static WaitQuality Of(IReadOnlyList<TileType> concealed, IReadOnlyList<Meld> melds,
            Wind seatWind = Wind.East, Wind roundWind = Wind.East, Func<TileType, int>? visibleCopies = null,
            RuleVariant? variant = null)
        {
            var hand = Build(concealed, melds);
            var waits = HandSolver.WaitingTiles(hand);
            if (waits.Count == 0) return None;

            var live = waits.Sum(wait => Math.Max(0, 4 - (visibleCopies?.Invoke(wait) ?? 0)));

            // Scoring takes the thirteen tiles and the winning face separately: it adds the face itself.
            var rules = variant ?? RuleVariant.Default;
            var points = 0;
            var scored = 0;
            foreach (var wait in waits)
            {
                var result = rules.Score(new ScoringContext
                {
                    Hand = hand,
                    WinningTile = wait,
                    Source = ClaimSource.Discard,
                    SeatWind = seatWind,
                    RoundWind = roundWind
                });
                if (!result.IsValidWin) continue;
                points += result.TotalPoints;
                scored++;
            }

            return new WaitQuality(waits.Count, scored == 0 ? 0 : (double)points / scored, live);
        }

        private static PlayerHand Build(IReadOnlyList<TileType> concealed, IReadOnlyList<Meld> melds)
        {
            var hand = new PlayerHand();
            var id = 0;
            foreach (var type in concealed) hand.AddConcealed(new Tile(type, id++));
            foreach (var meld in melds) hand.AddMeld(meld);
            return hand;
        }
    }
}
