using Mahjong.Localization;
using UnityEngine;

namespace Mahjong.Unity
{
    /// <summary>
    /// A tray of on-screen action buttons along the bottom edge of the frame (У-6): rulebook, pause,
    /// game review, save, language and free table. It uses the same mechanism as the action bar — a
    /// flat cube face, a <see cref="TextMesh"/> caption and a <see cref="BoxCollider"/> — so a ray
    /// from the camera can hit it exactly the way the engine hits the choice buttons. Hot keys stay
    /// as a second way to act; the tray is the first way a player using a touchpad or a stick sees.
    ///
    /// The tray is a child of the camera and floats in front of it, so it stays inside the frame at
    /// any aspect ratio. It never hides the hand or the table: it rides the bottom edge where the
    /// HUD line sits and the well is empty.
    /// </summary>
    public sealed class ControlsPanel : MonoBehaviour
    {
        /// <summary>Height of a tray tile. Kept smaller than a hand tile so the row never looks like it
        /// owns the table.</summary>
        private const float TileHeight = 0.034f;
        private const float TileDepth = 0.004f;
        private const float TileGap = 0.014f;
        private const float PanelY = -0.29f;
        private const float PanelZ = 0.52f;

        private enum Control
        {
            Rulebook,
            Menu,
            Replay,
            Save,
            Language,
            FreePlay
        }

        private static readonly (string Label, KeyCode Key, Control Type, bool LiveOnly)[]
            Controls =
        {
            (Loc.Get("controls.rulebook"), KeyCode.R, Control.Rulebook, false),
            (Loc.Get("controls.menu"), KeyCode.Escape, Control.Menu, false),
            (Loc.Get("controls.replay"), KeyCode.V, Control.Replay, false),
            (Loc.Get("controls.save"), KeyCode.F5, Control.Save, true),
            (Loc.Get("controls.language"), KeyCode.L, Control.Language, false),
            (Loc.Get("controls.freeplay"), KeyCode.F, Control.FreePlay, false)
        };

        private readonly List<ControlTile> tiles = new();
        private Color lastHover = Color.clear;

        public int Count => Controls.Length;
        public bool IsOpen => tiles.Count > 0;
        public IReadOnlyList<ControlTile> Tiles => tiles;

        public static ControlsPanel Create(Transform cameraTransform)
        {
            var root = new GameObject("ControlsPanel");
            root.transform.SetParent(cameraTransform, false);
            root.transform.localPosition = new Vector3(0f, PanelY, PanelZ);
            root.transform.localRotation = Quaternion.identity;

            var panel = root.AddComponent<ControlsPanel>();
            panel.Build();
            return panel;
        }

        private void Build()
        {
            var slot = 0.056f; // fixed pitch; all captions fit because none is long
            var total = Controls.Length * slot - TileGap;
            for (var i = 0; i < Controls.Length; i++)
            {
                var (label, key, type, liveOnly) = Controls[i];
                var tile = ControlTile.Create(transform, label, key, type);
                tile.transform.localPosition = new Vector3(-total * 0.5f + slot * 0.5f + i * slot, 0f, 0f);
                tile.SetEnabled(!liveOnly);
                tiles.Add(tile);
            }

            Physics.SyncTransforms();
        }

        public void Hide()
        {
            foreach (var tile in tiles)
                if (tile != null) Destroy(tile.gameObject);
            tiles.Clear();
        }

        /// <summary>Highlights the tile under the cursor and returns its kind, if any.</summary>
        public ControlTile? Hover(Ray ray)
        {
            var hovered = TileUnderRay(ray);
            foreach (var tile in tiles) tile.SetHovered(tile == hovered);
            return hovered;
        }

        /// <summary>Turns a click into a tray action; only the tray is picked, never a tile behind it.</summary>
        public bool TryPick(Ray ray, out ControlTile tile)
        {
            tile = TileUnderRay(ray);
            return tile != null;
        }

        private ControlTile? TileUnderRay(Ray ray)
        {
            if (tiles.Count == 0) return null;

            // The tiles are flat plates; the table cube and the hand tiles also have colliders and may
            // sit between the camera and the tray. The nearest tray tile wins, exactly like the bar.
            var hits = Physics.RaycastAll(ray, PanelZ * 2f);
            if (hits.Length == 0) return null;
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            foreach (var hit in hits)
            {
                if (hit.collider == null) continue;
                var tile = hit.collider.GetComponentInParent<ControlTile>();
                if (tile != null && tiles.Contains(tile)) return tile;
            }

            return null;
        }

        /// <summary>Tick when the tray is held by the mouse: keep the caption visible for a moment
        /// after release so a player can read what he hovered.</summary>
        public void TickHover()
        {
            // nothing yet — the bar does not need this; left for a future tooltip
        }

        private void OnDisable()
        {
            // Clean up any dangling tiles if the scene is unloaded while the tray is up.
            foreach (var tile in tiles)
                if (tile != null) Destroy(tile.gameObject);
            tiles.Clear();
        }
    }
}
