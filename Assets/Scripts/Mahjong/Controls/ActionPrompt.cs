using System;
using System.Collections.Generic;
using System.Linq;
using Mahjong.Localization;
using Mahjong.Rules;

namespace Mahjong.Controls
{
    /// <summary>One button of the action bar: a legal action with a short label and a hotkey number.</summary>
    public sealed record ActionOption(int Number, LegalAction Action, string Label)
    {
        /// <summary>
        /// Full sentence for the hint line. In English this is the engine's own wording; in another
        /// language <see cref="Text.ActionHint"/> builds the sentence from the same structured action.
        /// </summary>
        public string Hint => Text.ActionHint(Action);

        public override string ToString() => $"{Number}. {Label}";
    }

    /// <summary>
    /// The choices a human player has to pick from, prepared for a bar of buttons.
    ///
    /// Only decisions belong here. <see cref="ActionKind.Discard"/> is done by clicking a tile and
    /// <see cref="ActionKind.Draw"/> happens automatically, so both are dropped. A window where the
    /// only option is <see cref="ActionKind.Pass"/> is not a decision either: the rules let the claim
    /// window close by itself when nobody can claim anything, so such a prompt is closed.
    ///
    /// Knows nothing about Unity: the order, labels and hotkeys are checked by the headless self-test
    /// and by the editor tests.
    /// </summary>
    public sealed class ActionPrompt
    {
        /// <summary>Hotkeys are the number keys 1..9, so no more options than that are offered.</summary>
        public const int MaxOptions = 9;

        /// <summary>A prompt with nothing to decide.</summary>
        public static ActionPrompt Closed { get; } = new(Array.Empty<ActionOption>());

        private ActionPrompt(IReadOnlyList<ActionOption> options)
        {
            Options = options;
            Fingerprint = string.Join("|", options.Select(option =>
                $"{option.Number}:{option.Action.Kind}:{option.Action.Tile?.InstanceId}:"
                + string.Join(",", option.Action.RequiredTiles.Select(tile => tile.InstanceId))));
        }

        /// <summary>Actions in the order they are shown, numbered from one.</summary>
        public IReadOnlyList<ActionOption> Options { get; }

        /// <summary>Identity of this option set; a UI rebuilds only when it changes.</summary>
        public string Fingerprint { get; }

        public bool IsOpen => Options.Count > 0;
        public int Count => Options.Count;

        /// <summary>True when at least one option does something; a bare Pass is not a decision.</summary>
        public bool RequiresChoice => Options.Any(option => option.Action.Kind != ActionKind.Pass);

        /// <summary>Builds the bar contents for the actions the engine offers to one player.</summary>
        public static ActionPrompt FromLegalActions(IEnumerable<LegalAction> legal)
        {
            if (legal == null) throw new ArgumentNullException(nameof(legal));

            var options = legal
                .Where(action => action.Kind is not (ActionKind.Discard or ActionKind.Draw))
                .OrderBy(action => Priority(action.Kind))
                .ThenBy(ChowKey)
                .Take(MaxOptions)
                .Select((action, index) => new ActionOption(index + 1, action, Label(action)))
                .ToList();

            if (options.Count == 0 || options.All(option => option.Action.Kind == ActionKind.Pass))
                return Closed;

            return new ActionPrompt(options);
        }

        /// <summary>
        /// Claim priorities of rules 3.6.7 and 3.7.2.4, expressed as button order:
        /// Hu first, then kongs, pungs, chows and finally the pass.
        /// </summary>
        public static int Priority(ActionKind kind) => kind switch
        {
            ActionKind.Win => 0,
            ActionKind.OpenKong or ActionKind.ClosedKong or ActionKind.AddedKong => 1,
            ActionKind.Pung => 2,
            ActionKind.Chow => 3,
            ActionKind.Pass => 4,
            _ => 5
        };

        /// <summary>
        /// Short caption that fits on a button; the full sentence goes to the hint line. The caption
        /// follows the interface language, so the bar reads Russian under a Russian HUD.
        /// </summary>
        public static string Label(LegalAction action) => Text.ActionLabel(action);

        /// <summary>Finds an option by its hotkey number.</summary>
        public ActionOption? Find(int number) => Options.FirstOrDefault(option => option.Number == number);

        /// <summary>Finds the first option of a kind, if the engine offers one.</summary>
        public ActionOption? Find(ActionKind kind) => Options.FirstOrDefault(option => option.Action.Kind == kind);

        public bool TrySelect(int number, out LegalAction action)
        {
            var option = Find(number);
            action = option?.Action!;
            return option != null;
        }

        public bool TrySelectKind(ActionKind kind, out LegalAction action)
        {
            var option = Find(kind);
            action = option?.Action!;
            return option != null;
        }

        public override string ToString() => IsOpen ? string.Join(" ", Options) : "closed";

        /// <summary>Chows are ordered by their lowest rank so the same hand always looks the same.</summary>
        private static int ChowKey(LegalAction action)
        {
            if (action.Kind != ActionKind.Chow) return 0;
            var ranks = action.RequiredTiles.Select(tile => tile.Type.Rank).ToList();
            if (action.Tile.HasValue) ranks.Add(action.Tile.Value.Type.Rank);
            return ranks.Count == 0 ? 0 : ranks.Min();
        }
    }
}
