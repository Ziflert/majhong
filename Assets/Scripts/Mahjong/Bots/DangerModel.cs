using System;
using System.Collections.Generic;
using Mahjong.Rules;

namespace Mahjong.Bots
{
    /// <summary>
    /// Read of the table: how likely a discard is to be taken by somebody.
    ///
    /// Only public information is used — every discard on the table, every exposed meld — plus
    /// the bot's own concealed tiles. One thing to remember while reading the numbers: MCR has
    /// no furiten rule (rule.pdf 3.4–3.8), so a face an opponent has already thrown is only
    /// unlikely to be their wait, never impossible. Such a face is scored low, not zero.
    ///
    /// The scale is 0 (harmless) to 1 (a live wait), relative to the hand in progress, not points.
    /// </summary>
    public sealed class DangerModel
    {
        private const int Faces = 34;
        private const int Players = 4;

        private readonly int[] visible = new int[Faces];
        private readonly bool[,] thrownBy = new bool[Players, Faces];
        private readonly bool[,] meldedBy = new bool[Players, Faces];
        private readonly int[] discardsOf = new int[Players];
        private readonly int[] openMeldsOf = new int[Players];
        private readonly int[] ownCount = new int[Faces];
        private readonly int observer;

        private DangerModel(int observer) => this.observer = observer;

        /// <summary>Discards made in this hand so far; the table gets more dangerous as it grows.</summary>
        public int Turn { get; private set; }

        public static DangerModel Read(GameState state, int player)
        {
            var model = new DangerModel(player);

            for (var discarder = 0; discarder < Players; discarder++)
                foreach (var tile in state.Players[discarder].Discards)
                {
                    var index = Index(tile.Type);
                    model.thrownBy[discarder, index] = true;
                    model.visible[index]++;
                    model.discardsOf[discarder]++;
                    model.Turn++;
                }

            for (var owner = 0; owner < Players; owner++)
                foreach (var meld in state.Players[owner].Melds)
                {
                    if (meld.IsOpen) model.openMeldsOf[owner]++;
                    foreach (var tile in meld.Tiles)
                    {
                        var index = Index(tile.Type);
                        model.meldedBy[owner, index] = true;
                        model.visible[index]++;
                    }
                }

            // The bot's own flowers and concealed tiles are visible to nobody else, but they do
            // cut down how many copies the opponents can still be holding.
            var own = state.Players[player];
            foreach (var tile in own.Concealed)
            {
                var index = Index(tile.Type);
                model.ownCount[index]++;
                model.visible[index]++;
            }

            return model;
        }

        /// <summary>Copies of a face the bot can see somewhere: discards, melds and its own hand.</summary>
        public int VisibleCopies(TileType type) => visible[Index(type)];

        /// <summary>How ready an opponent looks: melds and discards say more than hidden tiles.</summary>
        public double Pressure(int opponent)
        {
            if (opponent == observer) return 0;
            var pressure = 0.25 + 0.03 * Turn + 0.18 * openMeldsOf[opponent] + 0.01 * discardsOf[opponent];
            return Math.Min(1.0, pressure);
        }

        /// <summary>Danger of one face against one opponent, ignoring how ready that opponent is.</summary>
        public double FaceDanger(TileType type, int opponent)
        {
            var index = Index(type);

            // A face the opponent already holds in a meld cannot complete the hand again.
            if (meldedBy[opponent, index]) return 0.05;

            var danger = Baseline(type);
            danger *= CopyFactor(type, index);

            // MCR has no furiten, so their own discard only makes a wait unlikely.
            if (thrownBy[opponent, index]) danger *= 0.35;
            else if (type.IsNumbered) danger *= SujiFactor(type, opponent);

            // Copies in the bot's own hand leave less material for an opponent's wait.
            danger *= Math.Max(0.4, 1.0 - 0.2 * ownCount[index]);
            return Math.Min(1.0, danger);
        }

        /// <summary>Danger of one face against every opponent: the worst of them decides.</summary>
        public double Danger(TileType type)
        {
            var worst = 0.0;
            for (var opponent = 0; opponent < Players; opponent++)
            {
                if (opponent == observer) continue;
                var danger = Pressure(opponent) * FaceDanger(type, opponent);
                if (danger > worst) worst = danger;
            }
            return worst;
        }

        public static int Index(TileType type)
        {
            return type.Suit switch
            {
                TileSuit.Characters or TileSuit.Dots or TileSuit.Bamboo => NumberedIndex(type.Suit, type.Rank),
                TileSuit.Wind => 27 + type.Rank,
                TileSuit.Dragon => 31 + type.Rank,
                _ => 0
            };
        }

        private static int NumberedIndex(TileSuit suit, int rank)
        {
            var baseIndex = suit switch
            {
                TileSuit.Characters => 0,
                TileSuit.Dots => 9,
                _ => 18
            };
            return baseIndex + rank - 1;
        }

        /// <summary>Middle tiles complete more chows and pungs than the edges.</summary>
        private static double Baseline(TileType type)
        {
            if (type.IsHonor) return 0.85;
            if (!type.IsNumbered) return 0.2;
            return type.Rank switch
            {
                1 or 9 => 0.4,
                2 or 8 => 0.75,
                _ => 1.0
            };
        }

        /// <summary>Copies already on the table: three visible copies leave no pair and no pung wait.</summary>
        private double CopyFactor(TileType type, int index)
        {
            var seen = visible[index] - ownCount[index];
            if (type.IsHonor)
                return seen switch { 0 => 1.0, 1 => 0.85, 2 => 0.4, _ => 0.05 };
            return seen switch { 0 => 1.0, 1 => 0.95, 2 => 0.75, _ => 0.3 };
        }

        /// <summary>Throwing the neighbours of a face makes a wait on that face less likely.</summary>
        private double SujiFactor(TileType type, int opponent)
        {
            double factor = 1.0;

            // A wait that needs this face as an inside tile is broken by the tile three away.
            for (var delta = -3; delta <= 3; delta += 3)
            {
                if (delta == 0) continue;
                var rank = type.Rank + delta;
                if (rank < 1 || rank > 9) continue;
                if (thrownBy[opponent, NumberedIndex(type.Suit, rank)]) factor = Math.Min(factor, 0.65);
            }

            for (var delta = -2; delta <= 2; delta++)
            {
                if (delta == 0) continue;
                var rank = type.Rank + delta;
                if (rank < 1 || rank > 9) continue;
                if (thrownBy[opponent, NumberedIndex(type.Suit, rank)]) factor = Math.Min(factor, 0.8);
            }

            return factor;
        }
    }
}
