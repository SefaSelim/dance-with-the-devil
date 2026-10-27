using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Fades the scenery that stands between the camera and Defne (MAIN_GDD §9: what hides the player turns see-through).
    /// Each frame three points on Defne (feet, chest, head) cast a ray toward the camera along its view direction (the
    /// camera is orthographic, so every ray is parallel); a tall map renderer whose bounds (grown by
    /// <see cref="_margin"/>) the rays cross eases to <see cref="_fadedAlpha"/> over <see cref="_fadeTime"/>, and back
    /// when it no longer covers her. A fading renderer swaps to transparent copies of its materials, built from the
    /// Resources/OcclusionFade template (URP Lit, transparent, so the variant is in builds) with the source's base map
    /// and colour; at full opacity it gets its own materials back. Static scenery only, collected once when the scene
    /// starts: characters, enemies, projectiles, effects, the floor and the far background are left out.
    /// Added by <see cref="PixelCameraRig"/> at runtime; no scene edit needed.
    /// </summary>
    [DefaultExecutionOrder(1010)] // after the camera rig has placed the camera for this frame
    [DisallowMultipleComponent]
    public sealed class OcclusionFader : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly float[] SampleHeights = { 0.35f, 1.0f, 1.6f };

        [Tooltip("Opacity of scenery that hides Defne.")]
        [SerializeField, Range(0f, 1f)] private float _fadedAlpha = 0.12f;
        [Tooltip("Seconds to fade out or back in.")]
        [SerializeField, Min(0.01f)] private float _fadeTime = 0.15f;
        [Tooltip("Metres the occluders' bounds are grown by (Defne's width round the rays).")]
        [SerializeField, Min(0f)] private float _margin = 0.35f;
        [Tooltip("Only renderers at least this tall (metres) can fade: low tombs and props never hide her.")]
        [SerializeField, Min(0f)] private float _minHeight = 1.4f;
        [Tooltip("Renderers wider than this (metres) are the far background or the island itself: never faded.")]
        [SerializeField, Min(1f)] private float _maxSize = 40f;

        private sealed class Entry
        {
            public Renderer Renderer;
            public Bounds Bounds;
            public Material[] Original;
            public Material[] Faded;
            public Color[] Colors;
            public float Alpha = 1f;
            public bool Swapped;
        }

        private readonly List<Entry> _entries = new List<Entry>(512);
        private readonly List<Entry> _active = new List<Entry>(32);
        private readonly Dictionary<Material, Material> _fadedCopies = new Dictionary<Material, Material>();
        private MaterialPropertyBlock _block;
        private Material _template;
        private Camera _camera;
        private Transform _target;
        private bool _collected;

        /// <summary>Sets the camera and the character to keep visible.</summary>
        public void Configure(Camera gameCamera, Transform target)
        {
            _camera = gameCamera;
            _target = target;
        }

        private void Start()
        {
            _block = new MaterialPropertyBlock();
            _template = Resources.Load<Material>("OcclusionFade");
            Collect();
        }

        private void OnDestroy()
        {
            foreach (Entry e in _entries) Restore(e);
            foreach (Material m in _fadedCopies.Values)
                if (m != null) Destroy(m);
            _fadedCopies.Clear();
        }

        private void Collect()
        {
            _collected = true;
            foreach (MeshRenderer r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || r.sharedMaterials.Length == 0) continue;
                if (r.GetComponentInParent<PlayerController>() != null || r.GetComponentInParent<Enemy>() != null
                    || r.GetComponentInParent<Projectile>() != null || r.GetComponentInParent<Canvas>() != null) continue;
                Bounds b = r.bounds;
                if (b.size.y < _minHeight || b.size.x > _maxSize || b.size.z > _maxSize) continue;
                if (!AllFadeable(r.sharedMaterials)) continue;
                _entries.Add(new Entry { Renderer = r, Bounds = b, Original = r.sharedMaterials });
            }
        }

        private static bool AllFadeable(Material[] materials)
        {
            foreach (Material m in materials)
                if (m == null || !m.HasProperty(BaseColorId) || m.shader.name.Contains("Particles")) return false;
            return true;
        }

        private void LateUpdate()
        {
            if (!_collected || _camera == null || _target == null) return;
            Vector3 back = -_camera.transform.forward;
            float dt = Time.unscaledDeltaTime;
            float step = dt / _fadeTime;

            for (int i = 0; i < _entries.Count; i++)
            {
                Entry e = _entries[i];
                if (e.Renderer == null) continue;
                bool covers = Covers(e.Bounds, back);
                float goal = covers ? _fadedAlpha : 1f;
                if (Mathf.Approximately(e.Alpha, goal) && (covers || !e.Swapped)) continue;
                e.Alpha = Mathf.MoveTowards(e.Alpha, goal, step);
                Apply(e);
            }
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

        private void Apply(Entry e)
        {
            if (e.Alpha >= 0.999f)
            {
                Restore(e);
                return;
            }
            if (!e.Swapped)
            {
                if (e.Faded == null) BuildFaded(e);
                e.Renderer.sharedMaterials = e.Faded;
                e.Swapped = true;
            }
            for (int m = 0; m < e.Faded.Length; m++)
            {
                Color c = e.Colors[m];
                c.a *= e.Alpha;
                _block.Clear();
                _block.SetColor(BaseColorId, c);
                e.Renderer.SetPropertyBlock(_block, m);
            }
        }

        private void Restore(Entry e)
        {
            e.Alpha = 1f;
            if (!e.Swapped || e.Renderer == null) return;
            e.Renderer.sharedMaterials = e.Original;
            for (int m = 0; m < e.Original.Length; m++) e.Renderer.SetPropertyBlock(null, m);
            e.Swapped = false;
        }

        private void BuildFaded(Entry e)
        {
            e.Faded = new Material[e.Original.Length];
            e.Colors = new Color[e.Original.Length];
            for (int m = 0; m < e.Original.Length; m++)
            {
                Material source = e.Original[m];
                e.Colors[m] = source.GetColor(BaseColorId);
                if (!_fadedCopies.TryGetValue(source, out Material copy))
                {
                    copy = _template != null ? new Material(_template) : MakeTransparentLit();
                    copy.name = source.name + " (faded)";
                    Texture map = source.HasProperty(BaseMapId) ? source.GetTexture(BaseMapId) : source.mainTexture;
                    copy.SetTexture(BaseMapId, map);
                    if (source.HasProperty(BaseMapId))
                    {
                        copy.SetTextureScale(BaseMapId, source.GetTextureScale(BaseMapId));
                        copy.SetTextureOffset(BaseMapId, source.GetTextureOffset(BaseMapId));
                    }
                    copy.SetColor(BaseColorId, e.Colors[m]);
                    _fadedCopies[source] = copy;
                }
                e.Faded[m] = copy;
            }
        }

        /// <summary>Fallback when the Resources template is missing (the editor): URP Lit set to alpha blending.</summary>
        private static Material MakeTransparentLit()
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }
    }
}
