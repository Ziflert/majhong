using UnityEngine;

namespace Mahjong.Unity
{
    /// <summary>
    /// One button of the on-screen controls tray (У-6): a flat cube face, the name of the action and
    /// the hot key printed in its corner. The tray never replaces the hot keys, it adds buttons to
    /// them for the player who cannot press one.
    ///
    /// Like <see cref="ActionButton"/>, the root carries the collider exactly: the face cube is
    /// scaled non-uniformly, so a label parented to it would be squashed.
    /// </summary>
    public sealed class ControlTile : MonoBehaviour
    {
        /// <summary>Face size of one tray tile. Six of them sit on a row along the bottom edge of the
        /// frame, and the widest caption ("Сохранить") still fits the face at this width.</summary>
        public const float TileWidth = 0.062f;
        public const float TileHeight = 0.036f;
        private const float TileDepth = 0.004f;

        /// <summary>Colours of the three states a tile can be in. Public so a test can name the state
        /// it expects instead of hard-coding the same colour twice.</summary>
        public static readonly Color FaceColour = new(0.90f, 0.91f, 0.88f);
        public static readonly Color HoverColour = new(0.72f, 0.86f, 0.66f);
        public static readonly Color DisabledColour = new(0.55f, 0.56f, 0.55f);
        public static readonly Color CaptionColour = new(0.10f, 0.11f, 0.13f);
        public static readonly Color KeyColour = new(0.35f, 0.42f, 0.32f);

        public ControlType Type { get; private set; }

        /// <summary>Name of the action, in the language the caption was built in.</summary>
        public string Label => caption.text;

        /// <summary>The hot key as it is printed in the corner, in the form a player would press.</summary>
        public string KeyLabel => keyLabel.text;

        public KeyCode Key => key;

        private Renderer face = null!;
        private TextMesh caption = null!;
        private TextMesh keyLabel = null!;
        private KeyCode key;

        /// <summary>Caption width in world units: a TextMesh glyph advance is about 4.2 units of
        /// <c>characterSize</c>, the same estimate the action bar fits its labels with.</summary>
        public float CaptionWidth => caption.characterSize * 4.2f * caption.text.Length;

        public static ControlTile Create(Transform parent, string label, KeyCode key, ControlType type,
            float captionSize = 0f)
        {
            var root = new GameObject($"control-{type}");
            root.transform.SetParent(parent, false);

            var faceObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            faceObject.name = "face";
            faceObject.transform.SetParent(root.transform, false);
            faceObject.transform.localScale = new Vector3(TileWidth, TileHeight, TileDepth);
            Object.Destroy(faceObject.GetComponent<Collider>());

            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(TileWidth, TileHeight, TileDepth);

            var tile = root.AddComponent<ControlTile>();
            tile.Type = type;
            tile.key = key;
            tile.face = faceObject.GetComponent<Renderer>();
            tile.face.material.color = FaceColour;
            tile.caption = CreateCaption(root.transform, label, captionSize);
            tile.keyLabel = CreateKeyLabel(root.transform, key);
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
            obj.transform.localPosition = new Vector3(0f, TileHeight * 0.13f, -(TileDepth * 0.5f) - 0.0008f);
            obj.transform.localRotation = Quaternion.identity;

            var mesh = obj.AddComponent<TextMesh>();
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 64;
            mesh.characterSize = size > 0f ? size : CaptionSizeFor(text.Length);
            mesh.color = CaptionColour;
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
            mesh.color = KeyColour;
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

        public void SetHovered(bool hovered)
        {
            face.material.color = hovered ? HoverColour : FaceColour;
        }

        public void SetEnabled(bool enabled)
        {
            face.material.color = enabled ? FaceColour : DisabledColour;
            caption.color = enabled ? CaptionColour : new Color(0.25f, 0.25f, 0.25f);
            keyLabel.color = enabled ? KeyColour : new Color(0.20f, 0.20f, 0.20f);
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
        FreePlay
    }
}
