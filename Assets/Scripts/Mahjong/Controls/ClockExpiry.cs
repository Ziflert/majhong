using System;
using System.Collections.Generic;
using System.Linq;
using Mahjong.Bots;
using Mahjong.Rules;

namespace Mahjong.Controls
{
    /// <summary>
    /// What the table does when a window of <see cref="PlayClock"/> runs out.
    ///
    /// A claim that nobody spoke is not a claim: rules 3.6.7 and 3.11.6.5 give three seconds for a
    /// pung or kong, so silence turns into a pass. A turn cannot stand still, so a discard that was
    /// not made in time is made for the player: the table takes the tile the player's own bot would
    /// throw. Hu and kongs are not declared for a silent player — a claim is his to speak, and a false
    /// Hu is a penalty (3.11.6.6), so the clock never declares one.
    ///
    /// Lives next to the rules, not in the view, so the behaviour is checked headless.
    /// </summary>
    public static class ClockExpiry
    {
        /// <summary>
        /// The action to play when the window runs out, or null when the engine offers nothing the
        /// clock is allowed to play. <paramref name="autoPlay"/> picks the discard; without a bot the
        /// last tile offered is used, which is still a legal discard.
        /// </summary>
        public static LegalAction? Choose(GameState state, int player, IReadOnlyList<LegalAction> legal, IMahjongBot? autoPlay = null)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (legal == null) throw new ArgumentNullException(nameof(legal));
            if (legal.Count == 0) return null;

            var pass = legal.FirstOrDefault(action => action.Kind == ActionKind.Pass);
            if (pass != null) return pass;

            var discards = legal.Where(action => action.Kind == ActionKind.Discard).ToList();
            if (discards.Count == 0) return null;
            if (autoPlay != null) return autoPlay.Choose(state, player, discards);

            return discards[discards.Count - 1];
        }
    }
}
