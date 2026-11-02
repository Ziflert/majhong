using System.Linq;
using NUnit.Framework;
using Mahjong.Rules;

public class MahjongRulesTests
{
    [Test]
    public void WallContains144TilesAnd128LiveTiles()
    {
        var wall = Wall.CreateShuffled(new System.Random(1));
        Assert.AreEqual(128, wall.RemainingLive);
        Assert.AreEqual(16, wall.RemainingReplacement);
    }

    [Test]
    public void SevenPairsIsRecognized()
    {
        var hand = new PlayerHand();
        var id = 0;
        foreach (var type in new[] {
            new TileType(TileSuit.Characters, 1), new TileType(TileSuit.Characters, 2),
            new TileType(TileSuit.Characters, 3), new TileType(TileSuit.Dots, 1),
            new TileType(TileSuit.Dots, 2), new TileType(TileSuit.Bamboo, 1),
            new TileType(TileSuit.Wind, 0) })
        { hand.AddConcealed(new Tile(type, id++)); hand.AddConcealed(new Tile(type, id++)); }
        var result = HandSolver.Analyse(hand);
        Assert.IsTrue(result.IsComplete);
        Assert.AreEqual(WinningPattern.SevenPairs, result.Pattern);
    }
}
