using System;
using System.Collections.Generic;
using System.Linq;

namespace Mahjong.Rules
{
    /// <summary>
    /// What one seat is worth in the race for the current hand: how far its hand is from a win, how
    /// many live tiles would bring it closer, how many draws it still gets, and the two numbers the
    /// table is actually asking about — the chance to be ready before the wall runs out and the chance
    /// to win this very hand.
    /// </summary>
    /// <param name="Seat">Player index, zero to three.</param>
    /// <param name="Melds">Combinations the seat has already declared; they are public and they count.</param>
    /// <param name="Shanten">
    /// Tiles that still have to be swapped before the hand is complete: <c>0</c> is ready (a winning
    /// face completes it), <c>-1</c> is a complete hand, <c>4</c> is a hand that is still four steps away.
    /// </param>
    /// <param name="LiveTiles">Copies of every tile that takes the hand one step closer and is not on the table.</param>
    /// <param name="Turns">Draws this seat still gets before the wall runs out in its profile.</param>
    /// <param name="Known">
    /// True when the estimate could read the seat's own concealed tiles; false when the seat is a
    /// stranger and only its melds, its discards and the size of its hand were known.
    /// </param>
    /// <param name="ReadyChance">Chance the hand is ready before the wall ends, 0…1.</param>
    /// <param name="WinChance">Share of the hand this seat owns, 0…1; the four shares and the draw add up to one.</param>
    /// <param name="BestPoints">What the hand would score if it won on its best face right now; zero when it is not ready.</param>
    public sealed record SeatOdds(
        int Seat,
        int Melds,
        int Shanten,
        int LiveTiles,
        int Turns,
        bool Known,
        double ReadyChance,
        double WinChance,
        int BestPoints)
    {
        /// <summary>True when one more good tile wins the hand.</summary>
        public bool IsReady => Shanten <= 0;
    }

    /// <summary>The four seats of one hand, priced in the same breath, plus the chance of a draw.</summary>
    public sealed record TableOdds(
        IReadOnlyList<SeatOdds> Seats,
        double DrawChance,
        int WallDraws,
        int Turns,
        bool Oracle)
    {
        public SeatOdds Of(int seat) => Seats[seat];

        /// <summary>The seat most likely to win the hand.</summary>
        public SeatOdds Favourite => Seats.OrderByDescending(seat => seat.WinChance).First();

        /// <summary>One line per seat, for logs, tools and the tests that read numbers instead of pixels.</summary>
        public string Describe() =>
            string.Join(Environment.NewLine, Seats.Select(seat =>
                $"P{seat.Seat}: {seat.WinChance:P0} (shanten {seat.Shanten}, {seat.LiveTiles} live,"
                + $" {seat.Turns} draws{(seat.Known ? string.Empty : ", unknown hand")})"))
            + Environment.NewLine + $"draw: {DrawChance:P0}";
    }

    /// <summary>
    /// The probability that each hand of the current deal ends in a win, for both rule profiles.
    ///
    /// The model is a race for one hand, not a simulation of the game. Each seat needs <c>shanten + 1</c>
    /// good tiles; every good tile it can still draw is live with the copies that are not on the table,
    /// so the chance of a step on one draw is <c>live / unseen</c>, and the chance of a whole hand is the
    /// tail of a binomial over the draws the seat still has. A declared discard is a weaker opportunity
    /// than a draw (the owner has to hold the tile <em>and</em> be willing to drop it), so opponents are
    /// counted at <see cref="DiscardShare"/> of a draw. The four results are then normalised together
    /// with the draw, so the table always reads as one distribution and never as four loose numbers.
    ///
    /// What the model deliberately does not do: it does not look at the hidden hands of the opponents
    /// (see <see cref="FromSeat"/>), it does not model defence, kong declarations or a seat that stops
    /// pushing, and it prices a hand only through the scorer of the profile on the table. The exact
    /// version (<see cref="Oracle"/>) is for tools, bots and tests — it is not a hint the interface may
    /// show, because it reads tiles a player cannot see.
    ///
    /// The rules of the profile come in through three doors, and they are the reason this module is not
    /// a sporting-only adapter: the minimum eight points of MCR decide whether a ready hand <em>has</em>
    /// a winning face at all, the classic fourteen dead tiles and two free tiles decide how many draws
    /// are left, and the classic scoring decides what the hand is worth (<see cref="SeatOdds.BestPoints"/>).
    /// </summary>
    public static class WinOdds
    {
        /// <summary>
        /// How much of a draw a single opponent discard is worth. A discard only helps if that opponent
        /// holds the face and lets it go, which is much rarer than drawing it from the wall.
        /// </summary>
        public const double DiscardShare = 0.35d;

        /// <summary>
        /// Expected distance of a concealed hand nobody has seen. Thirteen random tiles are about four
        /// and a half steps from a win; every declared combination is three tiles the seat no longer
        /// has to find, and it also says the seat is playing towards a hand.
        /// </summary>
        private const double UnknownShantenBase = 4.5d;

        /// <summary>What one declared combination is worth in the same scale.</summary>
        private const double UnknownShantenPerMeld = 1.2d;

        /// <summary>Live tiles an unseen hand of the same size would be waiting on, for the same reason.</summary>
        private const int UnknownLiveBase = 48;

        /// <summary>How many of those live tiles one declared combination takes away.</summary>
        private const int UnknownLivePerMeld = 9;

        /// <summary>
        /// The table as the seat <paramref name="observer"/> can see it: its own concealed tiles are
        /// read exactly, the three opponents are priced from what everybody can see — their melds, the
        /// tiles they have discarded and the size of the hand they are holding. This is the estimate an
        /// interface may show: it changes only when the table changes.
        /// </summary>
        public static TableOdds FromSeat(GameState state, int observer)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if ((uint)observer >= (uint)state.Players.Length) throw new ArgumentOutOfRangeException(nameof(observer));
            return Estimate(state, observer);
        }

        /// <summary>
        /// The same model with every concealed hand read exactly. Tools, bots and the tests use it; the
        /// interface must not, because it would tell a player what the opponents are holding.
        /// </summary>
        public static TableOdds Oracle(GameState state) => Estimate(state, observer: -1);

        private static TableOdds Estimate(GameState state, int observer)
        {
            var wallDraws = state.Wall.DrawableTiles;
            var turns = wallDraws / 4;
            var odds = new List<SeatOdds>(4);
            var seenByObserver = Visible(state, observer);

            for (var seat = 0; seat < 4; seat++)
            {
                var hand = state.Players[seat];
                var known = observer < 0 || seat == observer;
                int shanten, live;
                if (known)
                {
                    (shanten, live) = ReadHand(state, seat, hand, seenByObserver);
                }
                else
                {
                    shanten = Math.Max(0, (int)Math.Round(UnknownShantenBase - UnknownShantenPerMeld * hand.Melds.Count));
                    live = Math.Max(0, UnknownLiveBase - UnknownLivePerMeld * hand.Melds.Count);
                }

                // A seat nearer to the move gets one more draw than the one three places away.
                var offset = ((seat - state.CurrentPlayer) % 4 + 4) % 4;
                var own = Math.Max(0, (wallDraws - offset + 3) / 4);
                var seen = Math.Max(1, 136 - seenByObserver.Count);
                odds.Add(Chance(seat, hand, shanten, live, own, seen, known, state));
            }

            // The shares have to add up: the chance of a draw is what is left when nobody wins, and the
            // three strangers were priced one by one, so the fourth number is the sum, not a guess.
            var draw = odds.Aggregate(1d, (rest, seat) => rest * (1d - seat.WinChance));
            var winMass = 1d - draw;
            var total = odds.Sum(seat => seat.WinChance);
            if (total > 0d)
            {
                var scale = winMass / total;
                odds = odds.Select(seat => seat with
                {
                    WinChance = Math.Round(seat.WinChance * scale, 6),
                    ReadyChance = Math.Round(seat.ReadyChance, 6)
                }).ToList();
            }

            return new TableOdds(odds, Math.Round(draw, 6), wallDraws, turns, observer < 0);
        }

        /// <summary>
        /// One seat's distance, its live tiles and the two chances that follow from them. The winning
        /// faces are priced by the scorer of the profile, so a sporting hand with fewer than eight
        /// points has no live winning face while the same hand in classic does.
        /// </summary>
        private static SeatOdds Chance(int seat, PlayerHand hand, int shanten, int live, int own,
            int seen, bool known, GameState state)
        {
            var hazard = Math.Min(1d, (double)live / seen);
            var steps = Math.Max(1, shanten + 1);
            var opportunities = own + (int)Math.Round(own * 3 * DiscardShare);
            var ready = AtLeast(steps, own, hazard);
            var row = AtLeast(steps, opportunities, hazard);
            var best = shanten <= 0 && known ? BestPoints(state, seat, hand) : 0;
            return new SeatOdds(seat, hand.Melds.Count, shanten, live, own, known,
                Math.Min(1d, ready), Math.Min(1d, row) * 0.98d, best);
        }

        /// <summary>What the hand scores on its best winning face right now, by the profile's own scorer.</summary>
        private static int BestPoints(GameState state, int seat, PlayerHand hand)
        {
            var best = 0;
            foreach (var wait in HandSolver.WaitingTiles(hand))
            {
                var result = state.Variant.Score(new ScoringContext
                {
                    Hand = hand,
                    WinningTile = wait,
                    Source = ClaimSource.SelfDraw,
                    SeatWind = state.SeatWindOf(seat),
                    RoundWind = state.RoundWind
                });
                if (result.IsValidWin) best = Math.Max(best, result.TotalPoints);
            }
            return best;
        }

        /// <summary>
        /// Distance to a win and the live tiles that shorten it, for a hand whose tiles are known. A hand
        /// that is already waiting on faces the scorer rejects counts as no waits at all: in the sporting
        /// profile eight points are the price of winning, so a hand waiting on a chicken hand is a hand
        /// with four dead waits.
        /// </summary>
        private static (int Shanten, int Live) ReadHand(GameState state, int seat, PlayerHand hand,
            IReadOnlyCollection<TileType> seen)
        {
            var concealed = hand.Concealed.Select(tile => tile.Type).ToList();
            var melds = hand.Melds.Count;
            var distance = ShantenOf(concealed, melds);
            if (distance < 0) return (-1, 0);

            var live = 0;
            foreach (var face in TileTypes.All)
            {
                var copies = 4 - Count(seen, face);
                if (copies <= 0) continue;
                if (ShantenOf(concealed.Append(face).ToList(), melds) >= distance) continue;
                if (distance == 0 && !Wins(state, seat, hand, face)) continue;
                live += copies;
            }
            return (distance, live);
        }

        /// <summary>True when that face completes the hand and pays at least the profile's minimum.</summary>
        private static bool Wins(GameState state, int seat, PlayerHand hand, TileType face)
        {
            if (!HandSolver.CanComplete(hand, face)) return false;
            var result = state.Variant.Score(new ScoringContext
            {
                Hand = hand,
                WinningTile = face,
                Source = ClaimSource.SelfDraw,
                SeatWind = state.SeatWindOf(seat),
                RoundWind = state.RoundWind
            });
            return result.IsValidWin;
        }

        /// <summary>
        /// How many tiles a hand of thirteen (minus its melds) is short of a complete one: <c>0</c> is
        /// ready, <c>-1</c> is already complete, <c>1</c> is one swap away. The standard four sets and a
        /// pair, seven pairs and thirteen orphans are measured; the knitted hands are not (they are rare
        /// and a hand playing for them is measured as the ordinary hand it could still become).
        /// </summary>
        public static int ShantenOf(IReadOnlyList<TileType> concealed, int melds)
        {
            var counts = new int[34];
            foreach (var tile in concealed)
            {
                var index = IndexOf(tile);
                if (index >= 0) counts[index]++;
            }

            var total = counts.Sum();
            var expected = 13 - melds * 3;
            if (total == expected + 1)
            {
                // A hand that has just drawn: the drawn tile is the one the player is about to drop,
                // so the distance is the best distance over the tiles one discard can leave. A hand
                // that already stands in its winning shape is one step further back, not one closer:
                // "complete" is -1 by the same scale where "ready" is 0.
                if (Math.Min(StandardShanten(counts, melds),
                        Math.Min(SevenPairsShanten(counts), ThirteenOrphansShanten(counts))) < 0)
                    return -1;

                var best = 8;
                for (var index = 0; index < 34; index++)
                {
                    if (counts[index] == 0) continue;
                    counts[index]--;
                    best = Math.Min(best, ShantenOf(counts, melds));
                    counts[index]++;
                }
                return best;
            }
            if (total != expected) return Math.Max(0, 8 - melds * 2);

            var standard = StandardShanten(counts, melds);
            var pairs = SevenPairsShanten(counts);
            var orphans = ThirteenOrphansShanten(counts);
            return Math.Min(standard, Math.Min(pairs, orphans));
        }

        private static int ShantenOf(int[] counts, int melds) =>
            Math.Min(StandardShanten(counts, melds),
                Math.Min(SevenPairsShanten(counts), ThirteenOrphansShanten(counts)));

        /// <summary>
        /// Standard distance: eight minus two per completed set and one per partial set, with at most
        /// four sets and partials together. The search below is the same decomposition the scorer uses,
        /// only it stops at the first useful shape instead of listing them.
        /// </summary>
        private static int StandardShanten(int[] counts, int melds)
        {
            var work = (int[])counts.Clone();
            var best = 8 - melds * 2;
            Walk(work, 0, melds, 0, 0, ref best);
            return best;
        }

        private static void Walk(int[] counts, int index, int sets, int partials, int pair, ref int best)
        {
            var distance = 8 - sets * 2 - partials - pair;
            if (distance < best) best = distance;

            for (var position = index; position < 34; position++)
            {
                if (counts[position] == 0) continue;

                // The pair is the last thing a hand needs, so it is tried before the four sets run out:
                // a complete hand is four sets and a pair, and only with both is it complete.
                if (counts[position] >= 2 && pair == 0)
                {
                    counts[position] -= 2;
                    Walk(counts, position, sets, partials, 1, ref best);
                    counts[position] += 2;
                }

                // Four sets are as much as a hand holds; from here only the pair above can help.
                if (sets >= 4) continue;

                if (counts[position] >= 3)
                {
                    counts[position] -= 3;
                    Walk(counts, position, sets + 1, partials, pair, ref best);
                    counts[position] += 3;
                }

                if (IsRunStart(position) && counts[position + 1] > 0 && counts[position + 2] > 0)
                {
                    counts[position]--;
                    counts[position + 1]--;
                    counts[position + 2]--;
                    Walk(counts, position, sets + 1, partials, pair, ref best);
                    counts[position]++;
                    counts[position + 1]++;
                    counts[position + 2]++;
                }

                if (sets + partials >= 4) continue;

                if (counts[position] >= 2)
                {
                    counts[position] -= 2;
                    Walk(counts, position, sets, partials + 1, pair, ref best);
                    counts[position] += 2;
                }

                if (IsRunStart(position) && counts[position + 1] > 0)
                {
                    counts[position]--;
                    counts[position + 1]--;
                    Walk(counts, position, sets, partials + 1, pair, ref best);
                    counts[position]++;
                    counts[position + 1]++;
                }

                if (IsGapStart(position) && counts[position + 2] > 0)
                {
                    counts[position]--;
                    counts[position + 2]--;
                    Walk(counts, position, sets, partials + 1, pair, ref best);
                    counts[position]++;
                    counts[position + 2]++;
                }

                // The tile is left alone: it may still become part of something later in the search.
                counts[position]--;
                Walk(counts, position, sets, partials, pair, ref best);
                counts[position]++;
            }
        }

        /// <summary>Seven pairs: six pairs are a ready hand, and a nearly empty hand needs new kinds too.</summary>
        private static int SevenPairsShanten(int[] counts)
        {
            var pairs = 0;
            var kinds = 0;
            foreach (var count in counts)
            {
                if (count == 0) continue;
                kinds++;
                if (count >= 2) pairs++;
            }
            var shanten = 6 - pairs + Math.Max(0, 7 - kinds);
            if (pairs == 7) shanten = -1;
            return shanten;
        }

        /// <summary>Thirteen orphans: one of every terminal and honour, and a pair of any of them.</summary>
        private static int ThirteenOrphansShanten(int[] counts)
        {
            var kinds = 0;
            var hasPair = false;
            for (var index = 0; index < 34; index++)
            {
                if (counts[index] == 0 || !IsTerminalOrHonour(index)) continue;
                kinds++;
                if (counts[index] >= 2) hasPair = true;
            }
            var shanten = 13 - kinds - (hasPair ? 1 : 0);
            return shanten == -1 ? -1 : shanten;
        }

        /// <summary>
        /// Tiles the observer can see: its own concealed hand, every flower, every discard and every
        /// declared combination on the table. With <paramref name="observer"/> below zero nothing is
        /// hidden, which is what the oracle needs to count the wall correctly.
        /// </summary>
        private static IReadOnlyCollection<TileType> Visible(GameState state, int observer)
        {
            var seen = new List<TileType>();
            for (var seat = 0; seat < 4; seat++)
            {
                var hand = state.Players[seat];
                if (observer < 0 || seat == observer) seen.AddRange(hand.Concealed.Select(tile => tile.Type));
                seen.AddRange(hand.Flowers.Select(tile => tile.Type));
                foreach (var meld in hand.Melds) seen.AddRange(meld.Tiles.Select(tile => tile.Type));
            }
            seen.AddRange(state.Discards.Select(discard => discard.Tile.Type));
            return seen;
        }

        private static int Count(IEnumerable<TileType> types, TileType target) =>
            types.Count(type => type.Equals(target));

        /// <summary>
        /// Chance of at least <paramref name="needed"/> steps in <paramref name="draws"/> draws, each of
        /// them a live tile with probability <paramref name="hazard"/>. A seat with no draws left has no
        /// chance, however good its hand is.
        /// </summary>
        private static double AtLeast(int needed, int draws, double hazard)
        {
            if (needed <= 0) return 1d;
            if (draws < needed) return 0d;
            if (hazard <= 0d) return 0d;
            if (hazard >= 1d) return 1d;

            var sum = 0d;
            for (var wins = needed; wins <= draws; wins++) sum += Binomial(draws, wins) * Math.Pow(hazard, wins)
                * Math.Pow(1d - hazard, draws - wins);
            return Math.Min(1d, sum);
        }

        private static double Binomial(int draws, int wins)
        {
            var result = 1d;
            for (var index = 0; index < wins; index++)
                result = result * (draws - index) / (index + 1);
            return result;
        }

        private static int IndexOf(TileType tile)
        {
            if (tile.IsNumbered) return (int)tile.Suit * 9 + tile.Rank - 1;
            if (tile.IsWind) return 27 + tile.Rank;
            if (tile.IsDragon) return 31 + tile.Rank;
            return -1;
        }

        private static bool IsRunStart(int index) => index < 27 && index % 9 <= 6;

        private static bool IsGapStart(int index) => index < 27 && index % 9 <= 6;

        private static bool IsTerminalOrHonour(int index) => index >= 27 || index % 9 == 0 || index % 9 == 8;
    }
}
