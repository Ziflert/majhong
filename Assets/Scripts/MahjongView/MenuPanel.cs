using System.Collections.Generic;
using Mahjong.Controls;
using Mahjong.Localization;
using UnityEngine;

namespace Mahjong.Unity
{
    /// <summary>
    /// The pause menu, drawn to the reference sheet (<c>examples/Эскизы UI Маджонг.html</c>, frame 2):
    /// a dimmed table behind a single card, a gold serif title, one row per setting with its value on
    /// the right, and the hint line along the bottom of the card.
    ///
    /// The rows themselves come from <see cref="GameMenu"/>, which owns what a step and a pick mean.
    /// This panel only draws them, so the behaviour is checked headless and the panel is free to be
    /// repainted without touching a single rule.
    ///
    /// Numbers are the reference's own design pixels on a 1280 × 720 sheet and are converted into
    /// camera space, so the card keeps its proportions on any resolution.
    /// </summary>
    public sealed class MenuPanel : MonoBehaviour
    {
        /// <summary>Plane the interface hangs on, half a metre in front of the camera.</summary>
        private const float PanelZ = 0.5f;

        /// <summary>Height of the design sheet the numbers below are measured on.</summary>
        private const float DesignHeight = 720f;

        // ------------------------------------------------------------------ design numbers (CSS px)

        private const float CardWidth = 486f;
        private const float CardTop = 48f;
        private const float CardPaddingX = 22f;
        private const float CardPaddingTop = 20f;
        private const float CardPaddingBottom = 18f;
        private const float TitlePx = 34f;
        private const float TitleGap = 16f;
        private const float RowGap = 4f;
        private const float RowPaddingX = 14f;
        private const float FooterPx = 12f;
        private const float FooterGap = 10f;

        /// <summary>Tallest row the reference draws; a card full of rows shrinks them instead.</summary>
        private const float RowMaxPx = 40f;
        private const float RowMinPx = 24f;

        // ------------------------------------------------------------------ drawing order

        // Alpha-blended surfaces are sorted by distance unless they say otherwise, and here they all
        // stand within four millimetres of one another. The numbers are explicit for that reason.
        private const int MenuScrimOrder = 40;
        private const int MenuCardOrder = 50;
        private const int MenuRowOrder = 60;
        private const int MenuTextOrder = 100;

        /// <summary>World height of a glyph per one unit of <see cref="TextMesh.characterSize"/>.</summary>
        private const float GlyphPerSize = 5f;

        private Transform cameraTransform = null!;
        private TextMesh title = null!;
        private TextMesh footer = null!;
        private UiPlate backdrop = null!;
        private UiPlate scrim = null!;

        private readonly List<UiPlate> rowFaces = new();
        private readonly List<TextMesh> rowLabels = new();
        private readonly List<TextMesh> rowValues = new();
        private readonly List<BoxCollider> rowColliders = new();
        private GameMenu? lastMenu;
        private int paintedRevision = -1;
        private float units = 0.000425f;

        public bool IsOpen { get; private set; }

        /// <summary>How many rows the menu has; the rows are fixed, their texts are not.</summary>
        public int RowCount => rowLabels.Count;

        public TextMesh Title => title;
        public TextMesh Footer => footer;

        /// <summary>The card itself; the tests measure it against the frame.</summary>
        public Renderer Backdrop => backdrop.Renderer;

        /// <summary>The card as a plate, so a test can measure its real width and height.</summary>
        public UiPlate Card => backdrop;

        public static MenuPanel Create(Transform cameraTransform)
        {
            var root = new GameObject("MenuPanel");
            root.transform.SetParent(cameraTransform, false);
            root.transform.localPosition = new Vector3(0f, 0f, PanelZ);
            root.transform.localRotation = Quaternion.identity;

            var panel = root.AddComponent<MenuPanel>();
            panel.cameraTransform = cameraTransform;

            // The table stays visible behind the card, dimmed the way the reference dims it.
            panel.scrim = UiPlate.Create(root.transform, UiSkin.Scrim, "scrim");
            panel.backdrop = UiPlate.Create(root.transform, UiSkin.Panel, "backdrop");

            // Every surface here is drawn with an alpha blend, so Unity would order them by their
            // distance from the camera and two plates a millimetre apart would swap places. The
            // order is therefore said out loud, from the dimmer up to the captions.
            SetOrder(panel.scrim.Renderer, MenuScrimOrder);
            SetOrder(panel.backdrop.Renderer, MenuCardOrder);
            panel.title = CreateText(root.transform, "title", TitlePx, TextAlignment.Center, UiRole.Accent);
            panel.footer = CreateText(root.transform, "footer", FooterPx, TextAlignment.Center, UiRole.TextMuted);

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

            var face = UiPlate.Create(root.transform, UiSkin.Row, "face");
            SetOrder(face.Renderer, MenuRowOrder);

            // Picking goes through the row's own box, exactly like an action button: the box is the
            // whole pitch of the list, so a click in the gap lands on a row instead of on nothing.
            var box = root.AddComponent<BoxCollider>();
            box.center = Vector3.zero;

            var label = CreateText(root.transform, "label", 18f, TextAlignment.Left, UiRole.TextMain);
            var value = CreateText(root.transform, "value", 13f, TextAlignment.Right, UiRole.Accent);

            rowFaces.Add(face);
            rowLabels.Add(label);
            rowValues.Add(value);
            rowColliders.Add(box);
        }

        private static TextMesh CreateText(Transform parent, string name, float px, TextAlignment alignment,
            UiRole role)
        {
            var host = new GameObject(name);
            host.transform.SetParent(parent, false);
            host.transform.localRotation = Quaternion.identity;

            var mesh = host.AddComponent<TextMesh>();
            mesh.anchor = alignment == TextAlignment.Center ? TextAnchor.MiddleCenter
                : alignment == TextAlignment.Right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            mesh.alignment = alignment;
            mesh.fontSize = 64;
            mesh.characterSize = px * UiStyle.TextScale;
            mesh.color = UiStyle.Of(role);
            mesh.richText = false;
            UiFont.Assign(mesh);
            // A caption stands on its own plate a millimetre away: without an order of its own the
            // two are sorted by distance and a long row can lose its text.
            SetOrder(mesh.GetComponent<Renderer>(), MenuTextOrder);
            return mesh;
        }

        private static void SetOrder(Renderer? renderer, int order)
        {
            if (renderer != null) renderer.sortingOrder = order;
        }

        /// <summary>Fits the card to the frame and lays the rows out again; called every visible frame.</summary>
        public void Layout()
        {
            var camera = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null;
            var halfHeight = camera != null
                ? Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * PanelZ
                : 0.1529f;
            units = halfHeight * 2f / DesignHeight;
            var halfWidth = halfHeight * (camera != null ? camera.aspect : 16f / 9f);

            // The dimmer covers the whole frame, whatever the card does.
            scrim.SetSize(halfWidth * 2f, halfHeight * 2f);
            scrim.SetPosition(0f, 0f, 0.004f);

            var width = Mathf.Min(CardWidth * units, halfWidth * 1.86f);
            var top = halfHeight - CardTop * units;

            // The card is as tall as its content, but never taller than the frame: a menu with fifteen
            // rows and a menu with three keep the same look, the rows only get shorter.
            var available = top + halfHeight - CardPaddingBottom * units - 12f * units;
            // The chrome is everything on the card that is not a row: the padding above and below the
            // list, the title, the gap under it and the hint line along the bottom edge.
            var chrome = CardPaddingTop * units + TitlePx * units + TitleGap * units
                + FooterGap * units + FooterPx * units + CardPaddingBottom * units + 8f * units;
            var rowsSpace = Mathf.Max(0.02f, available - chrome);
            var count = Mathf.Max(1, rowColliders.Count);
            var step = Mathf.Min(RowMaxPx * units, rowsSpace / count);
            var rowHeight = Mathf.Max(RowMinPx * units, step - RowGap * units);

            var cardHeight = chrome + count * step - RowGap * units;
            var cardY = top - cardHeight * 0.5f;
            // Depth order is explicit and never shared: the dimmer is the farthest thing on the plane,
            // then the card, then the rows, then every caption. Two surfaces on the same depth are
            // sorted by whichever the renderer happens to visit first, which is how a menu once drew
            // its own card over its rows.
            backdrop.SetSize(width, cardHeight);
            backdrop.SetPosition(0f, cardY, 0.002f);

            title.text = Loc.Get("menu.title");
            title.characterSize = TitlePx * units / GlyphPerSize;
            title.transform.localPosition = new Vector3(0f,
                top - CardPaddingTop * units - TitlePx * 0.5f * units, -0.001f);

            footer.text = Loc.Get("menu.hint");
            footer.characterSize = FooterPx * units / GlyphPerSize;
            footer.transform.localPosition = new Vector3(0f,
                cardY - cardHeight * 0.5f + CardPaddingBottom * units + FooterPx * units, -0.001f);

            var rowWidth = width - CardPaddingX * 2f * units;
            var firstY = top - CardPaddingTop * units - (TitlePx + TitleGap) * units - rowHeight * 0.5f;
            var labelPx = Mathf.Clamp(18f, 11f, rowHeight / units * 0.46f);
            var valuePx = Mathf.Clamp(13f, 9f, rowHeight / units * 0.36f);

            for (var index = 0; index < rowColliders.Count; index++)
            {
                var y = firstY - index * step;
                var box = rowColliders[index];
                box.transform.localPosition = new Vector3(0f, y, 0f);
                box.size = new Vector3(rowWidth, Mathf.Max(rowHeight, step), 0.01f);

                // The row's own object carries the height of the row: the face, the caption and the
                // value hang at the same local place inside it, otherwise the row would be drawn one
                // row lower than the box that answers a click.
                rowFaces[index].SetSize(rowWidth, rowHeight);
                rowFaces[index].SetPosition(0f, 0f, 0.001f);

                rowLabels[index].characterSize = labelPx * units / GlyphPerSize;
                rowLabels[index].transform.localPosition =
                    new Vector3(-rowWidth * 0.5f + RowPaddingX * units, 0f, -0.001f);

                rowValues[index].characterSize = valuePx * units / GlyphPerSize;
                rowValues[index].transform.localPosition =
                    new Vector3(rowWidth * 0.5f - RowPaddingX * units, 0f, -0.001f);
            }

            Physics.SyncTransforms();
        }

        private void Update()
        {
            if (!IsOpen) return;
            Layout();
            // The look of the menu can change from the table behind it: one frame later it follows.
            if (paintedRevision != UiStyle.Revision && lastMenu != null) Refresh(lastMenu);
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
            lastMenu = menu;
            paintedRevision = UiStyle.Revision;

            title.text = Loc.Get("menu.title");
            title.color = UiStyle.Of(UiRole.Accent);
            footer.text = Loc.Get("menu.hint");
            footer.color = UiStyle.Of(UiRole.TextMuted);
            backdrop.SetSkin(UiSkin.Panel);
            scrim.SetSkin(UiSkin.Scrim);

            for (var index = 0; index < rowLabels.Count; index++)
            {
                if (index >= menu.RowCount)
                {
                    rowFaces[index].gameObject.SetActive(false);
                    rowLabels[index].text = string.Empty;
                    rowValues[index].text = string.Empty;
                    continue;
                }

                rowFaces[index].gameObject.SetActive(true);
                var selected = index == menu.Index;
                var row = menu.RowAt(index);

                rowLabels[index].text = (selected ? "▸ " : "   ") + row.Label;
                rowLabels[index].color = UiStyle.Of(selected ? UiRole.TextOnAccent : UiRole.TextMain);
                rowValues[index].text = row.Value;
                rowValues[index].color = UiStyle.Of(selected ? UiRole.TextOnAccent : UiRole.Accent);

                // The row under the cursor is the card's one highlighted surface, which is what the
                // reference does with its primary button.
                rowFaces[index].SetSkin(selected ? UiSkin.RowSelected : UiSkin.Row);
            }
        }

        /// <summary>The caption of one row as it is drawn; the tests read it instead of the screen.</summary>
        public string RowText(int index)
        {
            if (index < 0 || index >= rowLabels.Count) return string.Empty;
            var label = rowLabels[index].text;
            var value = rowValues[index].text;
            return value.Length == 0 ? label : label + "   " + value;
        }

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
