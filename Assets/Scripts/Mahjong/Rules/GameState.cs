using System;
using System.Collections.Generic;
using System.Linq;

namespace Mahjong.Rules
{
    public enum GamePhase
    {
        NotStarted,
        AwaitingDraw,
        AwaitingDiscard,
        AwaitingClaims,
        AwaitingKongRobbery,
        HandFinished
    }

    public enum ActionKind { Draw, Discard, Chow, Pung, OpenKong, AddedKong, ClosedKong, Win, Pass }

    /// <summary>An action the engine currently allows, with everything the UI needs to describe it.</summary>
    public sealed record LegalAction(
        ActionKind Kind,
        int PlayerIndex,
        Tile? Tile,
        IReadOnlyList<Tile> RequiredTiles,
        string Description)
    {
        public override string ToString() => $"P{PlayerIndex} {Kind}: {Description}";
    }

    public sealed record WinResult(int PlayerIndex, ClaimSource Source, ScoreResult Score, int? DiscarderPlayer = null);

    /// <summary>
    /// Hand state machine: deal, draw, discard, claims, kongs, win and draw.
    /// Contains the rules of one hand and knows nothing about Unity.
    /// </summary>
    public sealed class GameState
    {
        private readonly List<(int Player, Tile Tile)> discards = new();
        private readonly Dictionary<int, LegalAction> claimResponses = new();
        private readonly Dictionary<int, string> winForbidden = new();

        private (int Player, Tile Tile)? pendingDiscard;
        private (int Player, Tile Tile)? robbery;
        private bool claimedThisTurn;

        public Wall Wall { get; private set; } = null!;
        public PlayerHand[] Players { get; } = { new(), new(), new(), new() };
        public IReadOnlyList<(int Player, Tile Tile)> Discards => discards;

        public int Dealer { get; private set; }
        public int CurrentPlayer { get; private set; }
        public GamePhase Phase { get; private set; } = GamePhase.NotStarted;
        public Tile? LastDraw { get; private set; }
        public ClaimSource? LastDrawSource { get; private set; }
        public Wind RoundWind { get; private set; } = Wind.East;
        public WinResult? Result { get; private set; }
        public bool EndedInDraw { get; private set; }

        public DiceRoll DealerRoll { get; private set; }
        public DiceRoll BreakRoll { get; private set; }
        public int DiceThrower { get; private set; }

        public Wind SeatWindOf(int player) => (Wind)(((player - Dealer) % 4 + 4) % 4);
        public IReadOnlyList<LegalAction> CurrentPlayerActions => GetLegalActions(CurrentPlayer);

        // ------------------------------------------------------- penalties

        /// <summary>
        /// Rule 3.11.6.2, 3.11.6.6, 3.11.6.7, 3.11.6.8, 3.11.6.9: some violations cost the player
        /// the right to declare Hu for the rest of the hand. The engine then stops offering the win.
        /// </summary>
        public void ForbidWin(int player, string reason)
        {
            if (player is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(player));
            winForbidden[player] = reason;
        }

        public bool IsWinForbidden(int player) => winForbidden.ContainsKey(player);

        public string? WinForbiddenReason(int player) =>
            winForbidden.TryGetValue(player, out var reason) ? reason : null;

        // ---------------------------------------------------------------- deal

        public void StartHand(int dealer = 0, int? seed = null, Wind roundWind = Wind.East)
        {
            if (dealer is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(dealer));

            Reset();
            Dealer = dealer;
            CurrentPlayer = dealer;
            RoundWind = roundWind;
            var random = seed.HasValue ? new Random(seed.Value) : new Random();
            Wall = Wall.CreateShuffled(random);

            DealerRoll = Dice.Roll(random);
            DiceThrower = (dealer + Dice.ThrowerOffset(DealerRoll.Sum)) % 4;
            BreakRoll = Dice.Roll(random);
            Wall.Break(Dice.BreakStacks(DealerRoll, BreakRoll));

            for (var round = 0; round < 3; round++)
                for (var offset = 0; offset < 4; offset++)
                    for (var i = 0; i < 4; i++)
                        Players[(Dealer + offset) % 4].AddConcealed(Wall.DrawLive());

            for (var offset = 0; offset < 4; offset++)
                Players[(Dealer + offset) % 4].AddConcealed(Wall.DrawLive());

            Players[Dealer].AddConcealed(Wall.DrawLive());

            ResolveFlowersAtDeal();

            CurrentPlayer = Dealer;
            Phase = GamePhase.AwaitingDiscard;
            LastDraw = Players[Dealer].Concealed.Last();
            LastDrawSource = ClaimSource.SelfDraw;
        }

        private void Reset()
        {
            discards.Clear();
            claimResponses.Clear();
            pendingDiscard = null;
            robbery = null;
            claimedThisTurn = false;
            Result = null;
            EndedInDraw = false;
            LastDraw = null;
            LastDrawSource = null;
            winForbidden.Clear();
            for (var i = 0; i < 4; i++) Players[i] = new PlayerHand();
        }

        /// <summary>Rule 3.5.7.6: the dealer replaces flowers first, then South, West and North.</summary>
        private void ResolveFlowersAtDeal()
        {
            for (var offset = 0; offset < 4; offset++)
            {
                CurrentPlayer = (Dealer + offset) % 4;
                var hand = Players[CurrentPlayer];
                foreach (var flower in hand.TakeConcealedFlowers())
                {
                    hand.AddFlower(flower);
                    if (Wall.IsExhausted) return;
                    hand.AddConcealed(ResolveFlowerChain(Wall.DrawReplacement()));
                }
            }
        }

        private Tile ResolveFlowerChain(Tile tile)
        {
            var hand = Players[CurrentPlayer];
            while (tile.IsFlower)
            {
                hand.AddFlower(tile);
                if (Wall.IsExhausted) throw new InvalidOperationException("The wall ran out while replacing flowers.");
                tile = Wall.DrawReplacement();
            }
            return tile;
        }

        // -------------------------------------------------------- legal actions

        public IReadOnlyList<LegalAction> GetLegalActions(int player)
        {
            if (player is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(player));

            return Phase switch
            {
                GamePhase.AwaitingDraw => player == CurrentPlayer
                    ? new[] { new LegalAction(ActionKind.Draw, player, null, Array.Empty<Tile>(), "Draw from the wall") }
                    : Array.Empty<LegalAction>(),
                GamePhase.AwaitingDiscard => GetDiscardPhaseActions(player),
                GamePhase.AwaitingClaims => GetClaimActions(player),
                GamePhase.AwaitingKongRobbery => GetRobberyActions(player),
                _ => Array.Empty<LegalAction>()
            };
        }

        private IReadOnlyList<LegalAction> GetDiscardPhaseActions(int player)
        {
            if (player != CurrentPlayer) return Array.Empty<LegalAction>();
            var hand = Players[player];
            var actions = new List<LegalAction>();

            foreach (var tile in hand.Concealed)
                actions.Add(new LegalAction(ActionKind.Discard, player, tile, Array.Empty<Tile>(), $"Discard {tile.Type}"));

            if (LastDrawSource.HasValue || hand.Concealed.Count == 14 - hand.Melds.Count * 3)
            {
                // A forbidden player keeps drawing and discarding, but the engine stops offering Hu (3.11.6).
                var win = IsWinForbidden(player)
                    ? ScoreResult.Forbidden(WinForbiddenReason(player) ?? "Hu is forbidden in this hand.")
                    : EvaluateWin(player, hand, null, LastDrawSource ?? ClaimSource.SelfDraw, Wall.IsLastLiveTile, false);
                if (win.IsValidWin)
                    actions.Add(new LegalAction(ActionKind.Win, player, null, Array.Empty<Tile>(), $"Declare Hu for {win.TotalPoints} points"));
            }

            if (!claimedThisTurn && !Wall.IsExhausted)
            {
                foreach (var group in hand.Concealed.GroupBy(t => t.Type).Where(g => g.Count() == 4))
                {
                    var tiles = group.ToList();
                    actions.Add(new LegalAction(ActionKind.ClosedKong, player, tiles[0], tiles, $"Concealed kong of {tiles[0].Type}"));
                }

                for (var i = 0; i < hand.Melds.Count; i++)
                {
                    var meld = hand.Melds[i];
                    if (meld.Kind != MeldKind.Pung) continue;
                    var fourth = hand.Concealed.FirstOrDefault(t => t.Type == meld.BaseType);
                    if (fourth.Type != meld.BaseType) continue;
                    actions.Add(new LegalAction(ActionKind.AddedKong, player, fourth, new[] { fourth }, $"Add {fourth.Type} to the pung"));
                }
            }

            return actions;
        }

        private IReadOnlyList<LegalAction> GetClaimActions(int player)
        {
            if (!pendingDiscard.HasValue) return Array.Empty<LegalAction>();
            if (claimResponses.ContainsKey(player)) return Array.Empty<LegalAction>();

            var (discarder, tile) = pendingDiscard.Value;
            if (player == discarder) return Array.Empty<LegalAction>();

            var hand = Players[player];
            var actions = new List<LegalAction>();

            var win = IsWinForbidden(player)
                ? ScoreResult.Forbidden(WinForbiddenReason(player) ?? "Hu is forbidden in this hand.")
                : EvaluateWin(player, hand, tile.Type, ClaimSource.Discard, false, Wall.IsExhausted);
            if (win.IsValidWin)
                actions.Add(new LegalAction(ActionKind.Win, player, tile, Array.Empty<Tile>(), $"Claim Hu on {tile.Type} for {win.TotalPoints} points"));

            var copies = hand.Concealed.Where(t => t.Type == tile.Type).ToList();
            // Rule: once the last tile of the wall has been taken, only Hu may be declared.
            if (!Wall.IsExhausted)
            {
                if (copies.Count >= 3)
                    actions.Add(new LegalAction(ActionKind.OpenKong, player, tile, copies.Take(3).ToList(), $"Open kong of {tile.Type}"));
                else if (copies.Count >= 2)
                    actions.Add(new LegalAction(ActionKind.Pung, player, tile, copies.Take(2).ToList(), $"Pung of {tile.Type}"));
            }

            if (!Wall.IsExhausted && discarder == (player + 3) % 4 && tile.Type.IsNumbered)
                foreach (var chow in FindChows(hand, tile.Type))
                    actions.Add(new LegalAction(ActionKind.Chow, player, tile, chow, $"Chow {tile.Type} with {string.Join(", ", chow.Select(t => t.Type))}"));

            actions.Add(new LegalAction(ActionKind.Pass, player, null, Array.Empty<Tile>(), "Pass"));
            return actions;
        }

        private IReadOnlyList<LegalAction> GetRobberyActions(int player)
        {
            if (!robbery.HasValue) return Array.Empty<LegalAction>();
            if (claimResponses.ContainsKey(player)) return Array.Empty<LegalAction>();
            if (player == robbery.Value.Player) return Array.Empty<LegalAction>();

            var actions = new List<LegalAction>();
            var win = IsWinForbidden(player)
                ? ScoreResult.Forbidden(WinForbiddenReason(player) ?? "Hu is forbidden in this hand.")
                : EvaluateWin(player, Players[player], robbery.Value.Tile.Type, ClaimSource.Discard, false, false, robbedKong: true);
            if (win.IsValidWin)
                actions.Add(new LegalAction(ActionKind.Win, player, robbery.Value.Tile, Array.Empty<Tile>(), $"Robbing the kong for {win.TotalPoints} points"));
            actions.Add(new LegalAction(ActionKind.Pass, player, null, Array.Empty<Tile>(), "Pass"));
            return actions;
        }

        private static IEnumerable<IReadOnlyList<Tile>> FindChows(PlayerHand hand, TileType tile)
        {
            if (!tile.IsNumbered) yield break;
            for (var offset = -2; offset <= 0; offset++)
            {
                var start = tile.Rank + offset;
                if (start < 1 || start > 7) continue;

                var needed = new List<Tile>();
                var complete = true;
                for (var rank = start; rank < start + 3; rank++)
                {
                    if (rank == tile.Rank) continue;
                    var wanted = new TileType(tile.Suit, rank);
                    var match = hand.Concealed.FirstOrDefault(t => t.Type == wanted);
                    if (match.Type != wanted) { complete = false; break; }
                    needed.Add(match);
                }
                if (complete) yield return needed;
            }
        }

        // -------------------------------------------------------------- actions

        public void Apply(LegalAction action)
        {
            var legal = GetLegalActions(action.PlayerIndex);
            var match = legal.FirstOrDefault(a => a.Kind == action.Kind && SameTile(a.Tile, action.Tile));
            if (match == null) throw new InvalidOperationException($"Illegal action: {action}");

            switch (action.Kind)
            {
                case ActionKind.Draw:
                    DrawFromWall();
                    break;
                case ActionKind.Discard:
                    Discard(match.Tile!.Value);
                    break;
                case ActionKind.Win:
                    if (Phase == GamePhase.AwaitingDiscard) DeclareWinFromHand();
                    else { claimResponses[action.PlayerIndex] = match; TryAutoCloseWindow(); }
                    break;
                case ActionKind.ClosedKong:
                    DeclareClosedKong(match);
                    break;
                case ActionKind.AddedKong:
                    DeclareAddedKong(match);
                    break;
                case ActionKind.Pass:
                case ActionKind.Chow:
                case ActionKind.Pung:
                case ActionKind.OpenKong:
                    claimResponses[action.PlayerIndex] = match;
                    TryAutoCloseWindow();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action));
            }
        }

        private static bool SameTile(Tile? left, Tile? right) =>
            left.HasValue == right.HasValue && (!left.HasValue || left.Value.InstanceId == right!.Value.InstanceId);

        private void DrawFromWall()
        {
            var hand = Players[CurrentPlayer];
            var tile = Wall.DrawLive();
            var source = ClaimSource.SelfDraw;
            while (tile.IsFlower)
            {
                hand.AddFlower(tile);
                source = ClaimSource.FlowerReplacement;
                if (Wall.IsExhausted) { FinishDraw(); return; }
                tile = Wall.DrawReplacement();
            }

            hand.AddConcealed(tile);
            LastDraw = tile;
            LastDrawSource = source;
            claimedThisTurn = false;
            Phase = GamePhase.AwaitingDiscard;
        }

        private void Discard(Tile tile)
        {
            var hand = Players[CurrentPlayer];
            if (!hand.RemoveConcealed(tile)) throw new InvalidOperationException("Tile is not in the hand.");

            hand.AddDiscard(tile);
            discards.Add((CurrentPlayer, tile));
            pendingDiscard = (CurrentPlayer, tile);
            claimResponses.Clear();
            claimedThisTurn = false;
            LastDraw = null;
            LastDrawSource = null;

            if (HasAnyClaim())
            {
                Phase = GamePhase.AwaitingClaims;
                return;
            }

            if (Wall.IsExhausted) { FinishDraw(); return; }
            AdvanceTurn();
        }

        private bool HasAnyClaim()
        {
            for (var player = 0; player < 4; player++)
                if (GetClaimActions(player).Any(a => a.Kind != ActionKind.Pass)) return true;
            return false;
        }

        private void AdvanceTurn()
        {
            pendingDiscard = null;
            CurrentPlayer = (CurrentPlayer + 1) % 4;
            if (Wall.IsExhausted) { FinishDraw(); return; }
            Phase = GamePhase.AwaitingDraw;
        }

        /// <summary>Closes the claim window using the priorities of rules 3.6.7 and 3.7.2.4.</summary>
        public void CloseClaimWindow()
        {
            if (Phase != GamePhase.AwaitingClaims || !pendingDiscard.HasValue)
                throw new InvalidOperationException("No claim window is open.");

            var (discarder, tile) = pendingDiscard.Value;
            var claim = ClaimPriority.Winner(claimResponses.Values, discarder);

            if (claim == null) { AdvanceTurn(); return; }
            if (claim.Kind == ActionKind.Win) { ApplyClaimedWin(claim.PlayerIndex, tile, false); return; }

            var hand = Players[claim.PlayerIndex];
            if (!hand.RemoveConcealed(claim.RequiredTiles)) throw new InvalidOperationException("Claim tiles are missing.");

            RemovePendingDiscard();
            var meldTiles = claim.Kind == ActionKind.Chow
                ? claim.RequiredTiles.Append(tile).OrderBy(t => t.Type.Rank).ToList()
                : claim.RequiredTiles.Append(tile).ToList();

            var kind = claim.Kind switch
            {
                ActionKind.Chow => MeldKind.Chow,
                ActionKind.Pung => MeldKind.Pung,
                _ => MeldKind.OpenKong
            };
            hand.AddMeld(new Meld(kind, meldTiles, discarder));

            CurrentPlayer = claim.PlayerIndex;
            pendingDiscard = null;
            claimResponses.Clear();
            claimedThisTurn = true;

            if (kind == MeldKind.OpenKong) { DrawReplacementForCurrentPlayer(); return; }
            Phase = GamePhase.AwaitingDiscard;
        }

        private void RemovePendingDiscard()
        {
            if (!pendingDiscard.HasValue) return;
            var (player, tile) = pendingDiscard.Value;
            Players[player].RemoveLastDiscard(tile);
            var index = discards.FindIndex(d => d.Player == player && d.Tile.InstanceId == tile.InstanceId);
            if (index >= 0) discards.RemoveAt(index);
        }

        private void ApplyClaimedWin(int player, Tile tile, bool robbedKong)
        {
            var hand = Players[player];
            var score = EvaluateWin(player, hand, tile.Type, ClaimSource.Discard, false, !robbedKong && Wall.IsExhausted, robbedKong);
            if (!score.IsValidWin) throw new InvalidOperationException("The claimed hand is not a valid win.");

            if (robbedKong)
            {
                var kongPlayer = robbery!.Value.Player;
                var kongHand = Players[kongPlayer];
                var kongIndex = kongHand.Melds.ToList().FindIndex(m => m.Kind == MeldKind.AddedKong && m.Tiles.Any(t => t.InstanceId == tile.InstanceId));
                if (kongIndex >= 0)
                {
                    var kong = kongHand.Melds[kongIndex];
                    kongHand.ReplaceMeld(kongIndex, new Meld(MeldKind.Pung, kong.Tiles.Where(t => t.InstanceId != tile.InstanceId).ToList(), kong.ClaimedFromPlayer));
                }
            }
            else
            {
                RemovePendingDiscard();
            }

            hand.AddConcealed(tile);
            var discarder = robbedKong ? robbery!.Value.Player : pendingDiscard!.Value.Player;
            Result = new WinResult(player, ClaimSource.Discard, score, discarder);
            Phase = GamePhase.HandFinished;
            pendingDiscard = null;
            robbery = null;
            claimResponses.Clear();
        }

        private void DeclareWinFromHand()
        {
            var hand = Players[CurrentPlayer];
            var source = LastDrawSource ?? ClaimSource.SelfDraw;
            var score = EvaluateWin(CurrentPlayer, hand, null, source, Wall.IsLastLiveTile, false);
            if (!score.IsValidWin) throw new InvalidOperationException("The hand is not a valid win.");
            Result = new WinResult(CurrentPlayer, source, score);
            Phase = GamePhase.HandFinished;
        }

        private void DeclareClosedKong(LegalAction action)
        {
            var hand = Players[CurrentPlayer];
            var tiles = action.RequiredTiles.ToList();
            if (!hand.RemoveConcealed(tiles)) throw new InvalidOperationException("Kong tiles are missing.");
            hand.AddMeld(new Meld(MeldKind.ClosedKong, tiles));
            DrawReplacementForCurrentPlayer();
        }

        private void DeclareAddedKong(LegalAction action)
        {
            var hand = Players[CurrentPlayer];
            var fourth = action.Tile!.Value;
            var index = hand.Melds.ToList().FindIndex(m => m.Kind == MeldKind.Pung && m.BaseType == fourth.Type);
            if (index < 0) throw new InvalidOperationException("No matching pung for the added kong.");
            if (!hand.RemoveConcealed(fourth)) throw new InvalidOperationException("The fourth tile is missing.");

            var existing = hand.Melds[index];
            hand.ReplaceMeld(index, new Meld(MeldKind.AddedKong, existing.Tiles.Append(fourth).ToList(), existing.ClaimedFromPlayer, index));

            robbery = (CurrentPlayer, fourth);
            claimResponses.Clear();

            var canBeRobbed = false;
            for (var player = 0; player < 4 && !canBeRobbed; player++)
                canBeRobbed = GetRobberyActions(player).Any(a => a.Kind == ActionKind.Win);

            if (canBeRobbed) { Phase = GamePhase.AwaitingKongRobbery; return; }
            robbery = null;
            DrawReplacementForCurrentPlayer();
        }

        /// <summary>Closes the robbing-the-kong window.</summary>
        public void CloseKongWindow()
        {
            if (Phase != GamePhase.AwaitingKongRobbery || !robbery.HasValue)
                throw new InvalidOperationException("No kong robbery window is open.");

            var kongPlayer = robbery.Value.Player;
            var robber = claimResponses.Values
                .Where(c => c.Kind == ActionKind.Win)
                .OrderBy(c => (c.PlayerIndex - kongPlayer + 4) % 4)
                .FirstOrDefault();

            if (robber != null) { ApplyClaimedWin(robber.PlayerIndex, robbery.Value.Tile, true); return; }

            robbery = null;
            claimResponses.Clear();
            DrawReplacementForCurrentPlayer();
        }

        private void TryAutoCloseWindow()
        {
            if (Phase == GamePhase.AwaitingClaims)
            {
                var blocked = false;
                for (var player = 0; player < 4 && !blocked; player++)
                {
                    if (player == pendingDiscard!.Value.Player) continue;
                    blocked = GetClaimActions(player).Any(a => a.Kind != ActionKind.Pass);
                }
                if (!blocked) CloseClaimWindow();
            }
            else if (Phase == GamePhase.AwaitingKongRobbery)
            {
                var blocked = false;
                for (var player = 0; player < 4 && !blocked; player++)
                {
                    if (player == robbery!.Value.Player) continue;
                    blocked = GetRobberyActions(player).Any(a => a.Kind == ActionKind.Win);
                }
                if (!blocked) CloseKongWindow();
            }
        }

        private void DrawReplacementForCurrentPlayer()
        {
            var hand = Players[CurrentPlayer];
            var tile = Wall.DrawReplacement();
            while (tile.IsFlower)
            {
                hand.AddFlower(tile);
                if (Wall.IsExhausted) { FinishDraw(); return; }
                tile = Wall.DrawReplacement();
            }

            hand.AddConcealed(tile);
            LastDraw = tile;
            LastDrawSource = ClaimSource.KongReplacement;
            if (Wall.IsExhausted) { FinishDraw(); return; }
            Phase = GamePhase.AwaitingDiscard;
        }

        private void FinishDraw()
        {
            EndedInDraw = true;
            Phase = GamePhase.HandFinished;
        }

        // -------------------------------------------------------------- scoring

        public ScoreResult EvaluateWin(int player, PlayerHand hand, TileType? winningTile, ClaimSource source,
            bool lastTileOfWall, bool lastDiscard, bool robbedKong = false)
        {
            return Scoring.Evaluate(new ScoringContext
            {
                Hand = hand,
                WinningTile = winningTile,
                Source = source,
                RoundWind = RoundWind,
                SeatWind = SeatWindOf(player),
                LastTileOfWall = lastTileOfWall,
                LastDiscard = lastDiscard,
                RobbedKong = robbedKong,
                DiscardPile = discards.Select(d => d.Tile.Type).ToList(),
                OtherMelds = Players.SelectMany(p => p.Melds).ToList()
            });
        }
    }
}
