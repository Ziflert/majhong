using System.Collections.Generic;
using Mahjong.Controls;
using Mahjong.Localization;
using Mahjong.Rules;
using UnityEngine;

namespace Mahjong.Unity
{
    /// <summary>
    /// The bar of legal human actions: one button per <see cref="ActionOption"/>, a hint line with the
    /// engine's own description of the action under the cursor, and mouse picking through a ray.
    ///
    /// The bar is a child of the camera and floats in front of it, so it stays inside the frame at any
    /// aspect ratio. It is rebuilt only when the prompt's fingerprint changes, which lets the game
    /// call <see cref="Show"/> every frame.
    /// </summary>
    public sealed class ActionBar : MonoBehaviour
    {
        private const float BarY = -0.128f;
        private const float BarZ = 0.5f;
        private const float MaxWidth = 0.40f;
        private const float MaxSlot = 0.085f;
        private const float ButtonHeight = 0.032f;
        private const float Gap = 0.008f;
        private const float HintY = 0.075f;

        private readonly List<ActionButton> buttons = new();
        private TextMesh hint = null!;

        /// <summary>Fingerprint of the option set on screen; empty when the bar is hidden.</summary>
        public string Fingerprint { get; private set; } = string.Empty;

        /// <summary>Language the captions were built in; a switch rebuilds the bar.</summary>
        private Language shownLanguage = Language.En;

        public bool IsOpen => buttons.Count > 0;
        public IReadOnlyList<ActionButton> Buttons => buttons;
        public TextMesh Hint => hint;

        public static ActionBar Create(Transform cameraTransform)
        {
            var root = new GameObject("ActionBar");
            root.transform.SetParent(cameraTransform, false);
            root.transform.localPosition = new Vector3(0f, BarY, BarZ);
            root.transform.localRotation = Quaternion.identity;

            var bar = root.AddComponent<ActionBar>();
            bar.hint = CreateHint(root.transform);
            bar.Hide();
            return bar;
        }

        private static TextMesh CreateHint(Transform parent)
        {
            var hintObject = new GameObject("hint");
            hintObject.transform.SetParent(parent, false);
            // The hint floats above the buttons, over the empty middle of the table: below them it
            // would fall out of the frame, and just under them it lands on the player's hand.
            hintObject.transform.localPosition = new Vector3(0f, HintY, 0f);
            hintObject.transform.localRotation = Quaternion.identity;

            var mesh = hintObject.AddComponent<TextMesh>();
            mesh.anchor = TextAnchor.UpperCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 64;
            // slightly larger than the HUD status line, which is known to be readable on 1080p
            mesh.characterSize = 0.0018f;
            mesh.color = UiStyle.Of(UiRole.TextMain);
            mesh.text = string.Empty;
            UiFont.Assign(mesh);
            return mesh;
        }

        /// <summary>Shows the prompt's buttons. A closed prompt hides the bar.</summary>
        public void Show(ActionPrompt prompt)
        {
            if (!prompt.IsOpen)
            {
                Hide();
                return;
            }

            if (prompt.Fingerprint == Fingerprint && buttons.Count == prompt.Count
                && shownLanguage == Loc.Current) return;

            Clear();
            Fingerprint = prompt.Fingerprint;
            shownLanguage = Loc.Current;

            var slot = Mathf.Min(MaxSlot, MaxWidth / prompt.Count);
            var width = slot - Gap;
            var total = prompt.Count * slot - Gap;
            for (var i = 0; i < prompt.Count; i++)
            {
                var button = ActionButton.Create(transform, prompt.Options[i], width, ButtonHeight);
                button.transform.localPosition = new Vector3(-total * 0.5f + width * 0.5f + i * slot, 0f, 0f);
                buttons.Add(button);
            }

            gameObject.SetActive(true);

            // Colliders are picking targets: sync them right away, otherwise the first click after a
            // rebuild hits wherever the physics scene still has the previous buttons.
            Physics.SyncTransforms();
        }

        public void Hide()
        {
            Clear();
            Fingerprint = string.Empty;
            hint.text = string.Empty;
        }

        /// <summary>Puts the engine's description of an option into the hint line.</summary>
        public void SetHint(string text)
        {
            hint.text = text ?? string.Empty;
        }

        /// <summary>Highlights the button under the cursor, if any, and returns its option.</summary>
        public ActionOption? Hover(Ray ray)
        {
            var hovered = ButtonUnderRay(ray);
            foreach (var button in buttons) button.SetHovered(button == hovered);
            return hovered?.Option;
        }

        /// <summary>Turns a click into a legal action: only the bar is picked, never a tile behind it.</summary>
        public bool TryPick(Ray ray, out LegalAction action)
        {
            var button = ButtonUnderRay(ray);
            action = button?.Action!;
            return button != null;
        }

        private ActionButton? ButtonUnderRay(Ray ray)
        {
            if (buttons.Count == 0) return null;

            // Not a single Physics.Raycast: the table cube and the tiles also have colliders, and any
            // of them may sit between the camera and the bar. The nearest bar button wins.
            var hits = Physics.RaycastAll(ray, BarZ * 2f);
            if (hits.Length == 0) return null;
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            foreach (var hit in hits)
            {
                if (hit.collider == null) continue;
                var button = hit.collider.GetComponentInParent<ActionButton>();
                if (button != null && buttons.Contains(button)) return button;
            }

            return null;
        }

        private void Clear()
        {
            foreach (var button in buttons)
                if (button != null) Destroy(button.gameObject);
            buttons.Clear();
            // A hidden bar has no captions, so the next Show must rebuild them in the new language.
            shownLanguage = Loc.Current;
        }
    }
}
