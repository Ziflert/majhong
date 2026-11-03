using Mahjong.Rules;
using UnityEngine;

namespace Mahjong.Unity
{
    /// <summary>
    /// A single 3D tile built from primitives: a cube body plus a label on the top face.
    /// No prefabs and no art assets are needed for the prototype.
    ///
    /// The tile is an empty root with two children on purpose: the body cube carries a non-uniform
    /// scale, so parenting the label or the tile itself to a scaled object would squash the text
    /// and the geometry. The root keeps scale 1 and owns the collider used for mouse picking.
    /// </summary>
    public sealed class TileObject : MonoBehaviour
    {
        // Sized for a 1.35-wide table: a tile takes about 1/19 of it, close to a real set.
        public const float Length = 0.072f;
        public const float Width = 0.052f;
        public const float Height = 0.028f;

        /// <summary>How fast a tile slides to its new place, in table units per second.</summary>
        public const float SlideSpeed = 1.8f;

        /// <summary>How fast a tile turns to its new orientation, in degrees per second.</summary>
        public const float TurnSpeed = 720f;

        private TextMesh label = null!;
        private Renderer body = null!;

        /// <summary>A fresh tile shows neither face nor back until one of them is bound.</summary>
        private bool faceSet;

        public TileType Type { get; private set; }
        public bool IsFaceUp { get; private set; }

        /// <summary>Where the tile is heading; the engine lays out the table and the tiles follow.</summary>
        public Vector3 TargetPosition { get; private set; }

        public float TargetRotationY { get; private set; }

        /// <summary>True once the tile has arrived: the position is reached and the turn is done.</summary>
        public bool IsSettled =>
            (transform.position - TargetPosition).sqrMagnitude < 1e-8f
            && Mathf.Abs(Mathf.DeltaAngle(transform.rotation.eulerAngles.y, TargetRotationY)) < 0.5f;

        public static TileObject Create(Transform parent, string name)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);

            var bodyObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bodyObject.name = "body";
            bodyObject.transform.SetParent(root.transform, false);
            bodyObject.transform.localScale = new Vector3(Width, Height, Length);
            // picking is done through the root collider, which has the same box
            Object.Destroy(bodyObject.GetComponent<Collider>());

            var box = root.AddComponent<BoxCollider>();
            box.size = new Vector3(Width, Height, Length);

            var tile = root.AddComponent<TileObject>();
            tile.body = bodyObject.GetComponent<Renderer>();
            tile.label = CreateLabel(root.transform);
            return tile;
        }

        private static TextMesh CreateLabel(Transform parent)
        {
            var labelObject = new GameObject("label");
            labelObject.transform.SetParent(parent, false);
            // sit just above the top face; the root is unscaled, so these are world units
            labelObject.transform.localPosition = new Vector3(0f, Height * 0.5f + 0.0015f, 0f);
            // Lay the label flat and face up, with the text top pointing away from the player seat.
            // A +90 degree turn about X maps the text normal to world up (+Y) and the text up
            // direction to +Z, so the glyphs read upright from the camera in front of the table.
            // LookRotation(up, forward) looks right on paper but shows the back of the text: the
            // screenshot of the prototype had doubled, upside-down captions.
            labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var mesh = labelObject.AddComponent<TextMesh>();
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 64;
            // TextMesh world height follows characterSize; 0.005 is about a third of the tile width
            mesh.characterSize = 0.005f;
            mesh.text = string.Empty;
            return mesh;
        }

        public void Bind(TileType type)
        {
            Type = type;
            IsFaceUp = true;
            faceSet = true;
            label.text = TileLabel.Short(type);
            label.color = TileLabel.Text(type);
            body.material.color = TileLabel.Face(type);
            label.gameObject.SetActive(true);
        }

        public void BindBack()
        {
            IsFaceUp = false;
            faceSet = true;
            label.text = string.Empty;
            body.material.color = new Color(0.25f, 0.45f, 0.3f);
            label.gameObject.SetActive(false);
        }

        /// <summary>Shows a face, doing nothing when the tile already shows it.</summary>
        public void SetFace(TileType type)
        {
            if (faceSet && IsFaceUp && Type == type) return;
            Bind(type);
        }

        /// <summary>Turns the tile face down, doing nothing when it already is.</summary>
        public void SetBack()
        {
            if (faceSet && !IsFaceUp) return;
            BindBack();
        }

        public void SetHighlight(bool highlighted)
        {
            var colour = TileLabel.Face(Type);
            body.material.color = highlighted ? Color.Lerp(colour, Color.yellow, 0.45f) : colour;
        }

        /// <summary>Persistent victory colour used while the hand-result screen is shown.</summary>
        public void SetVictoryHighlight()
        {
            body.material.color = Color.Lerp(TileLabel.Face(Type), Color.yellow, 0.7f);
        }

        /// <summary>Moves the tile to a place at once; used at the deal and by tests.</summary>
        public void SetPosition(Vector3 position, float rotationY)
        {
            SetTarget(position, rotationY);
            Snap();
        }

        /// <summary>Sets where the tile has to go; the next frames slide and turn it there.</summary>
        public void SetTarget(Vector3 position, float rotationY)
        {
            TargetPosition = position;
            TargetRotationY = rotationY;
        }

        /// <summary>Puts the tile at its target without animating.</summary>
        public void Snap()
        {
            transform.position = TargetPosition;
            transform.rotation = Quaternion.Euler(0f, TargetRotationY, 0f);
        }

        public void SpawnAt(Vector3 position)
        {
            transform.position = position;
        }

        /// <summary>
        /// The whole animation of the table: every tile glides towards the place the engine gave it.
        /// Tiles keep their identity between rebuilds, so a discard travels from the hand to the pile
        /// instead of jumping there as a new object.
        /// </summary>
        private void Update()
        {
            if (IsSettled) return;

            transform.position = Vector3.MoveTowards(transform.position, TargetPosition, SlideSpeed * Time.deltaTime);
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.Euler(0f, TargetRotationY, 0f), TurnSpeed * Time.deltaTime);
        }

        public void SetFaceDownRotation()
        {
            transform.rotation = Quaternion.Euler(0f, 0f, 180f);
        }
    }
}
