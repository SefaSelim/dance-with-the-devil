using System.Collections.Generic;
using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Opens a round hole around Defne in the scenery that stands between her and the camera (MAIN_GDD §9): only the
    /// part of a crypt, tree or fence inside a circle round her chest disappears (with a dithered rim), the rest of it
    /// stays as it is. Each frame three points on Defne (feet, chest, head) cast a ray toward the camera along its view
    /// direction (orthographic: parallel rays); a tall map renderer whose bounds (grown by <see cref="_margin"/>) the
    /// rays cross swaps to its "Dance/Lit Occlusion" copies (URP Lit with the cut-out, same look outside the hole) and
    /// back when it no longer covers her. The hole grows from nothing over <see cref="_openTime"/> when something first
    /// covers her and closes again when nothing does. The shader reads the globals set here.
    /// Copies come from Resources/OcclusionMaterials (built in the editor by Dance/Arena/Build Occlusion Materials, so
    /// the shader variants are in builds); a material missing from it gets a runtime copy (fine in the editor).
    /// Static URP Lit scenery only, collected once when the scene starts. Added by <see cref="PixelCameraRig"/>.
    /// </summary>
    [DefaultExecutionOrder(1010)] // after the camera rig has placed the camera for this frame
    [DisallowMultipleComponent]
    public sealed class OcclusionFader : MonoBehaviour
    {
        public const string LitShaderName = "Universal Render Pipeline/Lit";
        public const string OcclusionShaderName = "Dance/Lit Occlusion";
        private static readonly int TargetId = Shader.PropertyToID("_OcclusionTarget");
        private static readonly int CircleId = Shader.PropertyToID("_OcclusionCircle");
        private static readonly float[] SampleHeights = { 0.35f, 1.0f, 1.6f };
        private const float CentreHeight = 1.0f;   // the hole is centred on her chest
        private const float MinHeight = 1.4f;      // shorter scenery never hides her…
        private const float RaisedFrom = 1.0f;     // …unless it hangs above the floor (roof moss, lamps, branches)
        private const float MaxSize = 40f;         // wider than this: the island or the far background

        [Tooltip("Radius of the hole round Defne, metres.")]
        [SerializeField, Min(0.1f)] private float _radius = 3f;
        [Tooltip("Width of the dithered rim inside that radius, metres.")]
        [SerializeField, Min(0.01f)] private float _feather = 0.45f;
        [Tooltip("Seconds for the hole to open or close.")]
        [SerializeField, Min(0.01f)] private float _openTime = 0.15f;
        [Tooltip("Metres the occluders' bounds are grown by (Defne's width round the rays).")]
        [SerializeField, Min(0f)] private float _margin = 0.35f;

        private sealed class Entry
        {
            public Renderer Renderer;
            public Bounds Bounds;
            public Material[] Original;
            public Material[] Cut;
            public bool Swapped;
        }

        private readonly List<Entry> _entries = new List<Entry>(512);
        private readonly Dictionary<Material, Material> _cutCopies = new Dictionary<Material, Material>();
        private readonly List<Material> _runtimeCopies = new List<Material>();
        private Camera _camera;
        private Transform _target;
        private Shader _shader;
        private float _open;
        private bool _collected;

        /// <summary>Sets the camera and the character to keep visible.</summary>
        public void Configure(Camera gameCamera, Transform target)
        {
            _camera = gameCamera;
            _target = target;
        }

        /// <summary>True for scenery this can cut: tall or raised above the floor, not the backdrop, every material URP Lit.</summary>
        public static bool IsCandidate(Renderer r)
        {
            if (!(r is MeshRenderer) || !r.enabled || r.sharedMaterials.Length == 0) return false;
            if (r.GetComponentInParent<PlayerController>() != null || r.GetComponentInParent<Enemy>() != null
                || r.GetComponentInParent<Projectile>() != null || r.GetComponentInParent<Canvas>() != null) return false;
            Bounds b = r.bounds;
            if ((b.size.y < MinHeight && b.min.y < RaisedFrom) || b.size.x > MaxSize || b.size.z > MaxSize) return false;
            foreach (Material m in r.sharedMaterials)
                if (m == null || m.shader == null || m.shader.name != LitShaderName) return false;
            return true;
        }

        private void Start()
        {
            _shader = Shader.Find(OcclusionShaderName);
            var set = Resources.Load<OcclusionMaterialSet>("OcclusionMaterials");
            if (set != null)
                for (int i = 0; i < set.Sources.Count && i < set.Copies.Count; i++)
                    if (set.Sources[i] != null && set.Copies[i] != null) _cutCopies[set.Sources[i]] = set.Copies[i];
            foreach (MeshRenderer r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if (IsCandidate(r)) _entries.Add(new Entry { Renderer = r, Bounds = r.bounds, Original = r.sharedMaterials });
            _collected = true;
            Shader.SetGlobalVector(CircleId, Vector4.zero);
        }

        private void OnDestroy()
        {
            foreach (Entry e in _entries) Restore(e);
            foreach (Material m in _runtimeCopies)
                if (m != null) Destroy(m);
            Shader.SetGlobalVector(CircleId, Vector4.zero);
        }

        private void OnDisable()
        {
            foreach (Entry e in _entries) Restore(e);
            _open = 0f;
            Shader.SetGlobalVector(CircleId, Vector4.zero);
        }

        private void LateUpdate()
        {
            if (!_collected || _camera == null || _target == null || _shader == null) return;
            Vector3 back = -_camera.transform.forward;
            bool any = false;
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry e = _entries[i];
                if (e.Renderer == null) continue;
                if (Covers(e.Bounds, back))
                {
                    any = true;
                    if (!e.Swapped) Swap(e);
                }
                else if (e.Swapped && _open > 0f && AnyOtherCovering(i, back))
                {
                    Restore(e); // another piece still covers her: this one goes back at once
                }
            }

            _open = Mathf.MoveTowards(_open, any ? 1f : 0f, Time.unscaledDeltaTime / _openTime);
            if (_open <= 0f)
                for (int i = 0; i < _entries.Count; i++)
                    if (_entries[i].Swapped) Restore(_entries[i]);

            // Radius and rim as fractions of the rendered image's height (orthographic: 2 × size metres tall).
            float metresTall = _camera.orthographic ? _camera.orthographicSize * 2f : 13f;
            float eased = 1f - (1f - _open) * (1f - _open);
            Shader.SetGlobalVector(TargetId, _target.position + Vector3.up * CentreHeight);
            Shader.SetGlobalVector(CircleId, new Vector4(_radius * eased / metresTall, _feather / metresTall, 0f, 0f));
        }

        private bool AnyOtherCovering(int except, Vector3 back)
        {
            for (int i = 0; i < _entries.Count; i++)
                if (i != except && _entries[i].Swapped && _entries[i].Renderer != null && Covers(_entries[i].Bounds, back))
                    return true;
            return false;
        }

        private bool Covers(Bounds bounds, Vector3 back)
        {
            bounds.Expand(_margin * 2f);
            Vector3 feet = _target.position;
            for (int i = 0; i < SampleHeights.Length; i++)
            {
                var ray = new Ray(feet + Vector3.up * SampleHeights[i], back);
                if (bounds.IntersectRay(ray, out float distance) && distance < 60f) return true;
            }
            return false;
        }

        private void Swap(Entry e)
        {
            if (e.Cut == null)
            {
                e.Cut = new Material[e.Original.Length];
                for (int m = 0; m < e.Original.Length; m++) e.Cut[m] = CutCopy(e.Original[m]);
            }
            e.Renderer.sharedMaterials = e.Cut;
            e.Swapped = true;
        }

        private void Restore(Entry e)
        {
            if (!e.Swapped || e.Renderer == null) return;
            e.Renderer.sharedMaterials = e.Original;
            e.Swapped = false;
        }

        private Material CutCopy(Material source)
        {
            if (_cutCopies.TryGetValue(source, out Material copy)) return copy;
            copy = new Material(source) { name = source.name + " (occlusion)", shader = _shader };
            copy.CopyPropertiesFromMaterial(source);
            copy.shaderKeywords = source.shaderKeywords;
            copy.renderQueue = source.renderQueue;
            _cutCopies[source] = copy;
            _runtimeCopies.Add(copy);
            return copy;
        }
    }
}
