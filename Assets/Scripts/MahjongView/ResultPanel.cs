using System.Collections.Generic;
using System.Linq;
using Mahjong.Controls;
using UnityEngine;

namespace Mahjong.Unity
{
    /// <summary>
    /// The end-of-hand screen: who won, what the hand was worth, how it was paid, the points after the
    /// hand and — at the end of a round or of the session — the table of points, with a button that
    /// deals the next hand.
    ///
    /// It replaces the three seconds of HUD text the table used to show before it dealt again (У-7):
    /// the hand now stands still until the player presses the button or the waiting time set in the
    /// menu runs out. The content and the countdown live in <see cref="ResultScreen"/>; this panel only
    /// draws them and turns a click into "next hand".
    /// </summary>
    public sealed class ResultPanel : MonoBehaviour
    {
        /// <summary>Distance in front of the camera; the menu and the rulebook sit on the same plane.</summary>
        private const float PanelZ = 0.5f;

        /// <summary>Largest panel that fits the frame half a metre out (see <see cref="RulesPanel"/>).</summary>
        private const float MaxPanelWidth = 0.56f;
        private const float MaxPanelHeight = 0.28f;
        private const float FrameShare = 0.94f;

        /// <summary>Lines the panel can draw; a classic settlement plus a four-place table fits in eleven.</summary>
        public const int MaxLines = 12;

        private const float LineStep = 0.0115f;
        private const float LineSize = 0.0011f;

        private const float ButtonWidth = 0.20f;
        private const float ButtonHeight = 0.023f;

        /// <summary>From the top edge of the panel to the first line of the hand; the title sits above it.</summary>
        private const float TopPad = 0.042f;

        private const float FooterPad = 0.008f;
        private const float FooterHeight = 0.024f;

        /// <summary>Gap between the button and the footer under it.</summary>
        private const float ButtonGap = 0.008f;

        /// <summary>Gap between the last line of the hand and the button.</summary>
        private const float LineGap = 0.014f;

        /// <summary>From the bottom edge of the panel to the last line: footer, button and both gaps.</summary>
        private const float BottomPad = FooterPad + FooterHeight + ButtonGap + ButtonHeight + LineGap;

        // The colours of the page are roles of the palette in effect (UiStyle): the same sheet, the
        // same title and the same button as every other page, in whichever look the player chose.
        private static Color PanelColour => UiStyle.Of(UiRole.Surface, 0.96f);
        private static Color ButtonColour => UiStyle.Of(UiRole.Primary);
        private static Color TitleColour => UiStyle.Of(UiRole.Accent);
        private static Color LineColour => UiStyle.Of(UiRole.TextMain);
        private static Color FooterColour => UiStyle.Of(UiRole.TextMuted);
        private static Color ButtonCaptionColour => UiStyle.Of(UiRole.TextOnAccent);

        private Transform cameraTransform = null!;
        private TextMesh title = null!;
        private TextMesh footer = null!;
        private Renderer background = null!;
        private Renderer buttonFace = null!;
        private TextMesh buttonCaption = null!;
        private BoxCollider buttonCollider = null!;

        private readonly List<TextMesh> lines = new();
        private int paintedRevision = -1;

        /// <summary>Lines the open screen actually has; the panel is sized to them, not to the frame.</summary>
        private int contentLines;

        public bool IsOpen { get; private set; }

        public TextMesh Title => title;
        public TextMesh Footer => footer;

        /// <summary>The dark page behind the text; the PlayMode test checks that it fits the frame.</summary>
        public Renderer Backdrop => background;

        public TextMesh ButtonCaption => buttonCaption;

        /// <summary>False on the last hand of the session: there is no next hand to offer.</summary>
        public bool ButtonShown => buttonCollider.gameObject.activeSelf;

        /// <summary>How many lines the screen draws; the tests read them instead of the screen.</summary>
        public int LineCount => lines.Count(line => line.text.Length > 0);

        /// <summary>Top of the next-hand button in world space; a column of text must stay above it.</summary>
        public float ButtonTop => buttonCollider.transform.position.y + ButtonHeight * 0.5f;

        public string LineText(int index) =>
            index >= 0 && index < lines.Count ? lines[index].text : string.Empty;

        public float LineHeight(int index) => index >= 0 && index < lines.Count
            ? lines[index].transform.position.y
            : float.NaN;

        public static ResultPanel Create(Transform cameraTransform)
        {
            var root = new GameObject("ResultPanel");
            root.transform.SetParent(cameraTransform, false);
            root.transform.localPosition = new Vector3(0f, 0f, PanelZ);
            root.transform.localRotation = Quaternion.identity;

            var panel = root.AddComponent<ResultPanel>();
            panel.cameraTransform = cameraTransform;
            panel.background = CreateBackground(root.transform);
            panel.title = CreateText(root.transform, "title", 0.0021f, TextAnchor.MiddleCenter, TitleColour);
            for (var index = 0; index < MaxLines; index++)
                panel.lines.Add(CreateText(root.transform, $"line-{index}", LineSize,
                    TextAnchor.MiddleCenter, LineColour));
            panel.footer = CreateText(root.transform, "footer", 0.0011f, TextAnchor.LowerCenter, FooterColour);
            panel.CreateButton(root.transform);

            panel.Layout();
            panel.SetOpen(false);
            return panel;
        }

        private void CreateButton(Transform parent)
        {
            var root = new GameObject("next");
            root.transform.SetParent(parent, false);

            var faceObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            faceObject.name = "face";
            faceObject.transform.SetParent(root.transform, false);
            faceObject.transform.localScale = new Vector3(ButtonWidth, ButtonHeight, ActionButton.Depth);
            // picking goes through the button's own box, exactly like a menu row
            Object.Destroy(faceObject.GetComponent<Collider>());

            buttonCollider = root.AddComponent<BoxCollider>();
            buttonCollider.size = new Vector3(ButtonWidth, ButtonHeight, ActionButton.Depth);

            buttonFace = faceObject.GetComponent<Renderer>();
            buttonFace.material = new Material(Shader.Find("Unlit/Color"));
            buttonFace.material.color = ButtonColour;

            buttonCaption = CreateText(root.transform, "caption", 0.0014f, TextAnchor.MiddleCenter,
                ButtonCaptionColour);
            buttonCaption.transform.localPosition = new Vector3(0f, 0f, -(ActionButton.Depth * 0.5f) - 0.0008f);
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

        private static TextMesh CreateText(Transform parent, string name, float size, TextAnchor anchor,
            Color colour)
        {
            var host = new GameObject(name);
            host.transform.SetParent(parent, false);
            host.transform.localRotation = Quaternion.identity;

            var mesh = host.AddComponent<TextMesh>();
            mesh.anchor = anchor;
            mesh.alignment = anchor == TextAnchor.LowerCenter ? TextAlignment.Center : TextAlignment.Center;
            mesh.fontSize = 64;
            mesh.characterSize = size;
            mesh.color = colour;
            mesh.richText = false;
            UiFont.Assign(mesh);
            return mesh;
        }

        /// <summary>Fits the panel to its content and to the frame, and lays the texts out again.</summary>
        public void Layout()
        {
            var camera = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null;
            var halfHeight = camera != null
                ? Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * PanelZ
                : MaxPanelHeight * 0.5f;
            var halfWidth = camera != null ? halfHeight * camera.aspect : MaxPanelWidth * 0.5f;

            var width = Mathf.Min(MaxPanelWidth, halfWidth * 2f * FrameShare);

            // The panel is as tall as the hand it shows: a four-line result on a full-frame page reads
            // as an empty window, and a player has to hunt for the numbers in the top corner of it.
            var needed = TopPad + BottomPad + Mathf.Max(0, contentLines - 1) * LineStep;
            var height = Mathf.Min(Mathf.Min(MaxPanelHeight, halfHeight * 2f * FrameShare), needed);

            background.transform.localScale = new Vector3(width, height, 1f);
            title.transform.localPosition = new Vector3(0f, height * 0.5f - 0.019f, -0.0009f);

            // The lines run from the top pad down to the button, and the step shrinks if a settled hand
            // with a table of points comes in lower than the room between them.
            var firstLineY = height * 0.5f - TopPad;
            var lastLineY = -height * 0.5f + BottomPad;
            var step = contentLines > 1
                ? Mathf.Min(LineStep, (firstLineY - lastLineY) / (contentLines - 1))
                : LineStep;
            for (var index = 0; index < lines.Count; index++)
                lines[index].transform.localPosition =
                    new Vector3(0f, firstLineY - index * step, -0.0009f);

            var buttonWidth = Mathf.Min(ButtonWidth, width * 0.7f);
            var buttonY = -height * 0.5f + FooterPad + FooterHeight + ButtonGap + ButtonHeight * 0.5f;
            buttonCollider.transform.localPosition = new Vector3(0f, buttonY, 0f);
            buttonCollider.size = new Vector3(buttonWidth, ButtonHeight, ActionButton.Depth);
            buttonFace.transform.localScale = new Vector3(buttonWidth, ButtonHeight, ActionButton.Depth);

            footer.transform.localPosition = new Vector3(0f, -height * 0.5f + FooterPad, -0.0009f);
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

        /// <summary>Draws the model: title, the hand as it was paid, the table of points and the countdown.</summary>
        public void Refresh(ResultScreen screen)
        {
            if (paintedRevision != UiStyle.Revision) Repaint();
            title.text = screen.Title;

            var content = screen.AllLines();
            contentLines = Mathf.Min(content.Count, MaxLines);
            for (var index = 0; index < lines.Count; index++)
                lines[index].text = index < content.Count ? content[index] : string.Empty;

            buttonCollider.gameObject.SetActive(screen.CanContinue);
            buttonCaption.text = screen.Button;
            footer.text = screen.Hint + "\n" + screen.Countdown;

            // The panel is as tall as its content, so a redrawn screen is also a resized one.
            Layout();
        }

        /// <summary>
        /// Paints the page in the palette that is in effect: the sheet, the title, the lines of the hand,
        /// the button and the footer. The texts stay as they are — only their colour is the look.
        /// </summary>
        private void Repaint()
        {
            paintedRevision = UiStyle.Revision;
            background.material.color = PanelColour;
            title.color = TitleColour;
            footer.color = FooterColour;
            buttonFace.material.color = ButtonColour;
            buttonCaption.color = ButtonCaptionColour;
            foreach (var line in lines) line.color = LineColour;
        }

        /// <summary>Turns a click into "deal the next hand"; false when the ray missed the button.</summary>
        public bool TryPick(Ray ray)
        {
            if (!IsOpen || !ButtonShown) return false;

            foreach (var hit in Physics.RaycastAll(ray, 5f))
                if (hit.collider == buttonCollider) return true;

            return false;
        }
    }
}
