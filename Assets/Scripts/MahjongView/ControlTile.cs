using UnityEngine;

namespace Mahjong.Unity
{
    /// <summary>
    /// One clickable tile of the on-screen controls tray (У-6): a flat cube face, a caption that
    /// names the action, and the hot key shown in a corner so the tray never replaces the hot keys,
    /// only adds buttons to them.
    ///
    /// Like <see cref="ActionButton"/>, the root carries the collider exactly: the face cube is
    /// scaled non-uniformly, so a label parented to it would be squashed.
    /// </summary>
    public sealed class ControlTile : MonoBehaviour
    {
        /// <summary>Height of a tray tile. Kept smaller than a hand tile so the row never looks like it
        /// owns the table.</summary>
        private const float TileHeight = 0.034f;
        private const float TileDepth = 0.004f;

        public ControlType Type { get; private set; }
        public string Label => caption.text.TrimStart(KeyPrefix);
        public KeyCode Key => key;

        private static readonly Color FaceColour = new(0.90f, 0.91f, 0.88f);
        private static readonly Color HoverColour = new(0.72f, 0.86f, 0.66f);
        private static readonly Color DisabledColour = new(0.55f, 0.56f, 0.55f);
        private static readonly Color CaptionColour = new(0.10f, 0.11f, 0.13f);
        private static readonly Color KeyColour = new(0.35f, 0.42f, 0.32f);

        private const string KeyPrefix = "\u00b7 "; // · separator, same style as the bar's number
        private Renderer face = null!;
        private TextMesh caption = null!;
        private TextMesh keyLabel = null!;
        private KeyCode key;

        public static ControlTile Create(Transform parent, string label, KeyCode key, ControlType type)
        {
            var root = new GameObject($"control-{type}");
            root.transform.SetParent(parent, false);

            var faceObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            faceObject.name = "face";
            faceObject.transform.SetParent(root.transform, false);
            faceObject.transform.localScale = new Vector3(0.05f, TileHeight, TileDepth);
            Object.Destroy(faceObject.GetComponent<Collider>());

            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(0.05f, TileHeight, TileDepth);

            var tile = root.AddComponent<ControlTile>();
            tile.Type = type;
            tile.key = key;
            tile.face = faceObject.GetComponent<Renderer>();
            tile.face.material.color = FaceColour;
            tile.caption = CreateCaption(root.transform, label);
            tile.keyLabel = CreateKeyLabel(root.transform, key);
            return tile;
        }

        private static TextMesh CreateCaption(Transform parent, string text)
        {
            var obj = new GameObject("caption");
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = new Vector3(0f, 0f, -(TileDepth * 0.5f) - 0.0008f);
            obj.transform.localRotation = Quaternion.identity;

            var mesh = obj.AddComponent<TextMesh>();
            mesh.anchor = TextAnchor.UpperCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 64;
            mesh.characterSize = 0.0026f;
            mesh.color = CaptionColour;
            mesh.text = text;
            UiFont.Assign(mesh);
            return mesh;
        }

        private static TextMesh CreateKeyLabel(Transform parent, KeyCode key)
        {
            var obj = new GameObject("key");
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = new Vector3(0.023f, -0.01f, -(TileDepth * 0.5f) - 0.0008f);
            obj.transform.localRotation = Quaternion.identity;

            var mesh = obj.AddComponent<TextMesh>();
            mesh.anchor = TextAnchor.LowerRight;
            mesh.alignment = TextAlignment.UpperRight;
            mesh.fontSize = 48;
            mesh.characterSize = 0.0020f;
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
