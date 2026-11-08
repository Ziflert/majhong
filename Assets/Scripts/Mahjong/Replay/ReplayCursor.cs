using System;
using System.Collections.Generic;
using System.Linq;
using Mahjong.Net;
using Mahjong.Rules;

namespace Mahjong.Replay
{
    /// <summary>
    /// A moving position inside a saved game: the table exactly as it stood after the moves that have
    /// been stepped over. One step forward applies one move to the live replay table; a step back
    /// rebuilds the hand up to the target, which is cheap because a hand is a hundred moves of pure
    /// logic with no rendering attached.
    ///
    /// This is what the in-game review drives: arrows step, the board follows.
    /// </summary>
    public sealed class ReplayCursor
    {
        /// <summary>
        /// One saved move as a sentence can use it: who moved, what he did and the faces of the tiles
        /// he did it with. The words are the interface's business, not the log's.
        /// </summary>
        public sealed record SavedMove(int Seat, ActionKind Kind, IReadOnlyList<string> Tiles)
        {
            public override string ToString() =>
                $"P{Seat + 1} {ActionCodec.KindCode(Kind)}"
                + (Tiles.Count > 0 ? " " + string.Join(" ", Tiles) : string.Empty);
        }

        private readonly SessionLog log;
        private AuthoritativeTable table = null!;
        private int handIndex;
        private int moveIndex;

        public ReplayCursor(SessionLog log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            if (log.IsEmpty)
                throw new ArgumentException("a replay needs at least one dealt hand", nameof(log));
            RebuildTo(0);
        }

        public SessionLog Log => log;
        public int HandIndex => handIndex;
        public int HandCount => log.Hands.Count;
        public int MoveIndex => moveIndex;
        public int MoveCount => log.Hands[handIndex].Moves.Count;
        public bool AtStart => moveIndex == 0;
        public bool AtEnd => moveIndex == MoveCount;

        /// <summary>The table at the cursor position; the view renders exactly this state.</summary>
        public AuthoritativeTable Table => table;
        public GameState State => table.State;

        /// <summary>Where the cursor stands, as the review prints it: hand 2/4, move 17/53.</summary>
        public string PositionText => $"{handIndex + 1}/{HandCount} · {moveIndex}/{MoveCount}";

        /// <summary>Applies the next move of this hand; false at the end of the hand.</summary>
        public bool Next()
        {
            if (AtEnd) return false;
            var (seat, wire) = log.Hands[handIndex].Moves[moveIndex];
            var reply = table.Accept(seat, wire);
            if (!reply.Accepted)
                throw new InvalidOperationException(
                    $"hand {handIndex + 1} move {moveIndex + 1} does not replay: {reply.Reason}");
            moveIndex++;
            return true;
        }

        /// <summary>Steps back one move; false at the deal.</summary>
        public bool Back()
        {
            if (AtStart) return false;
            RebuildTo(moveIndex - 1);
            return true;
        }

        public void ToStart() => RebuildTo(0);
        public void ToEnd() => RebuildTo(MoveCount);

        /// <summary>Moves the cursor to the next hand and its deal; false on the last hand.</summary>
        public bool NextHand()
        {
            if (handIndex + 1 >= HandCount) return false;
            handIndex++;
            RebuildTo(0);
            return true;
        }

        /// <summary>Moves the cursor to the previous hand and its deal; false on the first hand.</summary>
        public bool PreviousHand()
        {
            if (handIndex == 0) return false;
            handIndex--;
            RebuildTo(0);
            return true;
        }

        /// <summary>
        /// The move at <paramref name="index"/>: who moved, what and with which tiles. Tile faces are
        /// named from the table at the cursor position, so the move the cursor has just stepped over
        /// is always named exactly.
        /// </summary>
        public SavedMove MoveAt(int index)
        {
            var moves = log.Hands[handIndex].Moves;
            if (index < 0 || index >= moves.Count)
                throw new ArgumentOutOfRangeException(nameof(index), $"hand {handIndex + 1} has no move {index + 1}");

            var (seat, wire) = moves[index];
            if (!ActionCodec.TryDecode(wire, out var intent, out _))
                return new SavedMove(seat, ActionKind.Pass, new[] { wire });

            var names = new List<string>();
            if (intent.TileId.HasValue) names.Add(TileName(intent.TileId.Value));
            foreach (var id in intent.RequiredIds) names.Add(TileName(id));
            return new SavedMove(seat, intent.Kind, names);
        }

        /// <summary>The move the cursor has just stepped over; null right after a deal.</summary>
        public SavedMove? LastStep => moveIndex == 0 ? null : MoveAt(moveIndex - 1);

        /// <summary>The same move as plain text, for logs and failure messages.</summary>
        public string LastMoveText => LastStep?.ToString() ?? string.Empty;

        private void RebuildTo(int move)
        {
            var hand = log.Hands[handIndex];
            table = AuthoritativeTable.Replay(hand.Dealer, hand.Seed, hand.RoundWind,
                hand.LogLines().Take(move));
            moveIndex = move;
        }

        /// <summary>The face of a tile by its identity; the identity alone when the tile is gone.</summary>
        private string TileName(int instanceId)
        {
            var tile = FindTile(instanceId);
            return tile.HasValue ? TileNotation.Format(tile.Value.Type) : $"#{instanceId}";
        }

        private Tile? FindTile(int instanceId)
        {
            var state = table.State;
            foreach (var player in state.Players)
            {
                foreach (var tile in player.Concealed)
                    if (tile.InstanceId == instanceId) return tile;
                foreach (var tile in player.Flowers)
                    if (tile.InstanceId == instanceId) return tile;
                foreach (var meld in player.Melds)
                foreach (var tile in meld.Tiles)
                    if (tile.InstanceId == instanceId) return tile;
            }

            foreach (var (player, tile) in state.Discards)
                if (tile.InstanceId == instanceId) return tile;

            return null;
        }
    }
}
