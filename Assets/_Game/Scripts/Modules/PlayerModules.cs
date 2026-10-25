using System;
using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// The player's whisper modules (MAIN_GDD §7; first GDD §6-7): the single offer (approach, 14 s lifetime,
    /// 0.45 s intentional contact accept), capacity (<see cref="ModuleCatalog.SlotCount"/> slots, one per type; decision K-23),
    /// right-click reject (60° cone, 3 m), Q selection ring (one position per slot, each showing its module's icon),
    /// Q + right-click eject, the shared 0.65 s cooldown,
    /// and applying/removing every module effect on the Rifle and PlayerController in one place
    /// (<see cref="ApplyEffects"/>). Hız (production/design/combo-haste.md §3), Keskin Göz
    /// (production/design/crit-difficulty.md §3) and Delici (user decision 2026-10-25: +2 pierce, −30 % damage per
    /// enemy passed) are rifle only (K-7); Keskin Göz's cost slows Defne's walk while the
    /// fire button is held (<see cref="UpdateFiringSlow"/>). Offers keep coming while every slot is full (K-23): the
    /// offer is shown blocked ("DOLU") and cannot be accepted until the player ejects a module to free a slot.
    /// Offers are placed by <see cref="ModuleSpawner"/> through <see cref="PresentOffer"/>. Scaled time throughout.
    /// World-side feedback: every reject pulse flashes its cone on the floor (cream on a whiff, the module's colour
    /// when it throws an offer), and Defne's body feedback (<see cref="HurtFeedback"/>) is added here when the scene
    /// has none, so it needs no scene edit.
    /// </summary>
    [DefaultExecutionOrder(5)] // after PlayerController (aim), before WeaponHub
    [RequireComponent(typeof(PlayerController))]
    [DisallowMultipleComponent]
    public sealed class PlayerModules : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Rifle the modules change (decision K-7). If empty, searched in children.")]
        [SerializeField] private Rifle _rifle;
        [Tooltip("Thrower for rejected/ejected modules. If empty, taken from or added to this GameObject.")]
        [SerializeField] private ModuleThrower _thrower;
        [Tooltip("Material template for the runtime module cubes. Empty = the pipeline's default primitive material.")]
        [SerializeField] private Material _moduleMaterial;

        [Header("Offer (GDD §7)")]
        [Tooltip("Offer lifetime in seconds (GDD: 14).")]
        [SerializeField, Min(0.1f)] private float _offerLifetime = 14f;
        [Tooltip("Approach speed in m/s (slow).")]
        [SerializeField, Min(0f)] private float _approachSpeed = 1.5f;
        [Tooltip("The offer stops once this close to the player (flat metres, GDD: ~2) and then stays put.")]
        [SerializeField, Min(0f)] private float _approachStopDistance = 2f;
        [Tooltip("Continuous contact needed to accept, seconds (GDD: 0.45).")]
        [SerializeField, Min(0.01f)] private float _acceptTime = 0.45f;
        [Tooltip("Core contact radius in metres, added to the player's controller radius.")]
        [SerializeField, Min(0f)] private float _coreRadius = 0.35f;

        [Header("Reject / eject (GDD §7)")]
        [Tooltip("Reject cone full angle in degrees (GDD: 60).")]
        [SerializeField, Range(1f, 360f)] private float _rejectConeDegrees = 60f;
        [Tooltip("Reject reach in metres (GDD: 3).")]
        [SerializeField, Min(0f)] private float _rejectRange = 3f;
        [Tooltip("Shared reject/eject cooldown in seconds (GDD: 0.65). A whiffed reject starts it too.")]
        [SerializeField, Min(0f)] private float _cooldown = 0.65f;
        [Tooltip("Colour of the reject cone flash on a whiff (a hit flashes in the thrown module's colour).")]
        [SerializeField] private Color _rejectWhiffColor = new Color(0.96f, 0.9f, 0.74f);

        [Header("Selection ring (first GDD §6)")]
        // Renamed from _slotOffsets (index was ModuleType, six entries) so old scene data is not picked up (K-23).
        [Tooltip("Ring offsets in the camera-flat frame (x = screen right, z = screen up on the floor, y = height), index = slot. Missing entries take the defaults.")]
        [SerializeField] private Vector3[] _ringOffsets = (Vector3[])DefaultRingOffsets.Clone();
        [Tooltip("While the cursor is within this flat distance of the player, the highlight follows it; outside it is kept.")]
        [SerializeField, Min(0.1f)] private float _selectionRadius = 2.2f;
        [Tooltip("A different slot must be this much closer to the cursor to take the highlight (no flicker on borders).")]
        [SerializeField, Min(0f)] private float _selectionHysteresis = 0.15f;
        [Tooltip("Scale of each slot's module icon (the icon is ~0.6 m across at 1, so 0.75 = ~0.45 m).")]
        [SerializeField, Min(0.01f)] private float _slotIconScale = 0.75f;
        [Tooltip("Extra scale of the highlighted slot's icon.")]
        [SerializeField, Min(1f)] private float _highlightScale = 1.6f;

        [Header("Effects (GDD §7)")]
        [Tooltip("Triple Shot side angle in degrees (GDD: ±12).")]
        [SerializeField, Range(0f, 45f)] private float _tripleSpreadDegrees = 12f;
        [Tooltip("Push per salvo opposite the aim, m/s (the player caps the total at 2.5).")]
        [SerializeField, Min(0f)] private float _tripleRecoilPerSalvo = 1.5f;
        [Tooltip("Density rifle damage multiplier (GDD: 1.6).")]
        [SerializeField, Min(0f)] private float _densityDamageMultiplier = 1.6f;
        [Tooltip("Density body and hit radius multiplier (GDD: 1.5).")]
        [SerializeField, Min(0.1f)] private float _densityBodyScale = 1.5f;
        [Tooltip("Hız rifle fire-rate multiplier (design: 1.4, 6 → 8.4 shots/s).")]
        [SerializeField, Min(0.01f)] private float _hasteFireRateMultiplier = 1.4f;
        [Tooltip("Hız cost: each bullet strays randomly up to ± this many degrees (design: 6).")]
        [SerializeField, Range(0f, 45f)] private float _hasteSpreadDegrees = 6f;
        [Tooltip("Keskin Göz rifle crit chance bonus, added to the base (design: 0.2, 5 % → 25 %).")]
        [SerializeField, Range(0f, 1f)] private float _keskinGozCritBonus = 0.2f;
        [Tooltip("Keskin Göz cost: walk speed multiplier while the fire button is held (design: 0.85, 6 → 5.1 m/s). Dodge unaffected.")]
        [SerializeField, Range(0.05f, 1f)] private float _keskinGozFiringSpeedMultiplier = 0.85f;
        [Tooltip("Delici: extra enemies each rifle bullet passes through, added to the contract's (design: 2).")]
        [SerializeField, Min(0)] private int _pierceExtra = 2;
        [Tooltip("Delici cost: fraction of damage a bullet loses per enemy it passes through (design: 0.3 → ×0.7 each).")]
        [SerializeField, Range(0f, 1f)] private float _pierceDamageFalloff = 0.3f;

        /// <summary>Left, up, right around the player (index = slot).</summary>
        private static readonly Vector3[] DefaultRingOffsets =
        {
            new Vector3(-1.2f, 1.2f, 0f),
            new Vector3(0f, 1.2f, 1.2f),
            new Vector3(1.2f, 1.2f, 0f),
        };

        /// <summary>Ring icon spin about world Y, degrees per second (same as the floating offer).</summary>
        private const float SlotSpinDegreesPerSecond = 90f;

        private PlayerController _player;
        private PlayerInputReader _input;
        private Material[] _materials;
        private ModuleOffer _offer;
        private Transform _slotRoot;
        private readonly Transform[] _slotPivots = new Transform[ModuleCatalog.SlotCount];
        private readonly Transform[,] _slotIcons = new Transform[ModuleCatalog.SlotCount, ModuleCatalog.Count];
        private readonly bool[] _equipped = new bool[ModuleCatalog.Count];
        /// <summary>ModuleType index held by each slot, or -1 when empty.</summary>
        private readonly int[] _slots = NewEmptySlots();
        private float _slotSpin;
        private float _readyTime = float.NegativeInfinity;
        private int _smallNoCount;
        private bool _selectionOpen;
        private int _highlighted = -1; // slot index
        private ConeFlash _rejectFlash;
        private bool _firingSlowApplied;

        public event Action<ModuleType> OfferAppeared;
        public event Action<ModuleType> Accepted;
        public event Action<ModuleType> Rejected;
        public event Action<ModuleType> Ejected;
        public event Action<ModuleType> OfferExpired;
        /// <summary>Any change of the equipped set (accept, eject, ClearAll).</summary>
        public event Action EquippedChanged;
        /// <summary>Every reject pulse that was fired (hit or whiff): origin, flat direction. For the cone VFX.</summary>
        public event Action<Vector3, Vector3> RejectPulsed;

        /// <summary>The active offer, or null.</summary>
        public ModuleOffer Current => _offer != null && _offer.IsActive ? _offer : null;
        public bool HasOffer => Current != null;
        /// <summary>Rejects that threw an offer plus ejects (GDD §11 "küçük hayırlar").</summary>
        public int SmallNoCount => _smallNoCount;
        /// <summary>True while Q is held (selection ring shown).</summary>
        public bool SelectionOpen => _selectionOpen;
        /// <summary>The highlighted (filled) slot while the selection is open, else -1.</summary>
        public int HighlightedSlot => _selectionOpen && _highlighted >= 0 && _slots[_highlighted] >= 0 ? _highlighted : -1;
        /// <summary>The module in the highlighted slot while the selection is open, else null.</summary>
        public ModuleType? Highlighted => SlotType(HighlightedSlot);
        public bool RejectReady => Time.time >= _readyTime;
        public float CooldownRemaining => Mathf.Max(0f, _readyTime - Time.time);
        public float Cooldown => _cooldown;
        public int EquippedCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _equipped.Length; i++) if (_equipped[i]) n++;
                return n;
            }
        }
        /// <summary>True when all <see cref="ModuleCatalog.SlotCount"/> slots hold a module.</summary>
        public bool SlotsFull => EquippedCount >= ModuleCatalog.SlotCount;
        public PlayerController Player => _player;
        public Rifle Rifle => _rifle;
        public ModuleThrower Thrower => _thrower;

        /// <summary>The tinted material of a module type (the ring's and the offer's), or null before Awake.</summary>
        internal Material MaterialFor(ModuleType type) =>
            _materials != null && (int)type < _materials.Length ? _materials[(int)type] : null;

        public bool IsEquipped(ModuleType type) => _equipped[(int)type];

        /// <summary>The module in <paramref name="slot"/>, or null when it is empty or out of range.</summary>
        public ModuleType? SlotType(int slot) =>
            slot >= 0 && slot < _slots.Length && _slots[slot] >= 0 ? (ModuleType)_slots[slot] : (ModuleType?)null;

        /// <summary>The slot holding <paramref name="type"/>, or -1 when it is not equipped.</summary>
        public int SlotOf(ModuleType type)
        {
            for (int i = 0; i < _slots.Length; i++) if (_slots[i] == (int)type) return i;
            return -1;
        }

        /// <summary>World position of a selection ring slot (around the player, camera-relative).</summary>
        public Vector3 SlotPosition(int slot)
        {
            Vector3 offset = slot >= 0 && slot < _ringOffsets.Length ? _ringOffsets[slot] : Vector3.up;
            Vector3 right = Vector3.right;
            Vector3 forward = Vector3.forward;
            if (_player != null && _player.CameraRig != null)
            {
                Quaternion view = _player.CameraRig.ViewRotation;
                right = Flat(view * Vector3.right, Vector3.right);
                forward = Flat(view * Vector3.forward, Vector3.forward);
            }
            return transform.position + right * offset.x + Vector3.up * offset.y + forward * offset.z;
        }

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _input = _player.InputReader != null ? _player.InputReader : GetComponent<PlayerInputReader>();
            if (_rifle == null) _rifle = GetComponentInChildren<Rifle>();
            if (_thrower == null) _thrower = GetComponent<ModuleThrower>();
            if (_thrower == null) _thrower = gameObject.AddComponent<ModuleThrower>();
            PadRingOffsets();

            _materials = ModuleVisuals.CreateMaterials(_moduleMaterial);
            BuildSlots();
            ApplyEffects(false);

            // Defne's hurt / dodge feedback (audit F9) without a scene edit.
            if (!TryGetComponent(out HurtFeedback _)) gameObject.AddComponent<HurtFeedback>();
        }

        private void OnDestroy()
        {
            if (_offer != null) Destroy(_offer.gameObject);
            if (_slotRoot != null) Destroy(_slotRoot.gameObject);
            if (_rejectFlash != null) _rejectFlash.Dispose();
            ModuleVisuals.DestroyMaterials(_materials);
        }

        private static int[] NewEmptySlots()
        {
            var slots = new int[ModuleCatalog.SlotCount];
            for (int i = 0; i < slots.Length; i++) slots[i] = -1;
            return slots;
        }

        // Fewer saved offsets than slots: the missing ones take the default position.
        private void PadRingOffsets()
        {
            int have = _ringOffsets != null ? _ringOffsets.Length : 0;
            if (have >= ModuleCatalog.SlotCount) return;
            var offsets = new Vector3[ModuleCatalog.SlotCount];
            for (int i = 0; i < offsets.Length; i++)
                offsets[i] = i < have ? _ringOffsets[i] : DefaultRingOffsets[i];
            _ringOffsets = offsets;
        }

        // Each slot gets a pivot holding all six icons; only the icon of the slot's module is shown.
        private void BuildSlots()
        {
            _slotRoot = new GameObject(name + "_ModuleSlots").transform;
            for (int s = 0; s < ModuleCatalog.SlotCount; s++)
            {
                Transform pivot = new GameObject("Slot_" + s).transform;
                pivot.SetParent(_slotRoot, false);
                for (int t = 0; t < ModuleCatalog.Count; t++)
                {
                    Transform icon = ModuleVisuals.BuildIcon((ModuleType)t, pivot, _materials[t]);
                    icon.gameObject.SetActive(false);
                    _slotIcons[s, t] = icon;
                }
                pivot.gameObject.SetActive(false);
                _slotPivots[s] = pivot;
            }
        }

        // ------------------------------------------------------------------ offers

        /// <summary>
        /// True when a new offer may appear (none active, player alive, some type not yet equipped). Full slots do not
        /// stop offers (K-23): the offer then shows blocked until a module is ejected.
        /// </summary>
        public bool CanPresentOffer =>
            !HasOffer && EquippedCount < ModuleCatalog.Count && (_player == null || !_player.Health.IsDead);

        /// <summary>True while Keskin Göz slows the walk (equipped and fire held).</summary>
        public bool FiringSlowActive => _firingSlowApplied;

        /// <summary>Shows an offer of <paramref name="type"/> on the floor point. Fails if one is already active.</summary>
        public bool PresentOffer(ModuleType type, Vector3 floorPosition)
        {
            if (!CanPresentOffer) return false;
            if (_offer == null) _offer = ModuleOffer.Create(_materials, (ContactRadius) * 2f);
            _offer.Show(type, floorPosition, _offerLifetime);
            RefreshBlocked(_offer);
            OfferAppeared?.Invoke(type);
            return true;
        }

        private float ContactRadius
        {
            get
            {
                CharacterController cc = _player != null ? _player.CharacterController : null;
                float radius = cc != null ? cc.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z) : 0.35f;
                return radius + _coreRadius;
            }
        }

        private void Update()
        {
            if (_player == null) return;
            if (_player.Health.IsDead)
            {
                CloseSelection();
                UpdateFiringSlow();
                return;
            }

            UpdateFiringSlow();
            TickOffer(Time.deltaTime);
            UpdateSelection();

            // Right click during a dodge is dropped, not buffered (GDD §4).
            if (_input != null && _input.RejectPressed && !_player.IsDodging)
            {
                if (_selectionOpen)
                {
                    ModuleType? highlighted = Highlighted;
                    if (highlighted.HasValue) TryEject(highlighted.Value);
                    // An empty slot or a cooling pulse never turns into a normal reject (first GDD §6).
                }
                else
                {
                    TryReject();
                }
            }
        }

        private void TickOffer(float dt)
        {
            ModuleOffer offer = Current;
            if (offer == null) return;

            if (offer.TickLifetime(dt))
            {
                ModuleType expired = offer.Type;
                offer.Hide();
                OfferExpired?.Invoke(expired);
                return;
            }

            Vector3 toPlayer = transform.position - offer.Position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;
            if (offer.IsApproaching)
            {
                // Approaches once, stops at ~2 m and stays; it never chases again (GDD §7).
                if (distance <= _approachStopDistance) offer.IsApproaching = false;
                else offer.MoveBy(toPlayer / distance * Mathf.Min(_approachSpeed * dt, distance - _approachStopDistance));
            }

            // Re-checked every tick: ejecting a module while the offer is up makes it acceptable on the next tick.
            bool blocked = RefreshBlocked(offer);

            Vector3 toCore = transform.position - offer.Position;
            toCore.y = 0f;
            bool contact = toCore.magnitude <= ContactRadius;
            // Leaving, dodging through it, a duplicate or full slots: progress resets (GDD §7 Niyetli kabul, Kapasite).
            if (!contact || blocked || _player.IsDodging)
            {
                offer.SetProgress(0f);
                return;
            }

            offer.SetProgress(offer.AcceptProgress + dt / _acceptTime);
            if (offer.AcceptProgress >= 1f)
            {
                ModuleType type = offer.Type;
                offer.Hide();
                SetEquipped(type, true);
                Accepted?.Invoke(type);
            }
        }

        /// <summary>Updates the offer's duplicate / full-slots flags; returns <see cref="ModuleOffer.IsBlocked"/>.</summary>
        private bool RefreshBlocked(ModuleOffer offer)
        {
            offer.IsEquippedDuplicate = IsEquipped(offer.Type);
            offer.SlotsFull = SlotsFull;
            return offer.IsBlocked;
        }

        // ------------------------------------------------------------------ reject / eject

        /// <summary>
        /// Right-click pulse toward the aim: throws the offer if it lies in the 60° cone within 3 m. Starts the shared
        /// cooldown even on a whiff. Returns true if an offer was thrown.
        /// </summary>
        public bool TryReject()
        {
            if (_player == null || _player.Health.IsDead || _player.IsDodging || !RejectReady) return false;
            _readyTime = Time.time + _cooldown;

            Vector3 aim = _player.AimDirection;
            RejectPulsed?.Invoke(transform.position, aim);

            ModuleOffer offer = Current;
            bool inCone = offer != null && IsInRejectCone(offer, aim);
            PlayRejectFlash(aim, inCone ? ModuleCatalog.ColorOf(offer.Type) : _rejectWhiffColor);
            if (!inCone) return false;

            ModuleType type = offer.Type;
            Vector3 origin = offer.CorePosition;
            offer.Hide();
            _thrower.Throw(type, origin, aim);
            _smallNoCount++;
            Rejected?.Invoke(type);
            return true;
        }

        private bool IsInRejectCone(ModuleOffer offer, Vector3 aim)
        {
            Vector3 toOffer = offer.Position - transform.position;
            toOffer.y = 0f;
            float distance = toOffer.magnitude;
            if (distance > _rejectRange) return false;
            return distance <= 1e-3f || Vector3.Angle(aim, toOffer) <= _rejectConeDegrees * 0.5f;
        }

        // The cone on the floor, so a whiff is visible too (audit F14). Built on first use: the feedback router's
        // unlit material exists only after every Awake.
        private void PlayRejectFlash(Vector3 aim, Color color)
        {
            if (_rejectFlash == null)
            {
                PixelImpactFeedback feedback = PixelImpactFeedback.Instance;
                Material material = feedback != null && feedback.FlashMaterial != null ? feedback.FlashMaterial : _materials[0];
                if (material == null) return;
                _rejectFlash = new ConeFlash(name + "_RejectCone", material);
            }
            _rejectFlash.Play(transform.position, aim, _rejectConeDegrees, _rejectRange, color);
        }

        /// <summary>Ejects an equipped module toward the aim (removes its effects, throws it). Shares the cooldown.</summary>
        public bool TryEject(ModuleType type)
        {
            if (_player == null || _player.Health.IsDead || _player.IsDodging || !RejectReady) return false;
            if (!IsEquipped(type)) return false;
            _readyTime = Time.time + _cooldown;

            Vector3 aim = _player.AimDirection;
            if (_highlighted >= 0 && _highlighted == SlotOf(type)) _highlighted = -1;
            SetEquipped(type, false);
            _thrower.Throw(type, transform.position + Vector3.up + aim * 0.5f, aim);
            _smallNoCount++;
            Ejected?.Invoke(type);
            return true;
        }

        /// <summary>Removes every module and its effects at once, without throwing or counting (death rule ❓ A-11b).</summary>
        public void ClearAll()
        {
            bool any = false;
            for (int i = 0; i < _equipped.Length; i++)
            {
                any |= _equipped[i];
                _equipped[i] = false;
            }
            for (int i = 0; i < _slots.Length; i++) _slots[i] = -1;
            _highlighted = -1;
            if (!any) return;
            ApplyEffects(true);
            EquippedChanged?.Invoke();
        }

        /// <summary>
        /// Equips directly into the first empty slot (debug and tests). Returns false if already equipped or every
        /// slot is full. Raises Accepted.
        /// </summary>
        public bool EquipImmediate(ModuleType type)
        {
            if (IsEquipped(type) || SlotsFull) return false;
            SetEquipped(type, true);
            Accepted?.Invoke(type);
            return true;
        }

        // Equipping takes the first empty slot (callers guarantee one: blocked offers and EquipImmediate stop when
        // full); unequipping empties only its own slot, the others keep their positions.
        private void SetEquipped(ModuleType type, bool value)
        {
            bool hadTriple = _equipped[(int)ModuleType.TripleShot];
            int slot = SlotOf(type);
            if (value && slot < 0)
            {
                slot = Array.IndexOf(_slots, -1);
                if (slot >= 0) _slots[slot] = (int)type;
            }
            else if (!value && slot >= 0)
            {
                _slots[slot] = -1;
            }
            _equipped[(int)type] = value;
            ApplyEffects(hadTriple && !_equipped[(int)ModuleType.TripleShot]);
            EquippedChanged?.Invoke();
        }

        /// <summary>The single place where module effects are applied to or removed from the rifle and the player.</summary>
        private void ApplyEffects(bool tripleRemoved)
        {
            bool triple = _equipped[(int)ModuleType.TripleShot];
            bool echo = _equipped[(int)ModuleType.Echo];
            bool density = _equipped[(int)ModuleType.Density];
            bool haste = _equipped[(int)ModuleType.Haste];
            bool keskinGoz = _equipped[(int)ModuleType.CritChance];
            bool pierce = _equipped[(int)ModuleType.Pierce];

            if (_rifle != null)
            {
                _rifle.TripleShot = triple;
                _rifle.TripleSpreadDegrees = _tripleSpreadDegrees;
                _rifle.RecoilSpeed = triple ? _tripleRecoilPerSalvo : 0f;
                _rifle.EchoEnabled = echo;
                _rifle.ModuleDamageMultiplier = density ? _densityDamageMultiplier : 1f;
                _rifle.ModuleFireRateMultiplier = haste ? _hasteFireRateMultiplier : 1f;
                _rifle.ModuleSpreadDegrees = haste ? _hasteSpreadDegrees : 0f;
                _rifle.ModuleCritChance = keskinGoz ? _keskinGozCritBonus : 0f;
                _rifle.ModulePierceCount = pierce ? _pierceExtra : 0;
                _rifle.PierceDamageFalloff = pierce ? _pierceDamageFalloff : 0f;
            }
            if (_player != null)
            {
                _player.BodyRadiusScale = density ? _densityBodyScale : 1f;
                if (tripleRemoved) _player.ClearExternalVelocity(); // remaining recoil stops at once
            }
            UpdateFiringSlow(); // removal restores full walk speed the same frame
        }

        /// <summary>
        /// Keskin Göz cost: while equipped and the fire button is held, the walk speed multiplier is multiplied by
        /// ×0.85; released, ejected or dead, it is divided back (other multipliers on it are kept). Dodge unaffected.
        /// </summary>
        private void UpdateFiringSlow()
        {
            if (_player == null) return;
            bool want = _equipped[(int)ModuleType.CritChance] && _input != null && _input.FireHeld &&
                        !_player.Health.IsDead;
            if (want == _firingSlowApplied) return;
            float factor = Mathf.Max(0.05f, _keskinGozFiringSpeedMultiplier);
            _player.MoveSpeedMultiplier = want ? _player.MoveSpeedMultiplier * factor : _player.MoveSpeedMultiplier / factor;
            _firingSlowApplied = want;
        }

        // ------------------------------------------------------------------ selection ring

        private void UpdateSelection()
        {
            bool open = _input != null && _input.EjectHeld;
            if (!open)
            {
                CloseSelection();
                return;
            }

            Vector3 cursor = _player.AimPoint;
            if (!_selectionOpen)
            {
                _selectionOpen = true;
                _highlighted = NearestFilledSlot(cursor, out _); // nearest filled slot on opening
            }
            else
            {
                if (_highlighted >= 0 && _slots[_highlighted] < 0) _highlighted = -1;
                if (FlatDistance(cursor, transform.position) <= _selectionRadius)
                {
                    int nearest = NearestFilledSlot(cursor, out float nearestDistance);
                    if (nearest >= 0 && nearest != _highlighted)
                    {
                        bool take = _highlighted < 0 ||
                                    FlatDistance(cursor, SlotPosition(_highlighted)) - nearestDistance > _selectionHysteresis;
                        if (take) _highlighted = nearest;
                    }
                }
            }
        }

        private void CloseSelection()
        {
            _selectionOpen = false;
            _highlighted = -1;
        }

        private int NearestFilledSlot(Vector3 point, out float distance)
        {
            int best = -1;
            distance = float.PositiveInfinity;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] < 0) continue;
                float d = FlatDistance(point, SlotPosition(i));
                if (d < distance)
                {
                    distance = d;
                    best = i;
                }
            }
            return best;
        }

        private void LateUpdate()
        {
            if (_rejectFlash != null) _rejectFlash.Tick(Time.unscaledDeltaTime);

            // Flat icons spin slowly about world Y so they read from the isometric camera (like the offer).
            _slotSpin = Mathf.Repeat(_slotSpin + Time.deltaTime * SlotSpinDegreesPerSecond, 360f);
            for (int s = 0; s < _slotPivots.Length; s++)
            {
                Transform pivot = _slotPivots[s];
                if (pivot == null) continue;
                int type = _slots[s];
                bool show = _selectionOpen && type >= 0;
                if (pivot.gameObject.activeSelf != show) pivot.gameObject.SetActive(show);
                if (!show) continue;
                for (int t = 0; t < ModuleCatalog.Count; t++)
                {
                    Transform icon = _slotIcons[s, t];
                    if (icon != null && icon.gameObject.activeSelf != (t == type)) icon.gameObject.SetActive(t == type);
                }
                pivot.SetPositionAndRotation(SlotPosition(s), Quaternion.Euler(0f, _slotSpin, 0f));
                pivot.localScale = Vector3.one * (_slotIconScale * (s == _highlighted ? _highlightScale : 1f));
            }
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static Vector3 Flat(Vector3 v, Vector3 fallback)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-6f ? v.normalized : fallback;
        }
    }
}
