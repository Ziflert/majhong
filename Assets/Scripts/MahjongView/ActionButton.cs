using Mahjong.Controls;
using Mahjong.Rules;
using UnityEngine;

namespace Mahjong.Unity
{
    /// <summary>
    /// One clickable button of the action bar, built from primitives: a flat cube face with a
    /// TextMesh caption on the camera side.
    ///
    /// The root is an empty object and carries the collider, exactly like <see cref="TileObject"/>:
    /// the face cube is scaled non-uniformly, so a label parented to it would be squashed.
    /// </summary>
    public sealed class ActionButton : MonoBehaviour
    {
        /// <summary>Thickness along the camera axis; the collider follows it.</summary>
        public const float Depth = 0.004f;

        private static readonly Color FaceColour = new(0.90f, 0.91f, 0.88f);
        private static readonly Color HoverColour = new(0.72f, 0.86f, 0.66f);
        private static readonly Color DisabledColour = new(0.55f, 0.56f, 0.55f);
        private static readonly Color CaptionColour = new(0.10f, 0.11f, 0.13f);

        private Renderer face = null!;
        private TextMesh label = null!;

        public ActionOption Option { get; private set; } = null!;
        public LegalAction Action => Option.Action;
        public string Label => Option.Label;
        public TextMesh Caption => label;

        public static ActionButton Create(Transform parent, ActionOption option, float width, float height)
        {
            var root = new GameObject($"action-{option.Number}");
            root.transform.SetParent(parent, false);

            var faceObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            faceObject.name = "face";
            faceObject.transform.SetParent(root.transform, false);
            faceObject.transform.localScale = new Vector3(width, height, Depth);
            // picking goes through the root collider, so the cube's own one is removed
            Object.Destroy(faceObject.GetComponent<Collider>());

            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(width, height, Depth);

            var button = root.AddComponent<ActionButton>();
            button.Option = option;
            button.face = faceObject.GetComponent<Renderer>();
            button.face.material.color = FaceColour;
            button.label = CreateLabel(root.transform, width, height, $"{option.Number}. {option.Label}");
            return button;
        }

        private static TextMesh CreateLabel(Transform parent, float width, float height, string text)
        {
            var labelObject = new GameObject("label");
            labelObject.transform.SetParent(parent, false);
            // the bar hangs in front of the camera, which looks along +z: the caption has to sit
            // slightly closer to the camera than the face. Identity rotation reads correctly,
            // the same way the HUD status line does.
            labelObject.transform.localPosition = new Vector3(0f, 0f, -(Depth * 0.5f) - 0.0008f);
            labelObject.transform.localRotation = Quaternion.identity;

            var mesh = labelObject.AddComponent<TextMesh>();
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 64;
            // A TextMesh glyph advance is about four characterSize units wide, so the caption is
            // fitted to the label length: short labels stay big, long ones shrink to fit the face.
            var fitWidth = (width * 0.92f) / Mathf.Max(3, text.Length) / 4.2f;
            mesh.characterSize = Mathf.Min(height * 0.11f, fitWidth);
            mesh.color = CaptionColour;
            mesh.text = text;
            UiFont.Assign(mesh);
            return mesh;
        }

        public void SetHovered(bool hovered)
        {
            face.material.color = hovered ? HoverColour : FaceColour;
        }

        public void SetEnabled(bool enabled)
        {
            face.material.color = enabled ? FaceColour : DisabledColour;
            label.color = enabled ? CaptionColour : new Color(0.25f, 0.25f, 0.25f);
        }

        public override string ToString() => Option.ToString();
    }
}
