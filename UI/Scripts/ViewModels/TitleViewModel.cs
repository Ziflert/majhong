using System;
using System.Collections.Generic;
using Mahjong.Controls;
using Mahjong.Localization;
using Text = Mahjong.Localization.Text;

namespace Mahjong.UI.ViewModels
{
    /// <summary>
    /// Adapter between the headless <see cref="TitleScreen"/> model and the visual
    /// UI layer. The model already defines rows, cursor, pages and commands; this
    /// ViewModel exposes them as display-ready data without duplicating logic.
    /// </summary>
    public sealed class TitleViewModel
    {
        private readonly TitleScreen _screen;

        public TitleViewModel(TitleScreen screen)
        {
            _screen = screen ?? throw new ArgumentNullException(nameof(screen));
        }

        /// <summary>Underlying model for direct access (tests, engine integration).</summary>
        public TitleScreen Model => _screen;

        /// <summary>Screen title text.</summary>
        public string Title => Loc.Get("title.title");

        /// <summary>Screen subtitle (variant name or page title).</summary>
        public string Subtitle => _screen.Page == TitlePage.Load
            ? Loc.Get("title.loadTitle")
            : Loc.Format("title.subtitle",
                Text.VariantName(_screen.Settings.Variant));

        /// <summary>Status / hint line below rows.</summary>
        public string Status => _screen.Status;

        /// <summary>Footer hint (keyboard shortcuts).</summary>
        public string Footer => _screen.Page == TitlePage.Load
            ? Loc.Get("title.loadHint")
            : Loc.Get("title.hint");

        /// <summary>Total visible row count on the current page.</summary>
        public int RowCount => _screen.RowCount;

        /// <summary>Currently selected row index.</summary>
        public int SelectedIndex => _screen.Index;

        /// <summary>Whether the Continue row can be activated (has a save).</summary>
        public bool CanContinue => _screen.CanContinue;

        /// <summary>One display-ready row.</summary>
        public readonly record struct DisplayRow(
            int Index,
            string Label,
            string Value,
            bool IsSelected,
            bool IsAvailable,
            bool IsSetting);

        /// <summary>All rows for the current page.</summary>
        public IReadOnlyList<DisplayRow> GetRows()
        {
            var rows = new List<DisplayRow>(RowCount);
            for (var i = 0; i < RowCount; i++)
            {
                var model = _screen.RowAt(i);
                rows.Add(new DisplayRow(
                    i,
                    model.Label,
                    model.Value,
                    i == _screen.Index,
                    model.Ready,
                    model.Command == TitleCommand.None));
            }
            return rows;
        }

        /// <summary>Move the cursor. Positive = down.</summary>
        public void Move(int delta) => _screen.Move(delta);

        /// <summary>Step the value of the selected setting row. Returns true if changed.</summary>
        public bool Adjust(int delta) => _screen.Adjust(delta);

        /// <summary>
        /// Activate the selected row. Returns a <see cref="TitleCommand"/> for the caller
        /// (UIManager, MahjongGame) to handle.
        /// </summary>
        public TitleCommand Activate() => _screen.Activate();

        /// <summary>Go back from the load page. Returns true if page changed.</summary>
        public bool Back() => _screen.Back();

        /// <summary>Pending save path (set after Continue or LoadSave).</summary>
        public string PendingSave => _screen.PendingSave;
    }
}
