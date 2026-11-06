using Mahjong.Controls;
using Mahjong.Rules;
using UnityEngine;

namespace Mahjong.Unity
{
    /// <summary>
    /// One clickable button of the action bar: a rounded plate of the palette with a <c>TextMesh</c>
    /// caption on the camera side.
    ///
    /// The root is an empty object and carries the collider, exactly like <see cref="TileObject"/>:
    /// the plate is a mesh of its own and the caption is a child of the root, so the caption keeps its
    /// own shape whatever the plate is stretched to.
    /// </summary>
    public sealed class ActionButton : MonoBehaviour
    {
        /// <summary>Thickness along the camera axis; the collider follows it.</summary>
        public const float Depth = 0.004f;

        private UiPlate face = null!;
        private TextMesh label = null!;
        private bool hovered;
        private bool enabled = true;
        private int paintedRevision = -1;

        public ActionOption Option { get; private set; } = null!;
        public LegalAction Action => Option.Action;
        public string Label => Option.Label;
        public TextMesh Caption => label;

        /// <summary>The plate the button is drawn on; a test reads its skin instead of a colour.</summary>
        public UiPlate Face => face;

        /// <summary>True while the pointer is over the button.</summary>
        public bool IsHovered => hovered;

        /// <summary>False while the button cannot be pressed.</summary>
        public bool IsEnabled => enabled;

        public static ActionButton Create(Transform parent, ActionOption option, float width, float height)
        {
            var root = new GameObject($"action-{option.Number}");
            root.transform.SetParent(parent, false);

            var plate = UiPlate.Create(root.transform, UiSkin.Button, "face");
            plate.SetSize(width, height);
            plate.SetPosition(0f, 0f, -(Depth * 0.5f) - 0.0002f);

            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(width, height, Depth);

            var button = root.AddComponent<ActionButton>();
            button.Option = option;
            button.face = plate;
            button.label = CreateLabel(root.transform, width, height, $"{option.Number}. {option.Label}");
            button.Repaint();
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
            mesh.text = text;
            UiFont.Assign(mesh);
            return mesh;
        }

        public void SetHovered(bool isHovered)
        {
            if (hovered == isHovered) return;
            hovered = isHovered;
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
            // The palette can change while the bar is on screen; one frame later the bar follows it.
            if (paintedRevision != UiStyle.Revision) Repaint();
        }

        /// <summary>
        /// Paints the button from three things and nothing else: whether it can be pressed, whether the
        /// pointer is on it and which palette is in effect — the same three states a tray tile has.
        /// </summary>
        private void Repaint()
        {
            paintedRevision = UiStyle.Revision;
            if (face == null) return;

            var skin = !enabled && !hovered
                ? UiSkin.ButtonDisabled
                : hovered ? UiSkin.ButtonHover : UiSkin.Button;
            face.SetSkin(skin);

            label.color = skin == UiSkin.ButtonHover
                ? UiStyle.Of(UiRole.TextOnAccent)
                : UiStyle.Of(enabled ? UiRole.TextMain : UiRole.TextMuted);
        }

        public override string ToString() => Option.ToString();
    }
}
