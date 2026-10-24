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
    /// World-side readability (audit F14): the core is drawn 1.8x and pulses, the contact ring has a bright edge in
    /// the module's colour, a duplicate shows a flat cream X across its ring (and a smaller, still core), and the core
    /// blinks in its last 3 seconds before it fades. Visual scale never changes the contact radius.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ModuleOffer : MonoBehaviour
    {
        private const float CoreHeight = 1.1f;
        private const float CoreScale = 1.8f;            // the 1x core read as a 3-6 px speck
        private const float DuplicateCoreScale = 1.2f;   // a duplicate ("TAKILI") sits smaller and still
        private const float BobAmplitude = 0.1f;
        private const float BobFrequency = 1.2f;
        private const float SpinDegreesPerSecond = 90f;
        private const float PulseFrequency = 1.6f;
        private const float PulseAmount = 0.14f;
        private const float ExpiryWarning = 3f;          // seconds left when the core starts to blink
        private const float ExpiryBlinkHz = 5f;
        private const int EdgeSegments = 16;
        private static readonly Color MarkerColor = new Color(0.96f, 0.9f, 0.74f); // cream, like the bounce edge

        private Transform _core;
        private readonly Transform[] _shapes = new Transform[ModuleCatalog.Count];
        private Transform _ringBase;
        private Transform _ringFill;
        private MeshRenderer _ringFillRenderer;
        private Transform _edge;
        private readonly MeshRenderer[] _edgeRenderers = new MeshRenderer[EdgeSegments];
        private GameObject _duplicateMarker;
        private Material _markerMaterial;
        private bool _coreVisible = true;
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

            // Haste: a double chevron ">>" (speed), thin bars.
            Transform haste = NewShape(ModuleType.Haste);
            m = Mat(ModuleType.Haste);
            for (int c = 0; c < 2; c++)
            {
                float tip = 0.2f - c * 0.24f;
                Transform upper = ModuleVisuals.CreatePart("Chevron" + c + "_Up", PrimitiveType.Cube, haste,
                    new Vector3(tip - 0.09f, 0.09f, 0f), new Vector3(0.28f, 0.08f, 0.08f), m);
                upper.localRotation = Quaternion.Euler(0f, 0f, -45f);
                Transform lower = ModuleVisuals.CreatePart("Chevron" + c + "_Down", PrimitiveType.Cube, haste,
                    new Vector3(tip - 0.09f, -0.09f, 0f), new Vector3(0.28f, 0.08f, 0.08f), m);
                lower.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }

            Material ringBase = materials != null && materials.Length > ModuleCatalog.Count ? materials[ModuleCatalog.Count] : null;
            _ringBase = ModuleVisuals.CreatePart("RingBase", PrimitiveType.Cylinder, transform, new Vector3(0f, 0.02f, 0f),
                new Vector3(_ringDiameter, 0.01f, _ringDiameter), ringBase);
            _ringFill = ModuleVisuals.CreatePart("RingFill", PrimitiveType.Cylinder, transform, new Vector3(0f, 0.035f, 0f),
                Vector3.zero, null);
            _ringFillRenderer = _ringFill.GetComponent<MeshRenderer>();
            _ringBase.GetComponent<MeshRenderer>().receiveShadows = false;
            _ringFillRenderer.receiveShadows = false;

            // Bright edge of the contact ring in the module's colour (the dark base alone vanished on the dark floor).
            _edge = new GameObject("RingEdge").transform;
            _edge.SetParent(transform, false);
            float edgeRadius = _ringDiameter * 0.5f;
            float edgeLength = 2f * Mathf.PI * edgeRadius / EdgeSegments * 0.6f;
            for (int i = 0; i < EdgeSegments; i++)
            {
                float a = (i + 0.5f) * 2f * Mathf.PI / EdgeSegments;
                var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Transform segment = ModuleVisuals.CreatePart("Edge_" + i, PrimitiveType.Cube, _edge,
                    radial * edgeRadius + Vector3.up * 0.05f, new Vector3(edgeLength, 0.08f, 0.12f), null);
                segment.localRotation = Quaternion.LookRotation(radial);
                _edgeRenderers[i] = segment.GetComponent<MeshRenderer>();
                _edgeRenderers[i].receiveShadows = false;
            }

            // "TAKILI" in the world (GDD §7): a flat cream X across the ring of a type the player already has. The
            // bars run along the world axes, which the 45° camera shows as an X.
            _markerMaterial = CreateMarkerMaterial(materials);
            _duplicateMarker = new GameObject("DuplicateMarker");
            _duplicateMarker.transform.SetParent(transform, false);
            float barLength = _ringDiameter * 1.2f;
            Transform barA = ModuleVisuals.CreatePart("Bar_A", PrimitiveType.Cube, _duplicateMarker.transform,
                Vector3.up * 0.06f, new Vector3(barLength, 0.06f, 0.16f), _markerMaterial);
            Transform barB = ModuleVisuals.CreatePart("Bar_B", PrimitiveType.Cube, _duplicateMarker.transform,
                Vector3.up * 0.065f, new Vector3(0.16f, 0.06f, barLength), _markerMaterial);
            barA.GetComponent<MeshRenderer>().receiveShadows = false;
            barB.GetComponent<MeshRenderer>().receiveShadows = false;
            _duplicateMarker.SetActive(false);
        }

        private static Material CreateMarkerMaterial(Material[] materials)
        {
            Material source = materials != null && materials.Length > 0 ? materials[0] : null;
            if (source == null) return null;
            var material = new Material(source) { name = "mat_module_duplicate_marker" };
            if (material.HasProperty(ModuleVisuals.BaseColorId)) material.SetColor(ModuleVisuals.BaseColorId, MarkerColor);
            return material;
        }

        private void OnDestroy()
        {
            if (_markerMaterial != null) Destroy(_markerMaterial);
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
            Material typeMaterial = Mat(type);
            if (_ringFillRenderer != null) _ringFillRenderer.sharedMaterial = typeMaterial;
            for (int i = 0; i < _edgeRenderers.Length; i++)
                if (_edgeRenderers[i] != null && typeMaterial != null) _edgeRenderers[i].sharedMaterial = typeMaterial;
            if (_duplicateMarker != null) _duplicateMarker.SetActive(false);
            SetCoreVisible(true);

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
            bool duplicate = IsEquippedDuplicate;
            float beat = duplicate ? 0f : Mathf.Sin(_age * PulseFrequency * 2f * Mathf.PI);
            beat *= beat; // 0..1, two soft beats per cycle
            if (_core != null)
            {
                _core.localPosition = Vector3.up * (CoreHeight + Mathf.Sin(_age * BobFrequency * 2f * Mathf.PI) * BobAmplitude);
                _core.localRotation = Quaternion.Euler(0f, _age * SpinDegreesPerSecond * (duplicate ? 0.3f : 1f), 0f);
                float scale = (duplicate ? DuplicateCoreScale : CoreScale) * (1f + PulseAmount * beat);
                _core.localScale = new Vector3(scale, scale, scale);
                // Expiry warning: the core blinks in its last seconds (TimeLeft had no visual).
                SetCoreVisible(_timeLeft > ExpiryWarning || Mathf.Repeat(_timeLeft * ExpiryBlinkHz, 1f) < 0.6f);
            }
            if (_edge != null)
            {
                float pulse = 1f - 0.08f * beat; // breathes inward, never past the real contact edge
                _edge.localScale = new Vector3(pulse, 1f, pulse);
            }
            if (_duplicateMarker != null && _duplicateMarker.activeSelf != duplicate) _duplicateMarker.SetActive(duplicate);
            if (_ringFill != null)
            {
                float d = _ringDiameter * _progress;
                _ringFill.localScale = new Vector3(d, 0.01f, d);
            }
        }

        private void SetCoreVisible(bool visible)
        {
            if (_core == null || _coreVisible == visible) return;
            _coreVisible = visible;
            _core.gameObject.SetActive(visible);
        }
    }
}
