using System;
using System.Collections.Generic;
using Mahjong.Controls;
using Mahjong.Localization;
using Mahjong.Rules;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UiText = UnityEngine.UI.Text;
using RulesText = Mahjong.Localization.Text;

namespace Mahjong.Unity
{
    /// <summary>
    /// New screen-space game HUD. It deliberately has no relationship to the 3D table transform:
    /// camera orbit and zoom must never move a screen control. The class only presents state and sends
    /// commands through callbacks supplied by MahjongGame.
    /// </summary>
    public sealed class CanvasGameHud : MonoBehaviour
    {
        private const float ReferenceWidth = 1280f;
        private const float ReferenceHeight = 720f;
        private readonly List<UiText> dynamicText = new();
        private readonly List<Button> actionButtons = new();
        private UiText handText = null!;
        private UiText rulesText = null!;
        private UiText wallText = null!;
        private UiText clockText = null!;
        private UiText[] seatText = new UiText[4];
        private UiText hintText = null!;
        private RectTransform actionRoot = null!;
        private RectTransform handRoot = null!;
        private Action? pause;
        private Action? rules;
        private Action<int>? action;
        private Font font = null!;
        private GameState? lastState;
        private string lastPrompt = string.Empty;

        public static CanvasGameHud Create(MahjongGame owner)
        {
            var canvasObject = new GameObject("NewCanvasGameHud");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            if (EventSystem.current == null)
            {
                var events = new GameObject("EventSystem");
                events.AddComponent<EventSystem>();
                events.AddComponent<StandaloneInputModule>();
            }
            var hud = canvasObject.AddComponent<CanvasGameHud>();
            hud.pause = owner.OpenMenu;
            hud.rules = () => owner.OpenRules();
            hud.action = owner.ExecutePromptOption;
            hud.Build();
            return hud;
        }

        private void Build()
        {
            font = UiFont.Get();
            var root = (RectTransform)transform;
            MakePanel(root, "TopBar", new Vector2(24, -16), new Vector2(-24, -60), UiRole.Surface, 0.94f);
            handText = MakeLabel("Hand", root, new Vector2(40, -38), new Vector2(280, -38), 19, TextAnchor.MiddleLeft);
            rulesText = MakeLabel("Rules", root, new Vector2(330, -38), new Vector2(560, -38), 17, TextAnchor.MiddleLeft, UiRole.TextMuted);
            wallText = MakeLabel("Wall", root, new Vector2(555, -38), new Vector2(725, -38), 19, TextAnchor.MiddleCenter, UiRole.Accent);
            clockText = MakeLabel("Clock", root, new Vector2(-260, -38), new Vector2(-40, -38), 19, TextAnchor.MiddleRight);

            seatText[0] = MakePlate("SeatNorth", root, new Vector2(540, -76), new Vector2(760, -130), TextAnchor.MiddleCenter);
            seatText[1] = MakePlate("SeatWest", root, new Vector2(24, -292), new Vector2(244, -346), TextAnchor.MiddleLeft);
            seatText[2] = MakePlate("SeatSouth", root, new Vector2(24, -570), new Vector2(274, -630), TextAnchor.MiddleLeft);
            seatText[3] = MakePlate("SeatEast", root, new Vector2(-244, -292), new Vector2(-24, -346), TextAnchor.MiddleRight);

            MakePanel(root, "Hint", new Vector2(390, -594), new Vector2(-390, -638), UiRole.SurfaceHigh, 0.98f);
            hintText = MakeLabel("HintText", root, new Vector2(410, -594), new Vector2(-410, -638), 19, TextAnchor.MiddleCenter, UiRole.Accent);
            actionRoot = (RectTransform)MakePanel(root, "Actions", new Vector2(250, -648), new Vector2(-250, -704), UiRole.Surface, 0.98f).transform;
            handRoot = (RectTransform)MakePanel(root, "HandSafeZone", new Vector2(300, -650), new Vector2(-300, -720), UiRole.Background, 0f).transform;
            handRoot.SetAsFirstSibling();
            MakeButton(root, "Pause", "Ⅱ  Esc", new Vector2(-116, -16), new Vector2(-24, -60), pause, UiRole.Primary);
            MakeButton(root, "RulesButton", "R  " + Loc.Get("hud.btn.rules"), new Vector2(24, -648), new Vector2(154, -704), rules, UiRole.SurfaceHigh);
        }

        private void LateUpdate()
        {
            var game = FindAnyObjectByType<MahjongGame>();
            if (game == null || game.Session?.State == null) return;
            Refresh(game.Session, game.Settings, game.HumanSeat, game.IsYourTurn, game.HandClock, game.Prompt);
        }

        public void Refresh(GameSession session, GameSettings settings, int humanSeat, bool humanTurn,
            PlayClock clock, ActionPrompt prompt)
        {
            var state = session.State;
            if (state == null) return;
            handText.text = Loc.Format("hud.handRound", session.HandsPlayed + 1, GameSession.TotalHands,
                RulesText.SeatName(session.RoundWind));
            rulesText.text = RulesText.VariantName(session.Variant);
            wallText.text = Loc.Format("hud.wall", state.Wall.Remaining);
            clockText.text = session.HasTimeLimit
                ? $"{Clock(session.Elapsed)} / {Clock(session.TimeLimit)}"
                : Clock(session.Elapsed);

            for (var seat = 0; seat < 4; seat++)
            {
                var view = (seat - humanSeat + 4) % 4;
                var prefix = seat == humanSeat ? Loc.Get("hud.you") : RulesText.SeatName(state.SeatWindOf(seat));
                var active = seat == state.CurrentPlayer ? "  ◉" : string.Empty;
                seatText[view].text = $"{prefix}{active}\n{Loc.Format("hud.playerScore", session.Scores[seat])}";
                seatText[view].color = seat == state.CurrentPlayer ? UiStyle.Of(UiRole.Accent) : UiStyle.Of(UiRole.TextMain);
            }

            hintText.text = humanTurn
                ? Loc.Get("hud.yourTurn")
                : Loc.Format("hud.turnName", RulesText.SeatName(state.SeatWindOf(state.CurrentPlayer)));
            if (clock.IsRunning)
                hintText.text += clock.IsLimited ? $"  ·  {clock.SecondsLeft}s" : "  ·  ∞";

            if (prompt.Fingerprint != lastPrompt || !ReferenceEquals(lastState, state))
            {
                lastPrompt = prompt.Fingerprint;
                lastState = state;
                RebuildActions(prompt);
            }
        }

        private void RebuildActions(ActionPrompt prompt)
        {
            foreach (var button in actionButtons) Destroy(button.gameObject);
            actionButtons.Clear();
            if (!prompt.IsOpen) { actionRoot.gameObject.SetActive(false); return; }
            actionRoot.gameObject.SetActive(true);
            var width = 150f;
            for (var i = 0; i < prompt.Options.Count; i++)
            {
                var option = prompt.Options[i];
                var button = MakeButton(actionRoot, "Action" + option.Number,
                    $"{option.Number}  {option.Label}", new Vector2(12 + i * (width + 8), -6),
                    new Vector2(12 + i * (width + 8) + width, -50), () => action?.Invoke(option.Number), UiRole.Primary);
                actionButtons.Add(button);
            }
        }

        private UiText MakePlate(string name, Transform parent, Vector2 min, Vector2 max, TextAnchor anchor)
        {
            MakePanel(parent, name + "Panel", min, max, UiRole.Surface, 0.96f);
            return MakeLabel(name, parent, min + new Vector2(12, -2), max - new Vector2(12, 2), 16, anchor);
        }

        private UiText MakeLabel(string name, Transform parent, Vector2 min, Vector2 max, int size,
            TextAnchor anchor, UiRole role = UiRole.TextMain)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            ApplyRect(rect, min, max);
            var text = go.AddComponent<UiText>();
            text.font = font;
            text.fontSize = size;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.color = UiStyle.Of(role);
            dynamicText.Add(text);
            return text;
        }

        private static GameObject MakePanel(Transform parent, string name, Vector2 min, Vector2 max,
            UiRole role, float alpha)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            ApplyRect(rect, min, max);
            var image = go.AddComponent<Image>();
            image.color = UiStyle.Of(role, alpha);
            return go;
        }

        private Button MakeButton(Transform parent, string name, string caption, Vector2 min, Vector2 max,
            Action? callback, UiRole role)
        {
            var go = MakePanel(parent, name, min, max, role, 0.98f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = go.GetComponent<Image>();
            if (callback != null) button.onClick.AddListener(() => callback());
            var label = MakeLabel(name + "Label", go.transform, new Vector2(8, -4), new Vector2(-8, -max.y + min.y - 4),
                16, TextAnchor.MiddleCenter);
            label.text = caption;
            return button;
        }

        /// <summary>
        /// Converts the design sheet's top-origin rectangles to Unity's bottom-origin RectTransform
        /// offsets. The previous prototype assigned top coordinates directly to offsetMin/Max, which
        /// produced zero-height labels and inverted/negative panels at runtime.
        /// </summary>
        private static void ApplyRect(RectTransform rect, Vector2 first, Vector2 second)
        {
            var stretchX = first.x >= 0f && second.x <= 0f;
            if (stretchX)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.offsetMin = new Vector2(first.x, 0f);
                rect.offsetMax = new Vector2(second.x, 0f);
            }
            else if (first.x >= 0f)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.offsetMin = new Vector2(first.x, 0f);
                rect.offsetMax = new Vector2(second.x, 0f);
            }
            else
            {
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.offsetMin = new Vector2(first.x, 0f);
                rect.offsetMax = new Vector2(second.x, 0f);
            }

            var top = Mathf.Max(first.y, second.y);
            var bottom = Mathf.Min(first.y, second.y);
            if (Mathf.Abs(top - bottom) < 0.01f)
            {
                top += 14f;
                bottom -= 14f;
            }
            rect.offsetMin = new Vector2(rect.offsetMin.x, bottom);
            rect.offsetMax = new Vector2(rect.offsetMax.x, top);
        }

        private static string Clock(TimeSpan time) => $"{(int)time.TotalMinutes}:{time.Seconds:00}";
    }
}
