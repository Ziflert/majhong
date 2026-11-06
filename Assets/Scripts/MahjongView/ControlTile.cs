using Mahjong.Controls;
using UnityEngine;

namespace Mahjong.Unity
{
    /// <summary>
    /// One button of the on-screen controls tray (У-6): a rounded plate of the palette, the name of the
    /// action and the hot key printed in its corner. The tray never replaces the hot keys, it adds
    /// buttons to them for the player who cannot press one.
    ///
    /// The root carries the collider exactly: the plate is a mesh of its own and the two captions are
    /// children of the root, so neither of them inherits the plate's shape.
    /// </summary>
    public sealed class ControlTile : MonoBehaviour
    {
        /// <summary>Face size of one tray tile. Six of them sit on a row along the bottom edge of the
        /// frame, and the widest caption ("Сохранить") still fits the face at this width.</summary>
        public const float TileWidth = 0.062f;
        public const float TileHeight = 0.036f;
        private const float TileDepth = 0.004f;

        public ControlType Type { get; private set; }

        /// <summary>Name of the action, in the language the caption was built in.</summary>
        public string Label => caption.text;

        /// <summary>The hot key as it is printed in the corner, in the form a player would press.</summary>
        public string KeyLabel => keyLabel.text;

        public KeyCode Key => key;

        /// <summary>The plate this tile is drawn on; a test reads its skin instead of a colour.</summary>
        public UiPlate Face => face;

        /// <summary>True while the pointer is over the tile.</summary>
        public bool IsHovered => hovered;

        /// <summary>False while the tile waits for a played hand.</summary>
        public bool IsEnabled => enabled;

        private UiPlate face = null!;
        private TextMesh caption = null!;
        private TextMesh keyLabel = null!;
        private KeyCode key;
        private bool hovered;
        private bool enabled = true;
        private int paintedRevision = -1;

        /// <summary>Caption width in world units: a TextMesh glyph advance is about 4.2 units of
        /// <c>characterSize</c>, the same estimate the action bar fits its labels with.</summary>
        public float CaptionWidth => caption.characterSize * 4.2f * caption.text.Length;

        public static ControlTile Create(Transform parent, string label, KeyCode key, ControlType type,
            float captionSize = 0f)
        {
            var root = new GameObject($"control-{type}");
            root.transform.SetParent(parent, false);

            var plate = UiPlate.Create(root.transform, UiSkin.Button, "face");
            plate.SetSize(TileWidth, TileHeight);
            plate.SetPosition(0f, 0f, -(TileDepth * 0.5f) - 0.0002f);

            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(TileWidth, TileHeight, TileDepth);

            var tile = root.AddComponent<ControlTile>();
            tile.Type = type;
            tile.key = key;
            tile.face = plate;
            tile.caption = CreateCaption(root.transform, label, captionSize);
            tile.keyLabel = CreateKeyLabel(root.transform, key);
            tile.Repaint();
            return tile;
        }

        /// <summary>
        /// The size a caption of <paramref name="length"/> characters is drawn at. Short captions stay
        /// big, long ones shrink to fit the face: a caption that runs over its neighbours is unreadable
        /// and hides the next button. The tray asks for the size of the longest caption it has and gives
        /// the same one to every tile, because captions in three different sizes read as three
        /// different kinds of button.
        /// </summary>
        public static float CaptionSizeFor(int length)
        {
            var fitWidth = TileWidth * 0.90f / Mathf.Max(3, length) / 4.2f;
            return Mathf.Min(TileHeight * 0.10f, fitWidth);
        }

        private static TextMesh CreateCaption(Transform parent, string text, float size)
        {
            var obj = new GameObject("caption");
            obj.transform.SetParent(parent, false);
            // The caption sits above the middle of the face: the corner below it belongs to the key.
            obj.transform.localPosition = new Vector3(0f, TileHeight * 0.15f, -(TileDepth * 0.5f) - 0.0008f);
            obj.transform.localRotation = Quaternion.identity;

            var mesh = obj.AddComponent<TextMesh>();
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 64;
            mesh.characterSize = size > 0f ? size : CaptionSizeFor(text.Length);
            mesh.color = Color.white;
            mesh.text = text;
            UiFont.Assign(mesh);
            return mesh;
        }

        private static TextMesh CreateKeyLabel(Transform parent, KeyCode key)
        {
            var obj = new GameObject("key");
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition =
                new Vector3(TileWidth * 0.28f, -TileHeight * 0.27f, -(TileDepth * 0.5f) - 0.0016f);
            obj.transform.localRotation = Quaternion.identity;

            var mesh = obj.AddComponent<TextMesh>();
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 48;
            mesh.characterSize = 0.0018f;
            mesh.color = Color.white;
            mesh.text = KeyString(key);
            UiFont.Assign(mesh);
            return mesh;
        }

        private static string KeyString(KeyCode key)
        {
            return key switch
            {
                KeyCode.R => "R",
                KeyCode.Escape => "Esc",
                KeyCode.V => "V",
                KeyCode.F5 => "F5",
                KeyCode.L => "L",
                KeyCode.F => "F",
                _ => key.ToString()
            };
        }

        public void SetHovered(bool isHovered)
        {
            if (hovered == isHovered) return;
            hovered = isHovered;
            Repaint();
        }

        public void SetCaption(string text, string keyText)
        {
            caption.text = text;
            keyLabel.text = keyText;
            Repaint();
        }

        public void SetEnabled(bool isEnabled)
        {
            if (enabled == isEnabled) return;
            enabled = isEnabled;
            Repaint();
        }

        private void Update()
        {
            // The palette can change while the tray is on screen; one frame later the tile follows it.
            if (paintedRevision != UiStyle.Revision) Repaint();
        }

        /// <summary>
        /// Paints the tile from three things and nothing else: whether it can be pressed, whether the
        /// pointer is on it and which palette is in effect. The three states are the three skins of the
        /// interface, so a tray tile looks like an action button and like a menu row.
        /// </summary>
        private void Repaint()
        {
            paintedRevision = UiStyle.Revision;
            if (face == null) return;

            var skin = !enabled && !hovered
                ? UiSkin.ButtonDisabled
                : hovered ? UiSkin.ButtonHover : UiSkin.Button;
            face.SetSkin(skin);

            var onAccent = skin == UiSkin.ButtonHover;
            var textRole = onAccent ? UiRole.TextOnAccent : enabled ? UiRole.TextMain : UiRole.TextMuted;
            caption.color = UiStyle.Of(textRole);
            keyLabel.color = UiStyle.Of(onAccent ? UiRole.TextOnAccent : UiRole.Accent, enabled ? 1f : 0.45f);
        }
    }

    /// <summary>The six actions the tray offers; the same set the hot keys fire.</summary>
    public enum ControlType
    {
        Rulebook,
        Menu,
        Replay,
        Save,
        Language,
        FreePlay,
        Hints,
        MeldGuide
    }
}
