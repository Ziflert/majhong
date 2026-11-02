using System;
using System.Collections.Generic;
using System.Linq;

namespace Mahjong.Rules
{
    public enum GamePhase { Initializing, AwaitingDraw, AwaitingDiscard, AwaitingClaim, Won, Draw }
    public enum ActionKind { Draw, Discard, Chow, Pung, OpenKong, ClosedKong, DeclareWin, ReplaceFlower }
    public sealed record GameAction(ActionKind Kind, int PlayerIndex, Tile? Tile = null, IReadOnlyList<Tile>? Combination = null);
    public sealed record WinResult(int PlayerIndex, ClaimSource Source, HandAnalysis Hand, int Points, IReadOnlyList<string> Fans);

    public sealed class GameState
    {
        public Wall Wall { get; private set; } = null!;
        public PlayerHand[] Players { get; } = { new(), new(), new(), new() };
        public List<(int Player, Tile Tile)> Discards { get; } = new();
        public int Dealer { get; private set; }
        public int CurrentPlayer { get; private set; }
        public GamePhase Phase { get; private set; }
        public Tile? LastDraw { get; private set; }

        public void StartHand(int dealer = 0, int? seed = null)
        {
            Dealer = dealer; CurrentPlayer = dealer; Wall = Wall.CreateShuffled(new Random(seed));
            foreach (var player in Players)
                while (player.Concealed.Count < 13) player.AddConcealed(Wall.DrawLive());
            Players[Dealer].AddConcealed(Wall.DrawLive());
            Phase = GamePhase.AwaitingDiscard;
            LastDraw = Players[Dealer].Concealed.Last();
        }

        public IReadOnlyList<GameAction> GetLegalActions()
        {
            if (Phase == GamePhase.Draw || Phase == GamePhase.Won) return Array.Empty<GameAction>();
            var actions = new List<GameAction>();
            var hand = Players[CurrentPlayer];
            if (Phase == GamePhase.AwaitingDraw) actions.Add(new(ActionKind.Draw, CurrentPlayer));
            if (Phase == GamePhase.AwaitingDiscard)
            {
                actions.AddRange(hand.Concealed.Select(t => new GameAction(ActionKind.Discard, CurrentPlayer, t)));
                if (HandSolver.Analyse(hand).IsComplete) actions.Add(new(ActionKind.DeclareWin, CurrentPlayer));
            }
            return actions;
        }

        public void Apply(GameAction action)
        {
            if (!GetLegalActions().Any(a => a.Kind == action.Kind && a.PlayerIndex == action.PlayerIndex && (!action.Tile.HasValue || a.Tile == action.Tile)))
                throw new InvalidOperationException($"Illegal action: {action.Kind} by player {action.PlayerIndex}.");
            var hand = Players[action.PlayerIndex];
            switch (action.Kind)
            {
                case ActionKind.Draw:
                    LastDraw = Wall.DrawLive(); hand.AddConcealed(LastDraw.Value); Phase = GamePhase.AwaitingDiscard; break;
                case ActionKind.Discard:
                    if (!hand.RemoveConcealed(action.Tile!.Value)) throw new InvalidOperationException("Tile is not in hand.");
                    hand.AddDiscard(action.Tile.Value); Discards.Add((action.PlayerIndex, action.Tile.Value)); LastDraw = null;
                    CurrentPlayer = (CurrentPlayer + 1) % 4; Phase = Wall.IsExhausted ? GamePhase.Draw : GamePhase.AwaitingDraw; break;
                case ActionKind.DeclareWin:
                    var analysis = HandSolver.Analyse(hand); if (!analysis.IsComplete) throw new InvalidOperationException("Hand is not complete.");
                    Phase = GamePhase.Won; break;
            }
        }
    }
}
