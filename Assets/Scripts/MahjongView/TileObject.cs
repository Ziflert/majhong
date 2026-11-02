using Mahjong.Rules;
using UnityEngine;

namespace Mahjong.Unity
{
    /// <summary>
    /// A single 3D tile built from primitives: a cube body plus a label on the face.
    /// No prefabs and no art assets are needed for the prototype.
    /// </summary>
    public sealed class TileObject : MonoBehaviour
    {
        public const float Length = 0.05f;
        public const float Width = 0.038f;
        public const float Height = 0.022f;

        private TextMesh label = null!;
        private Renderer body = null!;

        public TileType Type { get; private set; }
        public bool IsFaceUp { get; private set; }

        public static TileObject Create(Transform parent, string name)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = name;
            root.transform.SetParent(parent, false);
            root.transform.localScale = new Vector3(Width, Height, Length);

            var tile = root.AddComponent<TileObject>();
            tile.body = root.GetComponent<Renderer>();
            tile.label = CreateLabel(root.transform);
            return tile;
        }

        private static TextMesh CreateLabel(Transform parent)
        {
            var labelObject = new GameObject("label");
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            labelObject.transform.localScale = Vector3.one;

            var mesh = labelObject.AddComponent<TextMesh>();
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 64;
            mesh.characterSize = 0.06f;
            mesh.text = string.Empty;
            return mesh;
        }

        public void Bind(TileType type)
        {
            Type = type;
            IsFaceUp = true;
            label.text = TileLabel.Short(type);
            label.color = TileLabel.Text(type);
            body.material.color = TileLabel.Face(type);
            label.gameObject.SetActive(true);
        }

        public void BindBack()
        {
            IsFaceUp = false;
            label.text = string.Empty;
            body.material.color = new Color(0.25f, 0.45f, 0.3f);
            label.gameObject.SetActive(false);
        }

        public void SetHighlight(bool highlighted)
        {
            var colour = TileLabel.Face(Type);
            body.material.color = highlighted ? Color.Lerp(colour, Color.yellow, 0.45f) : colour;
        }

        public void SetPosition(Vector3 position, float rotationY)
        {
            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
        }

        public void SetFaceDownRotation()
        {
            transform.rotation = Quaternion.Euler(0f, 0f, 180f);
        }
    }
}
