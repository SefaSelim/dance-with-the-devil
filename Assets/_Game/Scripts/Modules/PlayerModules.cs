using System;
using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// The player's whisper modules (MAIN_GDD §7; first GDD §6-7): the single offer (approach, 14 s lifetime,
    /// 0.45 s intentional contact accept), capacity (one per type), right-click reject (60° cone, 3 m), Q selection
    /// ring with fixed slots, Q + right-click eject, the shared 0.65 s cooldown, and applying/removing every module
    /// effect on the Rifle and PlayerController in one place (<see cref="ApplyEffects"/>).
    /// Offers are placed by <see cref="ModuleSpawner"/> through <see cref="PresentOffer"/>. Scaled time throughout.
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

        [Header("Selection ring (first GDD §6)")]
        [Tooltip("Slot offsets in the camera-flat frame (x = screen right, z = screen up on the floor, y = height), index = ModuleType.")]
        [SerializeField] private Vector3[] _slotOffsets =
        {
            new Vector3(-1.2f, 1.2f, 0f),
            new Vector3(0f, 1.2f, 1.2f),
            new Vector3(1.2f, 1.2f, 0f),
        };
        [Tooltip("While the cursor is within this flat distance of the player, the highlight follows it; outside it is kept.")]
        [SerializeField, Min(0.1f)] private float _selectionRadius = 2.2f;
        [Tooltip("A different slot must be this much closer to the cursor to take the highlight (no flicker on borders).")]
        [SerializeField, Min(0f)] private float _selectionHysteresis = 0.15f;
        [Tooltip("Slot cube size in metres.")]
        [SerializeField, Min(0.01f)] private float _slotSize = 0.35f;
        [Tooltip("Scale of the highlighted slot cube.")]
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

        private PlayerController _player;
        private PlayerInputReader _input;
        private Material[] _materials;
        private ModuleOffer _offer;
        private Transform _slotRoot;
        private readonly Transform[] _slotCubes = new Transform[ModuleCatalog.Count];
        private readonly bool[] _equipped = new bool[ModuleCatalog.Count];
        private float _readyTime = float.NegativeInfinity;
        private int _smallNoCount;
        private bool _selectionOpen;
        private int _highlighted = -1;

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
        /// <summary>The highlighted equipped module while the selection is open, else null.</summary>
        public ModuleType? Highlighted => _selectionOpen && _highlighted >= 0 ? (ModuleType)_highlighted : (ModuleType?)null;
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
        public PlayerController Player => _player;
        public Rifle Rifle => _rifle;
        public ModuleThrower Thrower => _thrower;

        public bool IsEquipped(ModuleType type) => _equipped[(int)type];

        /// <summary>World position of a fixed slot (around the player, camera-relative).</summary>
        public Vector3 SlotPosition(ModuleType type)
        {
            Vector3 offset = (int)type < _slotOffsets.Length ? _slotOffsets[(int)type] : Vector3.up;
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

            _materials = ModuleVisuals.CreateMaterials(_moduleMaterial);
            BuildSlots();
            ApplyEffects(false);
        }

        private void OnDestroy()
        {
            if (_offer != null) Destroy(_offer.gameObject);
            if (_slotRoot != null) Destroy(_slotRoot.gameObject);
            ModuleVisuals.DestroyMaterials(_materials);
        }

        private void BuildSlots()
        {
            _slotRoot = new GameObject(name + "_ModuleSlots").transform;
            for (int i = 0; i < ModuleCatalog.Count; i++)
            {
                _slotCubes[i] = ModuleVisuals.CreatePart("Slot_" + (ModuleType)i, PrimitiveType.Cube, _slotRoot,
                    Vector3.zero, Vector3.one * _slotSize, _materials[i]);
                _slotCubes[i].gameObject.SetActive(false);
            }
        }

        // ------------------------------------------------------------------ offers

        /// <summary>True when a new offer may appear (none active, player alive).</summary>
        public bool CanPresentOffer => !HasOffer && (_player == null || !_player.Health.IsDead);

        /// <summary>Shows an offer of <paramref name="type"/> on the floor point. Fails if one is already active.</summary>
        public bool PresentOffer(ModuleType type, Vector3 floorPosition)
        {
            if (!CanPresentOffer) return false;
            if (_offer == null) _offer = ModuleOffer.Create(_materials, (ContactRadius) * 2f);
            _offer.Show(type, floorPosition, _offerLifetime);
            _offer.IsEquippedDuplicate = IsEquipped(type);
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
                return;
            }

            TickOffer(Time.deltaTime);
            UpdateSelection();

            // Right click during a dodge is dropped, not buffered (GDD §4).
            if (_input != null && _input.RejectPressed && !_player.IsDodging)
            {
                if (_selectionOpen)
                {
                    if (_highlighted >= 0) TryEject((ModuleType)_highlighted);
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

            bool duplicate = IsEquipped(offer.Type);
            offer.IsEquippedDuplicate = duplicate;

            Vector3 toCore = transform.position - offer.Position;
            toCore.y = 0f;
            bool contact = toCore.magnitude <= ContactRadius;
            // Leaving, dodging through it, or a duplicate: progress resets (GDD §7 Niyetli kabul, Kapasite).
            if (!contact || duplicate || _player.IsDodging)
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
            if (offer == null) return false;

            Vector3 toOffer = offer.Position - transform.position;
            toOffer.y = 0f;
            float distance = toOffer.magnitude;
            if (distance > _rejectRange) return false;
            if (distance > 1e-3f && Vector3.Angle(aim, toOffer) > _rejectConeDegrees * 0.5f) return false;

            ModuleType type = offer.Type;
            Vector3 origin = offer.CorePosition;
            offer.Hide();
            _thrower.Throw(type, origin, aim);
            _smallNoCount++;
            Rejected?.Invoke(type);
            return true;
        }

        /// <summary>Ejects an equipped module toward the aim (removes its effects, throws it). Shares the cooldown.</summary>
        public bool TryEject(ModuleType type)
        {
            if (_player == null || _player.Health.IsDead || _player.IsDodging || !RejectReady) return false;
            if (!IsEquipped(type)) return false;
            _readyTime = Time.time + _cooldown;

            Vector3 aim = _player.AimDirection;
            SetEquipped(type, false);
            if (_highlighted == (int)type) _highlighted = -1;
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
            _highlighted = -1;
            if (!any) return;
            ApplyEffects(true);
            EquippedChanged?.Invoke();
        }

        /// <summary>Equips directly (debug and tests). Returns false if already equipped. Raises Accepted.</summary>
        public bool EquipImmediate(ModuleType type)
        {
            if (IsEquipped(type)) return false;
            SetEquipped(type, true);
            Accepted?.Invoke(type);
            return true;
        }

        private void SetEquipped(ModuleType type, bool value)
        {
            bool hadTriple = _equipped[(int)ModuleType.TripleShot];
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

            if (_rifle != null)
            {
                _rifle.TripleShot = triple;
                _rifle.TripleSpreadDegrees = _tripleSpreadDegrees;
                _rifle.RecoilSpeed = triple ? _tripleRecoilPerSalvo : 0f;
                _rifle.EchoEnabled = echo;
                _rifle.ModuleDamageMultiplier = density ? _densityDamageMultiplier : 1f;
            }
            if (_player != null)
            {
                _player.BodyRadiusScale = density ? _densityBodyScale : 1f;
                if (tripleRemoved) _player.ClearExternalVelocity(); // remaining recoil stops at once
            }
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
                _highlighted = NearestEquippedSlot(cursor, out _); // nearest filled slot on opening
            }
            else
            {
                if (_highlighted >= 0 && !_equipped[_highlighted]) _highlighted = -1;
                if (FlatDistance(cursor, transform.position) <= _selectionRadius)
                {
                    int nearest = NearestEquippedSlot(cursor, out float nearestDistance);
                    if (nearest >= 0 && nearest != _highlighted)
                    {
                        bool take = _highlighted < 0 ||
                                    FlatDistance(cursor, SlotPosition((ModuleType)_highlighted)) - nearestDistance > _selectionHysteresis;
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

        private int NearestEquippedSlot(Vector3 point, out float distance)
        {
            int best = -1;
            distance = float.PositiveInfinity;
            for (int i = 0; i < ModuleCatalog.Count; i++)
            {
                if (!_equipped[i]) continue;
                float d = FlatDistance(point, SlotPosition((ModuleType)i));
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
            for (int i = 0; i < ModuleCatalog.Count; i++)
            {
                Transform cube = _slotCubes[i];
                if (cube == null) continue;
                bool show = _selectionOpen && _equipped[i];
                if (cube.gameObject.activeSelf != show) cube.gameObject.SetActive(show);
                if (!show) continue;
                cube.position = SlotPosition((ModuleType)i);
                cube.localScale = Vector3.one * (_slotSize * (i == _highlighted ? _highlightScale : 1f));
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
