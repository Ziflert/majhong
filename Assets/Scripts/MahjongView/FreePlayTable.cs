using System.Collections.Generic;
using System.Linq;
using Mahjong.Localization;
using Mahjong.Net;
using Mahjong.Rules;
using UnityEngine;

namespace Mahjong.Unity
{
    /// <summary>
    /// Free play on the 3D table: all 144 tiles lying on a grid, and nothing else — no wall, no turn,
    /// no scoring. Any seat may pick a tile up, put it down somewhere else and turn it over, which is
    /// what "just move the tiles" means in the product request.
    ///
    /// The picture is a projection of <see cref="FreeTable"/>, the authoritative shared table: a click
    /// becomes a wire move and the tiles follow whatever the table accepted. Because the seats are a
    /// property of that table, the same code serves a hotseat game on one screen and a networked one
    /// (Tab switches the active seat, exactly as a networked client would be one seat).
    /// </summary>
    public sealed class FreePlayTable : MonoBehaviour
    {
        /// <summary>Top of the board surface; the same height the scored table uses.</summary>
        private const float TileY = 0.01f;

        /// <summary>Spacing of the grid: a little more than a tile, so the faces stay readable.</summary>
        public const float StepX = 0.056f;
        public const float StepZ = 0.078f;

        /// <summary>
        /// Board origin in the local space of the parent (the table's board object). The 12 x 12 grid
        /// is centred on the felt: an off-centre set reads as a hand dealt into the middle of a game.
        /// </summary>
        private static readonly Vector3 Origin = new(-0.336f, TileY, -0.349f);

        /// <summary>Where tiles held by the active seat wait: the near edge, out of the grid.</summary>
        private static readonly Vector3 Tray = new(-0.028f, TileY + 0.022f, -0.47f);

        private readonly Dictionary<int, TileObject> views = new();
        private readonly List<int> held = new();
        private Transform board = null!;

        public FreeTable Table { get; private set; } = null!;

        public bool IsOpen { get; private set; }

        /// <summary>Seat that is acting now; a hotseat game switches it with Tab.</summary>
        public int ActiveSeat { get; private set; }

        /// <summary>Tile the active seat carries, if any.</summary>
        public int? HoldingId => Table.Tiles.FirstOrDefault(tile => tile.HeldBy == ActiveSeat)?.Id;

        public IReadOnlyList<TileObject> Views => views.Values.ToList();

        public int TileViewCount => views.Count;

        /// <summary>Last line the table said back, e.g. why a move was refused.</summary>
        public string LastMessage { get; private set; } = string.Empty;

        public static FreePlayTable Create(Transform board, int seed = 0)
        {
            var host = new GameObject("FreeTable");
            host.transform.SetParent(board, false);

            var table = host.AddComponent<FreePlayTable>();
            table.board = host.transform;
            table.Table = new FreeTable(seed);
            host.SetActive(false);
            return table;
        }

        /// <summary>Where a grid cell sits in the local space of the free table.</summary>
        public static Vector3 CellToLocal(int x, int y) =>
            Origin + new Vector3(x * StepX, 0f, y * StepZ);

        /// <summary>The cell under a point on the board plane; null when the point is off the grid.</summary>
        public static (int X, int Y)? LocalToCell(Vector3 local)
        {
            var x = Mathf.RoundToInt((local.x - Origin.x) / StepX);
            var y = Mathf.RoundToInt((local.z - Origin.z) / StepZ);
            if (x < 0 || x >= FreeTable.Columns || y < 0 || y >= FreeTable.Rows) return null;
            return (x, y);
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            gameObject.SetActive(true);
            ActiveSeat = 0;
            LastMessage = string.Empty;
            SpawnViews();
            Refresh();
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            ClearViews();
            gameObject.SetActive(false);
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        /// <summary>Passes the table to the next seat; this is the hotseat turn of a free-play table.</summary>
        public int NextSeat()
        {
            ActiveSeat = (ActiveSeat + 1) % 4;
            Refresh();
            return ActiveSeat;
        }

        /// <summary>Deals the whole set again, face up, one tile per cell.</summary>
        public void Reset()
        {
            var reply = Table.Submit(ActiveSeat, "reset|-");
            LastMessage = reply.Reason;
            Refresh();
        }

        /// <summary>
        /// One click: take a tile, put a carried tile down on a free cell, or (right click) turn a tile
        /// over. Returns true when the table accepted a move.
        /// </summary>
        public bool TryClick(Ray ray, bool flip)
        {
            if (!IsOpen) return false;

            var tileId = TileUnder(ray);
            if (flip)
            {
                if (!tileId.HasValue) return false;
                return Apply($"flip|{tileId.Value}");
            }

            var holding = HoldingId;
            if (tileId.HasValue)
            {
                if (holding.HasValue) return Apply($"place|{holding.Value}|{Table.Find(tileId.Value)!.X}|{Table.Find(tileId.Value)!.Y}");
                return Apply($"take|{tileId.Value}");
            }

            var cell = CellUnder(ray);
            if (!holding.HasValue || !cell.HasValue) return false;
            return Apply($"place|{holding.Value}|{cell.Value.X}|{cell.Value.Y}");
        }

        /// <summary>Sends a wire move to the shared table and repaints what it accepted.</summary>
        public bool Apply(string wire)
        {
            var reply = Table.Submit(ActiveSeat, wire);
            LastMessage = reply.Accepted ? string.Empty : reply.Reason;
            if (!reply.Accepted) Debug.Log($"[free] refused '{wire}': {reply.Reason}");
            Refresh();
            return reply.Accepted;
        }

        private int? TileUnder(Ray ray)
        {
            if (!Physics.Raycast(ray, out var hit, 5f))
            {
                Debug.Log($"[free] the ray from {ray.origin} hit nothing");
                return null;
            }

            var tile = hit.collider.GetComponentInParent<TileObject>();
            if (tile == null)
            {
                Debug.Log($"[free] the ray hit '{hit.collider.name}' at {hit.point}, which is not a free tile");
                return null;
            }

            foreach (var entry in views)
                if (entry.Value == tile) return entry.Key;
            return null;
        }

        private (int X, int Y)? CellUnder(Ray ray)
        {
            var plane = new Plane(Vector3.up, new Vector3(0f, TileY, 0f));
            if (!plane.Raycast(ray, out var distance)) return null;
            var world = ray.GetPoint(distance);
            return LocalToCell(board.InverseTransformPoint(world));
        }

        private void SpawnViews()
        {
            ClearViews();
            foreach (var tile in Table.Tiles.OrderBy(tile => tile.Id))
            {
                var view = TileObject.Create(board, $"free-{tile.Id}");
                view.SpawnAt(CellToLocal(tile.X, tile.Y));
                views[tile.Id] = view;
            }
        }

        private void ClearViews()
        {
            foreach (var view in views.Values)
                if (view != null) Destroy(view.gameObject);
            views.Clear();
        }

        /// <summary>Puts every tile where the shared table says it is, with the face it shows.</summary>
        public void Refresh()
        {
            held.Clear();

            foreach (var tile in Table.Tiles.OrderBy(tile => tile.Id))
            {
                if (!views.TryGetValue(tile.Id, out var view) || view == null) continue;

                if (tile.FaceUp) view.SetFace(tile.Type);
                else view.SetBack();

                if (tile.HeldBy.HasValue)
                {
                    var slot = held.Count;
                    held.Add(tile.Id);
                    // A carried tile waits in the near tray, raised, so it reads as "in hand".
                    view.SetTarget(Tray + new Vector3(slot * StepX, 0f, 0f), 0f);
                    view.SetHighlight(tile.HeldBy == ActiveSeat);
                }
                else
                {
                    view.SetTarget(CellToLocal(tile.X, tile.Y), 0f);
                    view.SetHighlight(false);
                }
            }

            // The project runs with autoSyncTransforms off, so a collider stays where it was until the
            // physics scene is told to read the transforms again. Without this the first click after a
            // layout change goes through the tile and lands on the felt.
            Physics.SyncTransforms();
        }

        /// <summary>The HUD text of the free table: mode, seat, what is carried and the last refusal.</summary>
        public string StatusText()
        {
            var lines = new List<string>
            {
                Loc.Get("free.title"),
                Loc.Format("free.tileCount", Table.TileCount),
                Loc.Format("free.seat", Text.SeatName((Wind)ActiveSeat))
            };

            var holding = HoldingId;
            lines.Add(holding.HasValue
                ? Loc.Format("free.ready", Text.Tile(Table.Find(holding.Value)!.Type))
                : Loc.Get("free.nothingHeld"));
            lines.Add(Loc.Get("free.hint"));

            if (!string.IsNullOrEmpty(LastMessage))
                lines.Add(Loc.Format("net.rejected", LastMessage));

            return string.Join("\n", lines);
        }
    }
}
