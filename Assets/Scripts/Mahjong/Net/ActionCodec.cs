using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Mahjong.Rules;

namespace Mahjong.Net
{
    /// <summary>
    /// A move as a player states it, without tile faces: the identity of the tile is enough, the
    /// server already knows what the tile is. This is what travels over a wire and what a replay log
    /// stores, and it is compared against the engine's own list of legal actions.
    /// </summary>
    public sealed record MoveIntent(ActionKind Kind, int? TileId, IReadOnlyList<int> RequiredIds)
    {
        public override string ToString() =>
            $"{Kind.ToString().ToLowerInvariant()}|{(TileId.HasValue ? TileId.Value.ToString(CultureInfo.InvariantCulture) : "-")}|"
            + (RequiredIds.Count == 0 ? "-" : string.Join(",", RequiredIds));
    }

    /// <summary>
    /// Text form of a move: <c>kind|tile|required</c>, for example <c>discard|17|-</c> or
    /// <c>chow|17|21,22</c>. The separator may be <c>|</c>, <c>;</c> or <c>:</c>, so a hand typed into
    /// a console or a bug report still parses.
    ///
    /// A move is never trusted: <see cref="Resolve"/> turns an intent into a real
    /// <see cref="LegalAction"/> only when the engine offers exactly that action to that seat.
    /// </summary>
    public static class ActionCodec
    {
        private static readonly char[] Separators = { '|', ';', ':' };

        public static string KindCode(ActionKind kind) => kind switch
        {
            ActionKind.Draw => "draw",
            ActionKind.Discard => "discard",
            ActionKind.Chow => "chow",
            ActionKind.Pung => "pung",
            ActionKind.OpenKong => "openkong",
            ActionKind.AddedKong => "addedkong",
            ActionKind.ClosedKong => "closedkong",
            ActionKind.Win => "win",
            ActionKind.Pass => "pass",
            _ => kind.ToString().ToLowerInvariant()
        };

        public static bool TryParseKind(string? text, out ActionKind kind)
        {
            switch ((text ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "draw": kind = ActionKind.Draw; return true;
                case "discard" or "drop": kind = ActionKind.Discard; return true;
                case "chow" or "chi": kind = ActionKind.Chow; return true;
                case "pung" or "pong": kind = ActionKind.Pung; return true;
                case "openkong" or "kong": kind = ActionKind.OpenKong; return true;
                case "addedkong": kind = ActionKind.AddedKong; return true;
                case "closedkong" or "concealedkong": kind = ActionKind.ClosedKong; return true;
                case "win" or "hu" or "mahjong": kind = ActionKind.Win; return true;
                case "pass": kind = ActionKind.Pass; return true;
                default: kind = default; return false;
            }
        }

        /// <summary>The wire line of a legal action offered by the engine.</summary>
        public static string Encode(LegalAction action) => IntentOf(action).ToString();

        /// <summary>The wire form of the action a player has chosen.</summary>
        public static MoveIntent IntentOf(LegalAction action) => new(
            action.Kind,
            action.Tile?.InstanceId,
            action.RequiredTiles.Select(tile => tile.InstanceId).OrderBy(id => id).ToList());

        public static bool TryDecode(string? wire, out MoveIntent intent, out string error)
        {
            intent = new MoveIntent(ActionKind.Pass, null, Array.Empty<int>());
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(wire))
            {
                error = "empty move";
                return false;
            }

            var parts = wire.Trim().Split(Separators, StringSplitOptions.None);
            if (parts.Length == 0 || !TryParseKind(parts[0], out var kind))
            {
                error = $"unknown move kind: {parts[0]}";
                return false;
            }

            int? tileId = null;
            if (parts.Length > 1 && !IsEmpty(parts[1]))
            {
                if (!int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                {
                    error = $"not a tile id: {parts[1]}";
                    return false;
                }
                tileId = id;
            }

            var required = new List<int>();
            if (parts.Length > 2 && !IsEmpty(parts[2]))
            {
                foreach (var piece in parts[2].Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!int.TryParse(piece.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                    {
                        error = $"not a tile id: {piece}";
                        return false;
                    }
                    required.Add(id);
                }
            }

            intent = new MoveIntent(kind, tileId, required);
            return true;
        }

        private static bool IsEmpty(string part)
        {
            var trimmed = part.Trim();
            return trimmed.Length == 0 || trimmed == "-";
        }

        /// <summary>
        /// Finds the engine action an intent names, or null when the engine does not offer it. This is
        /// the whole authority of the server: nothing else is needed to reject a forged move.
        /// </summary>
        public static LegalAction? Resolve(IEnumerable<LegalAction> legal, MoveIntent intent)
        {
            foreach (var action in legal)
            {
                if (action.Kind != intent.Kind) continue;
                if ((action.Tile?.InstanceId) != intent.TileId) continue;

                var required = action.RequiredTiles.Select(tile => tile.InstanceId).OrderBy(id => id).ToList();
                var wanted = intent.RequiredIds.OrderBy(id => id).ToList();
                if (required.Count != wanted.Count) continue;
                if (!required.SequenceEqual(wanted)) continue;

                return action;
            }

            return null;
        }

        /// <summary>Readable log line: <c>P2 chow 5c#17 with 3c#21, 4c#22</c>.</summary>
        public static string Describe(LegalAction action)
        {
            var head = $"P{action.PlayerIndex} {KindCode(action.Kind)}";
            if (action.Tile.HasValue) head += " " + TileNotation.Format(action.Tile.Value);
            if (action.RequiredTiles.Count > 0) head += " with " + TileNotation.Format(action.RequiredTiles);
            return head;
        }
    }
}
