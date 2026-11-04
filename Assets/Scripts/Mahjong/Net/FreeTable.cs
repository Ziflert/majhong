using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Mahjong.Rules;

namespace Mahjong.Net
{
    /// <summary>One tile lying on the free table. <see cref="HeldBy"/> is the seat carrying it.</summary>
    public sealed record FreeTile(int Id, TileType Type, int X, int Y, bool FaceUp, int? HeldBy)
    {
        public FreeTile Held(int seat) => this with { HeldBy = seat };
        public FreeTile Drop(int x, int y) => this with { X = x, Y = y, HeldBy = null };
        public FreeTile Turned() => this with { FaceUp = !FaceUp };

        public override string ToString() => $"{TileNotation.Format(Type)}#{Id}";
    }

    /// <summary>Result of one free-table move: the new state text, or a refusal.</summary>
    public sealed record FreeReply(bool Accepted, string Reason, string State, int Version)
    {
        public override string ToString() => Accepted ? $"accepted, version {Version}" : $"rejected: {Reason}";
    }

    /// <summary>
    /// Free play: the whole set of 144 tiles on a shared table, and nothing else. No wall, no turns,
    /// no hand — any seat may pick a tile up, put it somewhere else or turn it over. This is the
    /// sandbox mode of the product request ("just move the tiles"), and it is a multiplayer table from
    /// the start: moves come from seats and are validated by the same authority pattern as a scored hand.
    ///
    /// Rules of the table itself:
    /// <list type="bullet">
    /// <item>a tile is either on the grid or in the hands of exactly one seat;</item>
    /// <item>no two tiles may occupy the same cell;</item>
    /// <item>only the seat that holds a tile may place it;</item>
    /// <item>a tile held by another seat cannot be taken or turned.</item>
    /// </list>
    /// </summary>
    public sealed class FreeTable
    {
        /// <summary>Table surface, in cells. 18 × 8 fits the full set exactly.</summary>
        public const int Columns = 18;

        public const int Rows = 8;

        public const int TotalTiles = Columns * Rows;

        private readonly List<FreeTile> tiles = new();
        private int nextId = 1;

        public FreeTable(int? seed = null) => Reset(seed);

        public int Version { get; private set; }

        public IReadOnlyList<FreeTile> Tiles => tiles;

        public int TileCount => tiles.Count;

        /// <summary>Lays out a freshly shuffled set: every tile face up, one per cell.</summary>
        public void Reset(int? seed = null)
        {
            var random = seed.HasValue ? new Random(seed.Value) : new Random();
            var faces = new List<TileType>(TotalTiles);
            foreach (var type in TileTypes.All)
                for (var copy = 0; copy < 4; copy++)
                    faces.Add(type);
            for (var flower = 0; flower < 8; flower++)
                faces.Add(new TileType(TileSuit.Flower, flower));

            if (faces.Count != TotalTiles)
                throw new InvalidOperationException($"The set has {faces.Count} tiles, the table holds {TotalTiles}.");

            for (var i = faces.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (faces[i], faces[j]) = (faces[j], faces[i]);
            }

            tiles.Clear();
            nextId = 1;
            for (var i = 0; i < faces.Count; i++)
                tiles.Add(new FreeTile(nextId++, faces[i], i % Columns, i / Columns, true, null));

            Version++;
        }

        public FreeTile? Find(int id) => tiles.FirstOrDefault(tile => tile.Id == id);

        public FreeTile? TileAt(int x, int y) =>
            tiles.FirstOrDefault(tile => tile.HeldBy == null && tile.X == x && tile.Y == y);

        public bool Holds(int seat, int id) => Find(id)?.HeldBy == seat;

        /// <summary>
        /// Applies one wire move: <c>take|id</c>, <c>place|id|x|y</c>, <c>flip|id</c> or <c>reset|-</c>.
        /// A refused move leaves the table untouched.
        /// </summary>
        public FreeReply Submit(int seat, string wire)
        {
            if (!TryExecute(seat, wire, out var error))
                return new FreeReply(false, error, Describe(), Version);
            return new FreeReply(true, string.Empty, Describe(), Version);
        }

        private bool TryExecute(int seat, string wire, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(wire))
            {
                error = "empty move";
                return false;
            }

            var parts = wire.Trim().Split(new[] { '|', ';', ':' });
            var op = parts[0].Trim().ToLowerInvariant();

            if (op == "reset")
            {
                Reset();
                return true;
            }

            if (parts.Length < 2 || !int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
            {
                error = $"not a tile id: {wire}";
                return false;
            }

            switch (op)
            {
                case "take":
                    return TryTake(seat, id, out error);
                case "flip":
                    return TryFlip(seat, id, out error);
                case "place":
                case "put":
                    if (parts.Length < 4
                        || !int.TryParse(parts[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var x)
                        || !int.TryParse(parts[3].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
                    {
                        error = $"place needs x and y: {wire}";
                        return false;
                    }
                    return TryPlace(seat, id, x, y, out error);
                default:
                    error = $"unknown move: {op}";
                    return false;
            }
        }

        public bool TryTake(int seat, int id, out string error)
        {
            error = string.Empty;
            var tile = Find(id);
            if (tile == null)
            {
                error = $"there is no tile {id}";
                return false;
            }

            if (tile.HeldBy == seat) return true;
            if (tile.HeldBy.HasValue)
            {
                error = $"tile {id} is held by seat {tile.HeldBy}";
                return false;
            }

            Replace(tile.Held(seat));
            Version++;
            return true;
        }

        public bool TryPlace(int seat, int id, int x, int y, out string error)
        {
            error = string.Empty;
            var tile = Find(id);
            if (tile == null)
            {
                error = $"there is no tile {id}";
                return false;
            }

            if (tile.HeldBy != seat)
            {
                error = tile.HeldBy.HasValue
                    ? $"tile {id} is held by seat {tile.HeldBy}, not by seat {seat}"
                    : $"tile {id} is already on the table: take it first";
                return false;
            }

            if (x < 0 || x >= Columns || y < 0 || y >= Rows)
            {
                error = $"cell {x},{y} is outside the table";
                return false;
            }

            var occupant = TileAt(x, y);
            if (occupant != null && occupant.Id != id)
            {
                error = $"cell {x},{y} is taken by tile {occupant.Id}";
                return false;
            }

            Replace(tile.Drop(x, y));
            Version++;
            return true;
        }

        public bool TryFlip(int seat, int id, out string error)
        {
            error = string.Empty;
            var tile = Find(id);
            if (tile == null)
            {
                error = $"there is no tile {id}";
                return false;
            }

            if (tile.HeldBy.HasValue && tile.HeldBy != seat)
            {
                error = $"tile {id} is held by seat {tile.HeldBy}";
                return false;
            }

            Replace(tile.Turned());
            Version++;
            return true;
        }

        /// <summary>Puts a held tile back where it came from without moving it.</summary>
        public bool TryReturn(int seat, int id, out string error) => TryPlace(seat, id, Find(id)?.X ?? 0, Find(id)?.Y ?? 0, out error);

        private void Replace(FreeTile tile)
        {
            var index = tiles.FindIndex(candidate => candidate.Id == tile.Id);
            if (index < 0) throw new InvalidOperationException($"No tile {tile.Id}");
            tiles[index] = tile;
        }

        /// <summary>
        /// The shared state as text: one line per tile, sorted by identity, so two equal tables give
        /// equal text and a client can diff its picture against the server's.
        /// </summary>
        public string Describe()
        {
            var text = new StringBuilder();
            text.Append("version|").Append(Version.ToString(CultureInfo.InvariantCulture)).Append('\n');
            text.Append("tiles|").Append(tiles.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
            foreach (var tile in tiles.OrderBy(tile => tile.Id))
            {
                text.Append("tile|")
                    .Append(tile.Id.ToString(CultureInfo.InvariantCulture)).Append('|')
                    .Append(TileNotation.Format(tile.Type)).Append('|')
                    .Append(tile.X.ToString(CultureInfo.InvariantCulture)).Append('|')
                    .Append(tile.Y.ToString(CultureInfo.InvariantCulture)).Append('|')
                    .Append(tile.FaceUp ? "up" : "down").Append('|')
                    .Append(tile.HeldBy?.ToString(CultureInfo.InvariantCulture) ?? "-")
                    .Append('\n');
            }

            return text.ToString();
        }
    }
}
