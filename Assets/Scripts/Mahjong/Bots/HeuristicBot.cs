using System;
using System.Collections.Generic;
using System.Linq;
using Mahjong.Rules;

namespace Mahjong.Bots
{
    /// <summary>
    /// Rough hand strength used by the bots: complete sets are worth most,
    /// partial sets next, isolated tiles nothing.
    /// </summary>
    public static class HandShape
    {
        /// <summary>Approximate shanten: lower is closer to a win.</summary>
        public static int Shanten(IReadOnlyCollection<TileType> tiles, int meldCount)
        {
            var sets = meldCount;
            var partials = 0;
            var counts = ToCounts(tiles);

            foreach (var type in counts.Keys.ToList())
            {
                while (counts[type] >= 3) { counts[type] -= 3; sets++; }
            }
            foreach (var suit in new[] { TileSuit.Characters, TileSuit.Dots, TileSuit.Bamboo })
                for (var rank = 1; rank <= 7; rank++)
                {
                    var first = new TileType(suit, rank);
                    var second = new TileType(suit, rank + 1);
                    var third = new TileType(suit, rank + 2);
                    while (counts.GetValueOrDefault(first) > 0 && counts.GetValueOrDefault(second) > 0 && counts.GetValueOrDefault(third) > 0)
                    {
                        counts[first]--;
                        counts[second]--;
                        counts[third]--;
                        sets++;
                    }
                }

            var pairFound = false;
            foreach (var type in counts.Keys.ToList())
            {
                if (!pairFound && counts[type] >= 2) { counts[type] -= 2; pairFound = true; partials++; continue; }
                if (counts[type] >= 2) { counts[type] -= 2; partials++; }
            }
            foreach (var suit in new[] { TileSuit.Characters, TileSuit.Dots, TileSuit.Bamboo })
                for (var rank = 1; rank <= 8; rank++)
                {
                    var first = new TileType(suit, rank);
                    var second = new TileType(suit, rank + 1);
                    if (counts.GetValueOrDefault(first) > 0 && counts.GetValueOrDefault(second) > 0)
                    {
                        counts[first]--;
                        counts[second]--;
                        partials++;
                    }
                }

            var shanten = 8 - sets * 2 - partials - (pairFound ? 1 : 0);
            return Math.Max(0, shanten);
        }

        /// <summary>How much a tile helps the hand: duplicates and neighbours raise the value.</summary>
        public static int Connect(TileType type, IReadOnlyCollection<TileType> tiles)
        {
            var value = tiles.Count(t => t == type) * 3;
            if (!type.IsNumbered) return value;
            for (var delta = -2; delta <= 2; delta++)
            {
                if (delta == 0) continue;
                var rank = type.Rank + delta;
                if (rank < 1 || rank > 9) continue;
                value += tiles.Count(t => t.Suit == type.Suit && t.Rank == rank) * (Math.Abs(delta) == 1 ? 2 : 1);
            }
            return value;
        }

        /// <summary>True when the tile can no longer be used in a chow or a pung.</summary>
        public static bool IsIsolated(TileType type, IReadOnlyCollection<TileType> tiles)
        {
            if (!type.IsNumbered) return tiles.Count(t => t == type) == 1;
            if (tiles.Count(t => t == type) >= 2) return false;
            for (var delta = -2; delta <= 2; delta++)
            {
                if (delta == 0) continue;
                var rank = type.Rank + delta;
                if (rank < 1 || rank > 9) continue;
                if (tiles.Any(t => t.Suit == type.Suit && t.Rank == rank)) return false;
            }
            return true;
        }

        private static Dictionary<TileType, int> ToCounts(IEnumerable<TileType> tiles)
        {
            var counts = new Dictionary<TileType, int>();
            foreach (var tile in tiles) counts[tile] = counts.GetValueOrDefault(tile) + 1;
            return counts;
        }
    }

    /// <summary>Anything that can pick an action for a player.</summary>
    public interface IMahjongBot
    {
        LegalAction Choose(GameState state, int player, IReadOnlyList<LegalAction> options);
    }

    /// <summary>How much of the table the bot reads when it chooses a discard.</summary>
    public enum BotLevel
    {
        /// <summary>Tiles only: shape of the own hand, no danger read.</summary>
        Fast = 1,

        /// <summary>Shape plus the danger model: avoids feeding a ready opponent.</summary>
        Cautious = 2,

        /// <summary>Cautious reading plus what the hand is worth and what the match needs.</summary>
        Strategic = 3
    }

    /// <summary>
    /// A bot built on three layers that it turns on with its level:
    ///
    /// 1. <see cref="BotLevel.Fast"/> — shape only: keeps the hand connected, claims only what
    ///    shortens the distance to a win, always takes a legal Hu.
    /// 2. <see cref="BotLevel.Cautious"/> — adds the <see cref="DangerModel"/>: among equally good
    ///    tiles the safest goes out, and a hand still far from a win gives up a little shape
    ///    to stay safe.
    /// 3. <see cref="BotLevel.Strategic"/> — adds <see cref="HandValue"/> when claiming (a dragon
    ///    pung is not the same as a chow) and shifts between push and fold according to how far
    ///    behind it is on the score sheet (<see cref="PointsBehind"/>).
    /// </summary>
    public sealed class HeuristicBot : IMahjongBot
    {
        private const int ShantenWeight = 1000;
        private const int ShapeWeight = 10;
        private const int DangerWeight = 500;
        private const int WaitWeight = 150;

        private readonly Random random;

        public HeuristicBot(int seed = 0, BotLevel level = BotLevel.Fast)
        {
            random = new Random(seed);
            Level = level;
        }

        public BotLevel Level { get; }

        /// <summary>How many game points this seat is behind the table leader; set per hand.</summary>
        public int PointsBehind { get; set; }

        public LegalAction Choose(GameState state, int player, IReadOnlyList<LegalAction> options)
        {
            var win = options.FirstOrDefault(option => option.Kind == ActionKind.Win);
            if (win != null) return win;

            var hand = state.Players[player];
            var tiles = hand.Concealed.Select(tile => tile.Type).ToList();
            var baseShanten = HandShape.Shanten(tiles, hand.Melds.Count);

            var claim = options
                .Where(option => option.Kind is ActionKind.Pung or ActionKind.Chow or ActionKind.OpenKong
                                 or ActionKind.ClosedKong or ActionKind.AddedKong)
                .OrderByDescending(option => ClaimValue(option, hand))
                .FirstOrDefault(option => ImprovesShanten(option, hand, baseShanten));

            if (claim != null && random.Next(100) < 85) return claim;

            var discard = options.FirstOrDefault(option => option.Kind == ActionKind.Discard);
            if (discard != null)
                return ChooseDiscard(options, tiles, hand, state, player,
                    Level == BotLevel.Fast ? null : DangerModel.Read(state, player));

            if (options.Any(option => option.Kind == ActionKind.Draw))
                return options.First(option => option.Kind == ActionKind.Draw);

            return options.First(option => option.Kind == ActionKind.Pass);
        }

        private static int ClaimValue(LegalAction option, PlayerHand hand)
        {
            var tile = option.Tile?.Type;
            if (!tile.HasValue) return 0;
            return HandShape.Connect(tile.Value, hand.Concealed.Select(t => t.Type).ToList());
        }

        private bool ImprovesShanten(LegalAction option, PlayerHand hand, int baseShanten)
        {
            if (option.Kind is ActionKind.ClosedKong or ActionKind.AddedKong) return true;
            var remaining = hand.Concealed.Select(t => t.Type).ToList();
            if (option.Kind is not ActionKind.ClosedKong)
                foreach (var required in option.RequiredTiles) remaining.Remove(required.Type);
            var melds = hand.Melds.Count + 1;
            return HandShape.Shanten(remaining, melds) <= baseShanten;
        }

        private LegalAction ChooseDiscard(IReadOnlyList<LegalAction> options, List<TileType> tiles,
            PlayerHand hand, GameState state, int player, DangerModel? danger)
        {
            var best = options[0];
            var bestScore = double.MinValue;

            foreach (var option in options.Where(o => o.Kind == ActionKind.Discard))
            {
                var type = option.Tile!.Value.Type;
                var rest = new List<TileType>(tiles);
                rest.Remove(type);
                var shanten = HandShape.Shanten(rest, hand.Melds.Count);
                var push = danger == null ? 0.0 : PushWeight(danger!.Turn, shanten, PointsBehind);
                double score = -shanten * ShantenWeight * (1 - 0.6 * push)
                            - HandShape.Connect(type, tiles) * ShapeWeight
                            + random.Next(9);

                if (danger != null)
                    score -= push * danger.Danger(type) * DangerWeight;

                // A strategic bot also asks what the hand will pay: a wide wait on a full flush is
                // worth keeping, a wait that never reaches eight points is worth breaking.
                if (Level == BotLevel.Strategic && shanten == 0)
                {
                    var quality = WaitQuality.Of(rest, hand.Melds, state.SeatWindOf(player), state.RoundWind,
                        danger == null ? null : (Func<TileType, int>)(face => danger.VisibleCopies(face)));
                    score += Math.Min(WaitWeight, quality.Value);
                }

                if (score > bestScore) { bestScore = score; best = option; }
            }
            return best;
        }

        /// <summary>
        /// How much the bot is willing to risk. A hand one tile away from a win pushes almost
        /// regardless; a hand still two or more steps out folds once the table is warm.
        /// A seat far behind on the score sheet pushes harder, a seat in front folds earlier.
        /// </summary>
        public static double PushWeight(int turn, int shantenAfterDiscard, int pointsBehind = 0)
        {
            var progress = Math.Min(1.0, turn / 30.0);
            var byDistance = shantenAfterDiscard switch
            {
                0 => 0.15,
                1 => 0.55,
                _ => 1.0
            };
            var byMatch = 1.0 + Math.Clamp(pointsBehind / 80.0, -0.3, 0.3);
            return Math.Min(1.0, byDistance * (0.5 + 0.5 * progress) * byMatch);
        }
    }

    /// <summary>Plays a whole session between bots; used by tests and by offline balance checks.</summary>
    public static class BotTable
    {
        public static GameSession PlaySession(int? seed = null, params IMahjongBot[] bots)
        {
            var session = new GameSession();
            var random = new Random(seed ?? Environment.TickCount);

            for (var handIndex = 0; handIndex < GameSession.TotalHands; handIndex++)
            {
                session.StartNextHand(random.Next());
                UpdateMatchContext(session, bots);
                PlayHand(session.State!, bots, random);
                session.CompleteHand();
            }
            return session;
        }

        /// <summary>Tells every heuristic bot how far behind it is, so it can push or fold accordingly.</summary>
        public static void UpdateMatchContext(GameSession session, IReadOnlyList<IMahjongBot> bots)
        {
            var leader = session.Scores.Max();
            for (var seat = 0; seat < bots.Count && seat < session.Scores.Count; seat++)
                if (bots[seat] is HeuristicBot heuristic)
                    heuristic.PointsBehind = leader - session.Scores[seat];
        }

        public static void PlayHand(GameState state, IReadOnlyList<IMahjongBot> bots, Random random)
        {
            var guard = 0;
            while (state.Phase != GamePhase.HandFinished && guard++ < 2000)
            {
                var claiming = state.Phase is GamePhase.AwaitingClaims or GamePhase.AwaitingKongRobbery;
                var acted = false;

                for (var player = 0; player < 4; player++)
                {
                    if (!claiming && player != state.CurrentPlayer) continue;
                    var options = state.GetLegalActions(player);
                    if (options.Count == 0) continue;

                    var action = bots[player].Choose(state, player, options);
                    state.Apply(action);
                    acted = true;
                    if (state.Phase is not (GamePhase.AwaitingClaims or GamePhase.AwaitingKongRobbery)) break;
                }

                if (!acted) break;
            }
        }
    }
}
