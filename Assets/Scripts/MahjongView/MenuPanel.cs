using System.Collections.Generic;
using Mahjong.Controls;
using Mahjong.Localization;
using UnityEngine;

namespace Mahjong.Unity
{
    /// <summary>
    /// The in-game menu: the table is paused, and the player sets the language, the strength and the
    /// speed of the bots, or asks to save, resume, review, open the rules and go back.
    ///
    /// The rows come from <see cref="GameMenu"/>, which owns what a step and a pick mean, so this
    /// panel only draws them and turns a click into a row number.
    /// </summary>
    public sealed class MenuPanel : MonoBehaviour
    {
        /// <summary>Distance in front of the camera; the action bar and the rulebook sit on the same plane.</summary>
        private const float PanelZ = 0.5f;

        /// <summary>Largest panel that fits the frame half a metre out (see <see cref="RulesPanel"/>).</summary>
        private const float MaxPanelWidth = 0.56f;
        private const float MaxPanelHeight = 0.28f;
        private const float FrameShare = 0.92f;

        private const float RowWidth = 0.44f;
        private const float RowHeight = 0.019f;
        private const float RowStep = 0.0205f;
        private const float FirstRowY = 0.082f;

        private static readonly Color PanelColour = new(0.06f, 0.08f, 0.10f, 0.96f);
        private static readonly Color RowColour = new(0.13f, 0.16f, 0.19f);
        private static readonly Color SelectedRowColour = new(0.22f, 0.34f, 0.26f);
        private static readonly Color CaptionColour = new(0.84f, 0.86f, 0.83f);
        private static readonly Color SelectedCaptionColour = new(0.99f, 0.95f, 0.72f);

        private Transform cameraTransform = null!;
        private TextMesh title = null!;
        private TextMesh footer = null!;
        private Renderer background = null!;

        private readonly List<Renderer> rowFaces = new();
        private readonly List<TextMesh> rowLabels = new();
        private readonly List<BoxCollider> rowColliders = new();

        public bool IsOpen { get; private set; }

        /// <summary>How many rows the menu has; the rows are fixed, their texts are not.</summary>
        public int RowCount => rowLabels.Count;

        public TextMesh Title => title;
        public TextMesh Footer => footer;
        public Renderer Backdrop => background;

        public static MenuPanel Create(Transform cameraTransform)
        {
            var root = new GameObject("MenuPanel");
            root.transform.SetParent(cameraTransform, false);
            root.transform.localPosition = new Vector3(0f, 0f, PanelZ);
            root.transform.localRotation = Quaternion.identity;

            var panel = root.AddComponent<MenuPanel>();
            panel.cameraTransform = cameraTransform;
            panel.background = CreateBackground(root.transform);
            // Sizes follow the rulebook page: a line of 0.0013 is the size that reads at 720p.
            panel.title = CreateText(root.transform, "title", Vector3.zero, 0.0021f,
                TextAnchor.MiddleCenter, SelectedCaptionColour);
            panel.footer = CreateText(root.transform, "footer", Vector3.zero, 0.0012f,
                TextAnchor.MiddleCenter, new Color(0.70f, 0.73f, 0.70f));

            for (var index = 0; index < GameMenu.TotalRows; index++)
                panel.CreateRow(root.transform, index);

            panel.Layout();
            panel.SetOpen(false);
            return panel;
        }

        private void CreateRow(Transform parent, int index)
        {
            var root = new GameObject($"row-{index}");
            root.transform.SetParent(parent, false);

            var faceObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            faceObject.name = "face";
            faceObject.transform.SetParent(root.transform, false);
            faceObject.transform.localScale = new Vector3(RowWidth, RowHeight, ActionButton.Depth);
            // picking goes through the row's own box, exactly like the action bar
            Object.Destroy(faceObject.GetComponent<Collider>());

            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(RowWidth, RowHeight, ActionButton.Depth);

            var face = faceObject.GetComponent<Renderer>();
            face.material = new Material(Shader.Find("Unlit/Color"));
            face.material.color = RowColour;

            var label = CreateText(root.transform, "label", Vector3.zero, 0.0013f,
                TextAnchor.MiddleLeft, CaptionColour);
            label.transform.localPosition = new Vector3(-RowWidth * 0.5f + 0.008f, 0f, -(ActionButton.Depth * 0.5f) - 0.0008f);

            rowFaces.Add(face);
            rowLabels.Add(label);
            rowColliders.Add(box);
        }

        private static Renderer CreateBackground(Transform parent)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "backdrop";
            quad.transform.SetParent(parent, false);
            quad.transform.localRotation = Quaternion.identity;
            Object.Destroy(quad.GetComponent<Collider>());
            var renderer = quad.GetComponent<Renderer>();
            renderer.material = new Material(Shader.Find("Unlit/Color"));
            renderer.material.color = PanelColour;
            return renderer;
        }

        private static TextMesh CreateText(Transform parent, string name, Vector3 position, float size,
            TextAnchor anchor, Color colour)
        {
            var host = new GameObject(name);
            host.transform.SetParent(parent, false);
            host.transform.localPosition = position;
            host.transform.localRotation = Quaternion.identity;

            var mesh = host.AddComponent<TextMesh>();
            mesh.anchor = anchor;
            mesh.alignment = anchor == TextAnchor.MiddleLeft ? TextAlignment.Left : TextAlignment.Center;
            mesh.fontSize = 64;
            mesh.characterSize = size;
            mesh.color = colour;
            mesh.richText = false;
            UiFont.Assign(mesh);
            return mesh;
        }

        /// <summary>Fits the panel to the frame and lays the rows out again; called every visible frame.</summary>
        public void Layout()
        {
            var camera = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null;
            var halfHeight = camera != null
                ? Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * PanelZ
                : MaxPanelHeight * 0.5f;
            var halfWidth = camera != null ? halfHeight * camera.aspect : MaxPanelWidth * 0.5f;

            var width = Mathf.Min(MaxPanelWidth, halfWidth * 2f * FrameShare);
            var height = Mathf.Min(MaxPanelHeight, halfHeight * 2f * FrameShare);

            background.transform.localScale = new Vector3(width, height, 1f);
            title.transform.localPosition = new Vector3(0f, height * 0.5f - 0.018f, -0.0009f);
            footer.transform.localPosition = new Vector3(0f, -height * 0.5f + 0.013f, -0.0009f);

            var rowWidth = Mathf.Min(RowWidth, width * 0.94f);
            for (var index = 0; index < rowColliders.Count; index++)
            {
                var y = FirstRowY - index * RowStep;
                var box = rowColliders[index];
                box.transform.localPosition = new Vector3(0f, y, 0f);
                box.size = new Vector3(rowWidth, RowHeight, ActionButton.Depth);
                rowFaces[index].transform.localScale = new Vector3(rowWidth, RowHeight, ActionButton.Depth);
                // The label hangs on the row, and the row is what moves: its own y stays zero.
                rowLabels[index].transform.localPosition =
                    new Vector3(-rowWidth * 0.5f + 0.008f, 0f, -(ActionButton.Depth * 0.5f) - 0.0008f);
            }
        }

        private void Update()
        {
            if (IsOpen) Layout();
        }

        public void SetOpen(bool open)
        {
            IsOpen = open;
            gameObject.SetActive(open);
        }

        public void Close() => SetOpen(false);

        /// <summary>Redraws every row from the model: caption, value, and which row the cursor is on.</summary>
        public void Refresh(GameMenu menu)
        {
            title.text = Loc.Get("menu.title");
            footer.text = Loc.Get("menu.hint");
            for (var index = 0; index < rowLabels.Count; index++)
            {
                if (index >= menu.RowCount)
                {
                    rowLabels[index].text = string.Empty;
                    continue;
                }

                var selected = index == menu.Index;
                rowLabels[index].text = (selected ? "▸ " : "   ") + menu.RowAt(index);
                rowLabels[index].color = selected ? SelectedCaptionColour : CaptionColour;
                rowFaces[index].material.color = selected ? SelectedRowColour : RowColour;
            }
        }

        /// <summary>The caption of one row as it is drawn; the tests read it instead of the screen.</summary>
        public string RowText(int index) => index >= 0 && index < rowLabels.Count ? rowLabels[index].text : string.Empty;

        /// <summary>Height of a row's own strip; the test compares it with the caption it carries.</summary>
        public float RowStripHeight(int index) => index >= 0 && index < rowColliders.Count
            ? rowColliders[index].transform.position.y
            : float.NaN;

        /// <summary>Height of a row's caption; a caption away from its strip is a layout bug.</summary>
        public float RowLabelHeight(int index) => index >= 0 && index < rowLabels.Count
            ? rowLabels[index].transform.position.y
            : float.NaN;

        /// <summary>Turns a click into a row number; false when the ray missed every row.</summary>
        public bool TryPick(Ray ray, out int row)
        {
            row = -1;
            if (!IsOpen) return false;

            var hits = Physics.RaycastAll(ray, 5f);
            var nearest = float.MaxValue;
            foreach (var hit in hits)
            {
                var index = rowColliders.FindIndex(box => box != null && hit.collider == box);
                if (index < 0 || hit.distance >= nearest) continue;
                nearest = hit.distance;
                row = index;
            }

            return row >= 0;
        }
    }
}
