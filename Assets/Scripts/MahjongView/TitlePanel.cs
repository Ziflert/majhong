using System.Collections.Generic;
using Mahjong.Controls;
using Mahjong.Localization;
using UnityEngine;

namespace Mahjong.Unity
{
    /// <summary>
    /// The welcome screen the game opens on: the name of the game, the ways into a game — new, continue,
    /// load, and the two modes that are still being built — and the settings a table is created with:
    /// the rules profile, the language, the bots and the result screen.
    ///
    /// The rows and what a step and a pick mean live in <see cref="TitleScreen"/>; this panel only draws
    /// them and turns a click into a row number, the same way <see cref="MenuPanel"/> draws the menu.
    /// </summary>
    public sealed class TitlePanel : MonoBehaviour
    {
        /// <summary>Distance in front of the camera; every page of the prototype sits on this plane.</summary>
        private const float PanelZ = 0.5f;

        /// <summary>Largest panel that fits the frame half a metre out (see <see cref="RulesPanel"/>).</summary>
        private const float MaxPanelWidth = 0.58f;
        private const float MaxPanelHeight = 0.30f;
        private const float FrameShare = 0.95f;

        private const float RowWidth = 0.50f;
        private const float MaxRowStep = 0.0176f;
        private const float MaxRowHeight = 0.0166f;

        /// <summary>From the top of the panel to the game name, the profile line and the first row.</summary>
        private const float TitleOffset = 0.024f;
        private const float SubtitleOffset = 0.042f;
        private const float RowsOffset = 0.054f;

        /// <summary>From the bottom of the panel to the status line and the key hint.</summary>
        private const float NoticeOffset = 0.030f;
        private const float FooterOffset = 0.012f;

        private static readonly Color PanelColour = new(0.05f, 0.07f, 0.09f, 0.97f);
        private static readonly Color RowColour = new(0.13f, 0.16f, 0.19f);
        private static readonly Color SelectedRowColour = new(0.22f, 0.34f, 0.26f);
        private static readonly Color CaptionColour = new(0.84f, 0.86f, 0.83f);
        private static readonly Color SelectedCaptionColour = new(0.99f, 0.95f, 0.72f);
        private static readonly Color PlannedCaptionColour = new(0.55f, 0.57f, 0.55f);
        private static readonly Color TitleColour = new(0.98f, 0.92f, 0.74f);
        private static readonly Color SubtitleColour = new(0.72f, 0.78f, 0.72f);
        private static readonly Color FooterColour = new(0.66f, 0.70f, 0.66f);

        private Transform cameraTransform = null!;
        private TextMesh title = null!;
        private TextMesh subtitle = null!;
        private TextMesh notice = null!;
        private TextMesh footer = null!;
        private Renderer background = null!;

        private readonly List<GameObject> rowRoots = new();
        private readonly List<Renderer> rowFaces = new();
        private readonly List<TextMesh> rowLabels = new();
        private readonly List<TextMesh> rowValues = new();
        private readonly List<BoxCollider> rowColliders = new();

        public bool IsOpen { get; private set; }

        /// <summary>Row strips the panel can draw; the load page uses fewer than the main page.</summary>
        public int RowCount => rowLabels.Count;

        public TextMesh Title => title;
        public TextMesh Subtitle => subtitle;
        public TextMesh Notice => notice;
        public TextMesh Footer => footer;
        public Renderer Backdrop => background;

        public static TitlePanel Create(Transform cameraTransform)
        {
            var root = new GameObject("TitlePanel");
            root.transform.SetParent(cameraTransform, false);
            root.transform.localPosition = new Vector3(0f, 0f, PanelZ);
            root.transform.localRotation = Quaternion.identity;

            var panel = root.AddComponent<TitlePanel>();
            panel.cameraTransform = cameraTransform;
            panel.background = CreateBackground(root.transform);
            panel.title = CreateText(root.transform, "title", Vector3.zero, 0.0032f,
                TextAnchor.MiddleCenter, TitleColour);
            panel.subtitle = CreateText(root.transform, "subtitle", Vector3.zero, 0.0012f,
                TextAnchor.MiddleCenter, SubtitleColour);
            panel.notice = CreateText(root.transform, "notice", Vector3.zero, 0.0011f,
                TextAnchor.MiddleCenter, SelectedCaptionColour);
            panel.footer = CreateText(root.transform, "footer", Vector3.zero, 0.0010f,
                TextAnchor.MiddleCenter, FooterColour);

            for (var index = 0; index < TitleScreen.MaxRows; index++)
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
            faceObject.transform.localScale = new Vector3(RowWidth, MaxRowHeight, ActionButton.Depth);
            // picking goes through the row's own box, exactly like the menu and the action bar
            Object.Destroy(faceObject.GetComponent<Collider>());

            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(RowWidth, MaxRowHeight, ActionButton.Depth);

            var face = faceObject.GetComponent<Renderer>();
            face.material = new Material(Shader.Find("Unlit/Color"));
            face.material.color = RowColour;

            var label = CreateText(root.transform, "label", Vector3.zero, 0.0011f,
                TextAnchor.MiddleLeft, CaptionColour);
            var value = CreateText(root.transform, "value", Vector3.zero, 0.0011f,
                TextAnchor.MiddleRight, SelectedCaptionColour);

            rowRoots.Add(root);
            rowFaces.Add(face);
            rowLabels.Add(label);
            rowValues.Add(value);
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
            mesh.alignment = anchor == TextAnchor.MiddleLeft ? TextAlignment.Left : TextAlignment.Right;
            if (anchor == TextAnchor.MiddleCenter) mesh.alignment = TextAlignment.Center;
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

            var top = height * 0.5f;
            title.transform.localPosition = new Vector3(0f, top - TitleOffset, -0.0009f);
            subtitle.transform.localPosition = new Vector3(0f, top - SubtitleOffset, -0.0009f);
            notice.transform.localPosition = new Vector3(0f, -height * 0.5f + NoticeOffset, -0.0009f);
            footer.transform.localPosition = new Vector3(0f, -height * 0.5f + FooterOffset, -0.0009f);

            // The rows take whatever is left between the profile line and the status line: a taller
            // frame gets wider strips, a short one squeezes them instead of pushing them off the panel.
            var rowsTop = top - RowsOffset;
            var rowsBottom = -height * 0.5f + NoticeOffset + 0.013f;
            var step = Mathf.Min(MaxRowStep, Mathf.Max(0.008f, (rowsTop - rowsBottom) / TitleScreen.MaxRows));
            var rowHeight = Mathf.Min(MaxRowHeight, step * 0.94f);
            var rowWidth = Mathf.Min(RowWidth, width * 0.94f);

            for (var index = 0; index < rowColliders.Count; index++)
            {
                var y = rowsTop - index * step;
                var box = rowColliders[index];
                box.transform.localPosition = new Vector3(0f, y, 0f);
                box.size = new Vector3(rowWidth, rowHeight, ActionButton.Depth);
                rowFaces[index].transform.localScale = new Vector3(rowWidth, rowHeight, ActionButton.Depth);
                // The two captions hang on the row, and the row is what moves.
                rowLabels[index].transform.localPosition =
                    new Vector3(-rowWidth * 0.5f + 0.010f, 0f, -(ActionButton.Depth * 0.5f) - 0.0008f);
                rowValues[index].transform.localPosition =
                    new Vector3(rowWidth * 0.5f - 0.010f, 0f, -(ActionButton.Depth * 0.5f) - 0.0008f);
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

        /// <summary>Redraws every row from the model: caption, value and which row the cursor is on.</summary>
        public void Refresh(TitleScreen screen)
        {
            title.text = Loc.Get("title.title");
            subtitle.text = screen.Page == TitlePage.Load
                ? Loc.Get("title.loadTitle")
                : Loc.Format("title.subtitle", Text.VariantName(screen.Settings.Variant));
            notice.text = screen.Status;
            footer.text = screen.Page == TitlePage.Load ? Loc.Get("title.loadHint") : Loc.Get("title.hint");

            for (var index = 0; index < rowLabels.Count; index++)
            {
                var used = index < screen.RowCount;
                if (rowRoots[index].activeSelf != used) rowRoots[index].SetActive(used);
                if (!used)
                {
                    rowLabels[index].text = string.Empty;
                    rowValues[index].text = string.Empty;
                    continue;
                }

                var row = screen.RowAt(index);
                var selected = index == screen.Index;
                rowLabels[index].text = (selected ? "▸ " : "   ") + row.Label;
                rowValues[index].text = row.Value;
                rowLabels[index].color = selected
                    ? SelectedCaptionColour
                    : row.Ready ? CaptionColour : PlannedCaptionColour;
                rowValues[index].color = row.Ready ? SelectedCaptionColour : PlannedCaptionColour;
                rowFaces[index].material.color = selected ? SelectedRowColour : RowColour;
            }
        }

        /// <summary>The row as it is drawn; the tests read it instead of the screen.</summary>
        public string RowText(int index) => index >= 0 && index < rowLabels.Count
            ? rowValues[index].text.Length == 0
                ? rowLabels[index].text
                : $"{rowLabels[index].text}: {rowValues[index].text}"
            : string.Empty;

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
