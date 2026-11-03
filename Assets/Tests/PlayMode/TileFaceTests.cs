using System.Collections;
using Mahjong.Rules;
using Mahjong.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mahjong.PlayTests
{
    /// <summary>
    /// The tile face is a Quad with a single-sided shader, so its normal has to point up or the
    /// texture is culled and the tiles read as blank white blocks from the seat. It happened once:
    /// the quad was turned -90 about X, which points the normal down. These checks pin the
    /// orientation and the atlas cell, because both are easy to break without seeing it in a test.
    /// </summary>
    public sealed class TileFaceTests
    {
        private Transform host = null!;

        [SetUp]
        public void SetUp() => host = new GameObject("FaceHost").transform;

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.Destroy(host.gameObject);
        }

        private static Transform FaceOf(TileObject tile)
        {
            var face = tile.transform.Find("atlas-face");
            Assert.IsNotNull(face, "A tile has no atlas face child.");
            return face;
        }

        [UnityTest]
        public IEnumerator FaceNormalPointsUp()
        {
            var tile = TileObject.Create(host, "tile");
            tile.Bind(new TileType(TileSuit.Characters, 1));
            yield return null;

            var face = FaceOf(tile);
            var filter = face.GetComponent<MeshFilter>();
            Assert.IsNotNull(filter, "The atlas face has no mesh.");
            Assert.Greater(filter.sharedMesh.normals.Length, 0, "The atlas face mesh has no normals.");

            // The Quad primitive faces along -Z, so the honest check is the mesh normal taken to world
            // space. transform.up would be wrong here: after the 90 degree pitch it lies flat on the table.
            var normal = face.TransformDirection(filter.sharedMesh.normals[0]);
            Assert.Greater(Vector3.Dot(normal, Vector3.up), 0.99f,
                "The atlas face is not lying face up: the single-sided shader would cull it from above.");
            Assert.IsTrue(face.gameObject.activeSelf, "The atlas face was left switched off.");
        }

        [UnityTest]
        public IEnumerator FaceUsesTheAtlasCellOfItsSuitAndRank()
        {
            var tile = TileObject.Create(host, "tile");
            tile.Bind(new TileType(TileSuit.Characters, 3));
            yield return null;

            var renderer = FaceOf(tile).GetComponent<Renderer>();
            Assert.IsNotNull(renderer, "The atlas face has no renderer.");
            var material = renderer.material;
            Assert.IsNotNull(material.mainTexture, "The atlas texture is not bound.");

            // The atlas is 9 columns by 5 rows: characters sit in the bottom row.
            Assert.AreEqual(1f / 9f, material.mainTextureScale.x, 1e-4f);
            Assert.AreEqual(1f / 5f, material.mainTextureScale.y, 1e-4f);
            Assert.AreEqual(2f / 9f, material.mainTextureOffset.x, 1e-4f, "The rank is not mapped to its column.");
            Assert.AreEqual(0f, material.mainTextureOffset.y, 1e-4f, "The characters row is not the bottom row.");
        }

        [UnityTest]
        public IEnumerator FaceDownTileHidesTheAtlasFace()
        {
            var tile = TileObject.Create(host, "tile");
            tile.Bind(new TileType(TileSuit.Wind, (int)Wind.East));
            yield return null;
            var face = FaceOf(tile);
            Assert.IsTrue(face.gameObject.activeSelf);

            tile.BindBack();
            yield return null;
            Assert.IsFalse(face.gameObject.activeSelf, "A face-down tile still shows its face texture.");
        }
    }
}
