using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// One floating whisper-module offer (MAIN_GDD §7): a cube placeholder coloured by type, hovering over the floor,
    /// and a flat contact ring under it that fills while the player holds contact. Pure state + visuals: the
    /// approach, lifetime, contact and reject rules are run by <see cref="PlayerModules"/>, which owns the single
    /// reused instance (at most one offer at a time). No colliders: it never blocks bullets or the player.
    /// The UI reads <see cref="Type"/>, <see cref="Position"/>, <see cref="AcceptProgress"/>, <see cref="TimeLeft"/>
    /// and <see cref="IsEquippedDuplicate"/> ("TAKILI").
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ModuleOffer : MonoBehaviour
    {
        private const float CoreHeight = 1f;
        private const float BobAmplitude = 0.08f;
        private const float BobFrequency = 1.2f;
        private const float SpinDegreesPerSecond = 90f;

        private Transform _core;
        private readonly Transform[] _shapes = new Transform[ModuleCatalog.Count];
        private Transform _ringBase;
        private Transform _ringFill;
        private MeshRenderer _ringFillRenderer;
        private Material[] _materials;
        private float _ringDiameter = 1.4f;

        private ModuleType _type;
        private float _lifetime;
        private float _timeLeft;
        private float _progress;
        private float _age;
        private bool _active;

        /// <summary>True while the offer is in the world (not accepted, rejected or expired).</summary>
        public bool IsActive => _active;
        public ModuleType Type => _type;
        /// <summary>Floor point under the offer (world).</summary>
        public Vector3 Position => transform.position;
        /// <summary>World position of the floating core (what the player touches and what reject throws).</summary>
        public Vector3 CorePosition => transform.position + Vector3.up * CoreHeight;
        /// <summary>0..1 contact progress toward acceptance (0.45 s of continuous contact).</summary>
        public float AcceptProgress => _progress;
        /// <summary>Seconds until the offer fades (scaled time).</summary>
        public float TimeLeft => _timeLeft;
        /// <summary>Total lifetime the offer started with (14 s).</summary>
        public float Lifetime => _lifetime;
        /// <summary>True when the player already has this type: the offer shows "TAKILI" and contact does nothing.</summary>
        public bool IsEquippedDuplicate { get; internal set; }
        /// <summary>True until it has come within the stop distance of the player once; then it stays put.</summary>
        public bool IsApproaching { get; internal set; }

        /// <summary>Builds an inactive offer from primitives. <paramref name="materials"/> from ModuleVisuals.CreateMaterials.</summary>
        internal static ModuleOffer Create(Material[] materials, float ringDiameter)
        {
            var root = new GameObject("ModuleOffer");
            ModuleOffer offer = root.AddComponent<ModuleOffer>();
            offer.Build(materials, ringDiameter);
            root.SetActive(false);
            return offer;
        }

        private void Build(Material[] materials, float ringDiameter)
        {
            _materials = materials;
            _ringDiameter = Mathf.Max(0.1f, ringDiameter);

            _core = new GameObject("Core").transform;
            _core.SetParent(transform, false);
            _core.localPosition = Vector3.up * CoreHeight;

            // Triple Shot: a core with three prongs.
            Transform triple = NewShape(ModuleType.TripleShot);
            Material m = Mat(ModuleType.TripleShot);
            ModuleVisuals.CreatePart("Body", PrimitiveType.Cube, triple, Vector3.zero, Vector3.one * 0.28f, m);
            ModuleVisuals.CreatePart("Prong_L", PrimitiveType.Cube, triple, new Vector3(-0.22f, 0.16f, 0f), Vector3.one * 0.13f, m);
            ModuleVisuals.CreatePart("Prong_C", PrimitiveType.Cube, triple, new Vector3(0f, 0.26f, 0f), Vector3.one * 0.13f, m);
            ModuleVisuals.CreatePart("Prong_R", PrimitiveType.Cube, triple, new Vector3(0.22f, 0.16f, 0f), Vector3.one * 0.13f, m);

            // Echo: a small core inside a square ring.
            Transform echo = NewShape(ModuleType.Echo);
            m = Mat(ModuleType.Echo);
            ModuleVisuals.CreatePart("Body", PrimitiveType.Cube, echo, Vector3.zero, Vector3.one * 0.2f, m);
            ModuleVisuals.CreatePart("Ring_N", PrimitiveType.Cube, echo, new Vector3(0f, 0f, 0.3f), new Vector3(0.6f, 0.1f, 0.1f), m);
            ModuleVisuals.CreatePart("Ring_S", PrimitiveType.Cube, echo, new Vector3(0f, 0f, -0.3f), new Vector3(0.6f, 0.1f, 0.1f), m);
            ModuleVisuals.CreatePart("Ring_E", PrimitiveType.Cube, echo, new Vector3(0.3f, 0f, 0f), new Vector3(0.1f, 0.1f, 0.6f), m);
            ModuleVisuals.CreatePart("Ring_W", PrimitiveType.Cube, echo, new Vector3(-0.3f, 0f, 0f), new Vector3(0.1f, 0.1f, 0.6f), m);

            // Density: one big cube.
            Transform density = NewShape(ModuleType.Density);
            ModuleVisuals.CreatePart("Body", PrimitiveType.Cube, density, Vector3.zero, Vector3.one * 0.5f, Mat(ModuleType.Density));

            Material ringBase = materials != null && materials.Length > ModuleCatalog.Count ? materials[ModuleCatalog.Count] : null;
            _ringBase = ModuleVisuals.CreatePart("RingBase", PrimitiveType.Cylinder, transform, new Vector3(0f, 0.02f, 0f),
                new Vector3(_ringDiameter, 0.01f, _ringDiameter), ringBase);
            _ringFill = ModuleVisuals.CreatePart("RingFill", PrimitiveType.Cylinder, transform, new Vector3(0f, 0.035f, 0f),
                Vector3.zero, null);
            _ringFillRenderer = _ringFill.GetComponent<MeshRenderer>();
            _ringBase.GetComponent<MeshRenderer>().receiveShadows = false;
            _ringFillRenderer.receiveShadows = false;
        }

        private Transform NewShape(ModuleType type)
        {
            var shape = new GameObject("Shape_" + type).transform;
            shape.SetParent(_core, false);
            _shapes[(int)type] = shape;
            return shape;
        }

        private Material Mat(ModuleType type) =>
            _materials != null && (int)type < _materials.Length ? _materials[(int)type] : null;

        /// <summary>Places and shows the offer on the floor point <paramref name="floorPosition"/>.</summary>
        internal void Show(ModuleType type, Vector3 floorPosition, float lifetime)
        {
            _type = type;
            _lifetime = Mathf.Max(0.01f, lifetime);
            _timeLeft = _lifetime;
            _progress = 0f;
            _age = 0f;
            _active = true;
            IsEquippedDuplicate = false;
            IsApproaching = true;

            transform.SetPositionAndRotation(floorPosition, Quaternion.identity);
            for (int i = 0; i < _shapes.Length; i++)
                if (_shapes[i] != null) _shapes[i].gameObject.SetActive(i == (int)type);
            if (_ringFillRenderer != null) _ringFillRenderer.sharedMaterial = Mat(type);

            gameObject.SetActive(true);
            UpdateVisual();
        }

        /// <summary>Removes the offer from the world (accepted, rejected or expired).</summary>
        internal void Hide()
        {
            _active = false;
            _progress = 0f;
            gameObject.SetActive(false);
        }

        /// <summary>Counts down the lifetime. Returns true when it has run out.</summary>
        internal bool TickLifetime(float deltaTime)
        {
            _timeLeft = Mathf.Max(0f, _timeLeft - deltaTime);
            _age += deltaTime;
            return _timeLeft <= 0f;
        }

        internal void SetProgress(float progress) => _progress = Mathf.Clamp01(progress);

        /// <summary>Moves the floor point by a flat offset.</summary>
        internal void MoveBy(Vector3 offset)
        {
            offset.y = 0f;
            transform.position += offset;
        }

        private void LateUpdate()
        {
            if (_active) UpdateVisual();
        }

        private void UpdateVisual()
        {
            if (_core != null)
            {
                _core.localPosition = Vector3.up * (CoreHeight + Mathf.Sin(_age * BobFrequency * 2f * Mathf.PI) * BobAmplitude);
                _core.localRotation = Quaternion.Euler(0f, _age * SpinDegreesPerSecond, 0f);
            }
            if (_ringFill != null)
            {
                float d = _ringDiameter * _progress;
                _ringFill.localScale = new Vector3(d, 0.01f, d);
            }
        }
    }
}
