using System.Collections.Generic;
using System.Linq;
using Mahjong.Localization;
using UnityEngine;

namespace Mahjong.Unity
{
    /// <summary>
    /// A tray of on-screen action buttons in the bottom-left corner of the frame (У-6): rulebook,
    /// pause, game review, save, language and free table. It uses the same mechanism as the action
    /// bar — a flat cube face, a <see cref="TextMesh"/> caption and a <see cref="BoxCollider"/> — so a
    /// ray from the camera can hit it exactly the way the engine hits the choice buttons. Hot keys
    /// stay as a second way to act; the tray is the first way a player using a touchpad or a stick sees.
    ///
    /// The tray is a child of the camera and floats in front of it, so it stays inside the frame at
    /// any aspect ratio (<see cref="Layout"/>). It takes a corner: the hand of the player stands in the
    /// middle of the bottom edge of the play view, and a row across the whole frame would hide the
    /// very tiles a player is about to click.
    /// </summary>
    public sealed class ControlsPanel : MonoBehaviour
    {
        private const float TileGap = 0.010f;
        private const float PanelZ = 0.52f;

        /// <summary>Air between the row and the edges of the frame, at the tray's own depth.</summary>
        private const float EdgeMargin = 0.010f;

        /// <summary>
        /// Share of the frame width the row may take. The hand stands in the middle of the bottom
        /// edge of the play view, so a row across the whole frame would cover the tiles the player
        /// is about to discard: the tray takes a corner instead, the way the HUD takes the other.
        /// </summary>
        private const float FrameShare = 0.45f;

        /// <summary>Frame measured when there is no camera to ask: half of a 34° frame at the tray depth.</summary>
        private const float FallbackHalfHeight = 0.159f;
        private const float FallbackAspect = 16f / 9f;

        /// <summary>
        /// The six controls in the order they sit on the row: the hot key, the action the click turns
        /// into, the catalogue key of the caption and whether the tile waits for a played hand. The
        /// caption is read from the catalogue when the row is built, so a language switch only has to
        /// rebuild the row — the same way <see cref="ActionBar"/> does it.
        /// </summary>
        private static readonly (KeyCode Key, ControlType Type, string Caption, bool LiveOnly)[] Rows =
        {
            (KeyCode.R, ControlType.Rulebook, "controls.rulebook", false),
            (KeyCode.Escape, ControlType.Menu, "controls.menu", false),
            (KeyCode.V, ControlType.Replay, "controls.replay", false),
            (KeyCode.F5, ControlType.Save, "controls.save", true),
            (KeyCode.L, ControlType.Language, "controls.language", false),
            (KeyCode.F, ControlType.FreePlay, "controls.freeplay", false)
        };

        private readonly List<ControlTile> tiles = new();

        public int Count => Rows.Length;
        public bool IsOpen => tiles.Count > 0;
        public IReadOnlyList<ControlTile> Tiles => tiles;

        public static ControlsPanel Create(Transform cameraTransform)
        {
            var root = new GameObject("ControlsPanel");
            root.transform.SetParent(cameraTransform, false);
            // The place is provisional: Layout puts the row on the bottom edge of the frame, which
            // depends on the field of view and the aspect of the camera it hangs on.
            root.transform.localPosition = new Vector3(0f, -FallbackHalfHeight + EdgeMargin, PanelZ);
            root.transform.localRotation = Quaternion.identity;   

            // Adding the component runs OnEnable, which builds the row; a hidden tray builds it when
            // it is shown again.
            var panel = root.AddComponent<ControlsPanel>();
            return panel;
        }

        private void Build()
        {
            if (tiles.Count > 0) return;

            // One size for the whole row: the longest caption decides, so every button reads as the same
            // kind of button instead of three sizes of label.
            var captionSize = ControlTile.CaptionSizeFor(Rows.Max(row => Loc.Get(row.Caption).Length));

            for (var i = 0; i < Rows.Length; i++)
            {
                var (key, type, caption, liveOnly) = Rows[i];
                var tile = ControlTile.Create(transform, Loc.Get(caption), key, type, captionSize);
                tile.SetEnabled(!liveOnly);
                tiles.Add(tile);
            }

            Layout();

            // Colliders are picking targets: sync them right away, otherwise the first click after a
            // rebuild hits wherever the physics scene still has the previous row.
            Physics.SyncTransforms();
        }

        /// <summary>
        /// Puts the row in the bottom-left corner of the frame and keeps it there at any field of view
        /// and aspect ratio. The tray used to stand at a fixed height in camera space: it was placed
        /// for a 60° view and hung two frames' worth below the bottom edge of the game's own 34°
        /// camera, so the buttons that У-6 added were never on the screen in a real game. A row wider
        /// than its share of the frame is scaled down whole, so a narrow window loses no button.
        /// </summary>
        public void Layout()
        {
            if (tiles.Count == 0) return;

            var camera = transform.parent != null ? transform.parent.GetComponent<Camera>() : null;
            var halfHeight = camera != null
                ? Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * PanelZ
                : FallbackHalfHeight;
            var aspect = camera != null && camera.aspect > 0f ? camera.aspect : FallbackAspect;
            var halfWidth = halfHeight * aspect;

            var slot = ControlTile.TileWidth + TileGap;
            var total = Rows.Length * slot - TileGap;
            // The size of the interface (8.10) is a wish, the frame is a fact: the row grows with the
            // setting, but never past its share of the frame, so no button leaves the screen.
            var scale = Mathf.Min(UiStyle.Scale, halfWidth * 2f * FrameShare / total);

            transform.localScale = Vector3.one * scale;
            transform.localPosition = new Vector3(
                -halfWidth + EdgeMargin + total * 0.5f * scale,
                -halfHeight + EdgeMargin + ControlTile.TileHeight * 0.5f * scale,
                PanelZ);

            for (var i = 0; i < tiles.Count; i++)
            {
                tiles[i].transform.localPosition = new Vector3(
                    -total * 0.5f + ControlTile.TileWidth * 0.5f + i * slot, 0f, 0f);
            }

            Physics.SyncTransforms();
        }

        /// <summary>The game view can be resized and the camera zoomed: the row follows the frame.</summary>
        private void Update()
        {
            if (tiles.Count > 0) Layout();
        }

        /// <summary>
        /// Rebuilds the captions in the language that is current now. A hidden tray has no captions
        /// to rebuild — it reads the catalogue when it is shown again.
        /// </summary>
        public void Relabel()
        {
            if (tiles.Count == 0) return;
            Hide();
            Build();
        }

        public void Hide()
        {
            Clear();
        }

        /// <summary>Highlights the tile under the cursor and returns it, if the ray hits one.</summary>
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

        private void OnEnable()
        {
            // The tray is hidden while a full-screen page is up; coming back it needs its row again,
            // built in the language the player has chosen by then.
            Build();
        }

        private void OnDisable()
        {
            Clear();
        }

        private void Clear()
        {
            foreach (var tile in tiles)
                if (tile != null) Destroy(tile.gameObject);
            tiles.Clear();
        }
    }
}
