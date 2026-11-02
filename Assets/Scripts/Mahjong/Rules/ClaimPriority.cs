using System.Collections.Generic;
using System.Linq;

namespace Mahjong.Rules
{
    /// <summary>
    /// Which claim wins an open window. Separate from <see cref="GameState"/> because it depends on
    /// nothing but the responses: the self-test feeds synthetic claims instead of hunting for a deal
    /// where two players happen to hold the same pair.
    ///
    /// Rules 3.6.7 and 3.7.2.4: Hu beats every meld, a kong beats a pung, a pung beats a chow, and
    /// equal claims go to the seat nearest the discarder, counting against the direction of play.
    /// </summary>
    public static class ClaimPriority
    {
        /// <summary>Returns the winning claim, or null when every response was a pass.</summary>
        public static LegalAction? Winner(IEnumerable<LegalAction> claims, int discarder)
        {
            return claims
                .Where(claim => claim.Kind != ActionKind.Pass)
                .OrderBy(claim => KindRank(claim.Kind))
                .ThenBy(claim => Distance(claim.PlayerIndex, discarder))
                .FirstOrDefault();
        }

        /// <summary>Plays goes counter-clockwise, so seat 1 after the discarder is one step away.</summary>
        public static int Distance(int player, int discarder) => ((player - discarder) % 4 + 4) % 4;

        /// <summary>Lower wins.</summary>
        public static int KindRank(ActionKind kind) => kind switch
        {
            ActionKind.Win => 0,
            ActionKind.OpenKong => 1,
            ActionKind.Pung => 2,
            ActionKind.Chow => 3,
            ActionKind.Pass => 4,
            _ => 5
        };
    }
}
