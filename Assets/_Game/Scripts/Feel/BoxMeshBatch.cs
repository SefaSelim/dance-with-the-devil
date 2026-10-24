using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// One runtime mesh of up to <see cref="Capacity"/> boxes, rewritten without allocations whenever it changes:
    /// many chunky cubes (blast rings, the reject wedge) for one renderer and one draw call. Boxes are added in world
    /// space between <see cref="Begin"/> and <see cref="End"/>; the object itself stays at the world origin.
    /// Each box copies the built-in cube's faces, so the winding (and back-face culling) is the cube's own.
    /// </summary>
    public sealed class BoxMeshBatch
    {
        private const int VertsPerBox = 24;
        private const int IndicesPerBox = 36;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private static Vector3[] s_cubeVertices;
        private static int[] s_cubeTriangles;

        private readonly GameObject _gameObject;
        private readonly MeshRenderer _renderer;
        private readonly Mesh _mesh;
        private readonly Vector3[] _vertices;
        private readonly int[] _triangles;
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private int _count;
        private Vector3 _min;
        private Vector3 _max;

        /// <summary>Most boxes one batch can draw.</summary>
        public int Capacity { get; }

        public BoxMeshBatch(string name, Material material, int capacity)
        {
            EnsureCubeTemplate();
            Capacity = Mathf.Max(1, capacity);
            _vertices = new Vector3[Capacity * VertsPerBox];
            _triangles = new int[Capacity * IndicesPerBox];
            for (int b = 0; b < Capacity; b++)
                for (int i = 0; i < IndicesPerBox; i++)
                    _triangles[b * IndicesPerBox + i] = s_cubeTriangles[i] + b * VertsPerBox;

            _mesh = new Mesh { name = name };
            _mesh.MarkDynamic();
            _mesh.SetVertices(_vertices);

            // No parent: the vertices are world positions, so the object must stay at the origin, unscaled.
            _gameObject = new GameObject(name);
            _gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = _gameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = material;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.enabled = false;
        }

        /// <summary>Flat colour of every box (_BaseColor through a property block).</summary>
        public void SetColor(Color color)
        {
            _block.SetColor(BaseColorId, color);
            _renderer.SetPropertyBlock(_block);
        }

        public void SetMaterial(Material material) => _renderer.sharedMaterial = material;

        /// <summary>Hides the batch until the next <see cref="End"/> with boxes.</summary>
        public void Hide()
        {
            _count = 0;
            _renderer.enabled = false;
        }

        public void Begin()
        {
            _count = 0;
            _min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            _max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        }

        /// <summary>
        /// Adds a box centred on <paramref name="center"/>, standing upright, its z axis along the flat
        /// <paramref name="forward"/>. <paramref name="size"/>: x across, y up, z along forward (metres).
        /// </summary>
        public void AddBox(Vector3 center, Vector3 forward, Vector3 size)
        {
            if (_count >= Capacity) return;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-8f) forward = Vector3.forward;
            forward.Normalize();
            // (right, up, forward) is a proper rotation, so the cube's triangle winding is preserved.
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            Vector3 x = right * size.x;
            Vector3 y = Vector3.up * size.y;
            Vector3 z = forward * size.z;
            int offset = _count * VertsPerBox;
            for (int i = 0; i < VertsPerBox; i++)
            {
                Vector3 v = s_cubeVertices[i];
                _vertices[offset + i] = center + x * v.x + y * v.y + z * v.z;
            }

            Vector3 extents = 0.5f * new Vector3(
                Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
            _min = Vector3.Min(_min, center - extents);
            _max = Vector3.Max(_max, center + extents);
            _count++;
        }

        /// <summary>Uploads the boxes added since <see cref="Begin"/> and shows them (hides the batch if none).</summary>
        public void End()
        {
            if (_count == 0)
            {
                _renderer.enabled = false;
                return;
            }

            // The whole vertex array is uploaded (unused boxes are simply not indexed), so the vertex count never
            // drops below what the index buffer references.
            _mesh.SetVertices(_vertices);
            _mesh.SetTriangles(_triangles, 0, _count * IndicesPerBox, 0, false);
            _mesh.bounds = new Bounds((_min + _max) * 0.5f, _max - _min);
            _renderer.enabled = true;
        }

        public void Destroy()
        {
            if (_gameObject != null) Object.Destroy(_gameObject);
            if (_mesh != null) Object.Destroy(_mesh);
        }

        private static void EnsureCubeTemplate()
        {
            if (s_cubeVertices != null) return;
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh cube = temp.GetComponent<MeshFilter>().sharedMesh;
            s_cubeVertices = cube.vertices;
            s_cubeTriangles = cube.triangles;
            Collider collider = temp.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            Object.Destroy(temp);
        }
    }
}
