using System;
using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Defne's body feedback (audit F9), presentation only. When damage lands: the model flashes white then red, it is
    /// knocked a little away from the hit (only the model moves, never the controller: no rule change; the
    /// CharacterAnimator adds its own flinch), the camera shakes, and <see cref="HurtPulse"/> asks the UI for a red
    /// screen-edge pulse; then the model blinks for the rest of the hit invulnerability. No hitstop on getting hit (it
    /// read as lag). On a dodge: a short trail of flat-tinted afterimages of the model and a puff of dust at the feet.
    /// Colours go through the renderers' property blocks and are put back to what they were before the flash, so
    /// the contract greys of <see cref="DefneCostVisuals"/> survive.
    /// Put it on the player root (PlayerModules adds it at runtime when the scene has none).
    /// </summary>
    [DefaultExecutionOrder(900)] // after PlayerController moved the dodge this frame
    [RequireComponent(typeof(PlayerController), typeof(Health))]
    [DisallowMultipleComponent]
    public sealed class HurtFeedback : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        /// <summary>
        /// Raised when damage lands on Defne, with a strength 0..1 (bigger hits pulse harder). The UI draws a short
        /// red screen-edge (vignette) pulse from it; this component draws nothing on screen.
        /// </summary>
        public static event Action<float> HurtPulse;

        [Header("Hurt flash (unscaled seconds)")]
        [SerializeField] private Color _flashWhite = Color.white;
        [SerializeField] private Color _flashRed = new Color(1f, 0.22f, 0.22f);
        [Tooltip("White first, then red, then the model's own colours again.")]
        [SerializeField, Min(0f)] private float _whiteTime = 0.05f;
        [SerializeField, Min(0f)] private float _redTime = 0.08f;

        [Header("Hurt knock, shake, blink")]
        // TODO(Burak): a real push of the CharacterController on getting hit would change the rules (GDD §4/§6 have
        // no player knockback), so it stays visual until decided.
        [Tooltip("Visual knock of the model away from the hit in metres (the controller does not move).")]
        [SerializeField, Min(0f)] private float _knockDistance = 0.22f;
        [Tooltip("Camera trauma on a hit (0.5 = about 1 texel of shake).")]
        [SerializeField, Range(0f, 1f)] private float _trauma = 0.5f;
        [Tooltip("Blink rate during the rest of the post-hit invulnerability, Hz.")]
        [SerializeField, Min(1f)] private float _blinkHz = 12f;
        [Tooltip("Weakest vignette pulse; a hit of 30% of max health or more pulses at full strength.")]
        [SerializeField, Range(0f, 1f)] private float _minPulse = 0.45f;

        [Header("Dodge afterimage")]
        [Tooltip("Afterimages per dodge.")]
        [SerializeField, Range(0, 6)] private int _ghostCount = 3;
        [Tooltip("Seconds into the dodge for the first afterimage, then between afterimages.")]
        [SerializeField, Min(0.01f)] private float _ghostInterval = 0.045f;
        [Tooltip("Life of one afterimage, seconds (scaled).")]
        [SerializeField, Min(0.02f)] private float _ghostLifetime = 0.16f;
        [Tooltip("Afterimage tint, fresh and in its second half.")]
        [SerializeField] private Color _ghostColor = new Color(0.62f, 0.98f, 0.95f);
        [SerializeField] private Color _ghostFadeColor = new Color(0.22f, 0.5f, 0.56f);
        [Tooltip("Dust cubes kicked up at the feet when the dodge starts.")]
        [SerializeField, Min(0)] private int _dustCubes = 5;
        [SerializeField] private Color _dustColor = new Color(0.55f, 0.55f, 0.62f);

        private sealed class Ghost
        {
            public GameObject Root;
            public Transform[] Parts;
            public MeshRenderer[] Renderers;
            public float Age;
            public int Step;
            public bool Active;
        }

        private PlayerController _player;
        private Health _health;
        private ModelPunch _punch;
        private CharacterAnimator _animator;
        private MaterialPropertyBlock _block;
        private MaterialPropertyBlock _ghostBlock;
        private MeshRenderer[] _renderers = Array.Empty<MeshRenderer>();
        private Color[] _savedColors = Array.Empty<Color>();
        private Transform[] _sourceParts = Array.Empty<Transform>();
        private Ghost[] _ghosts = Array.Empty<Ghost>();
        private int _nextGhost;
        private bool _ready;

        private int _flashStage;   // 0 none, 1 white, 2 red
        private float _flashTime;  // unscaled
        private bool _blinking;
        private float _blinkStart; // scaled
        private float _blinkEnd;
        private bool _hidden;

        private int _ghostsLeft;
        private float _ghostTimer;
        private Vector3 _dodgeStart;

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _health = GetComponent<Health>();
            TryGetComponent(out _animator); // it does not subscribe to Health itself; OnDamaged calls its Flinch
            _block = new MaterialPropertyBlock();
            _ghostBlock = new MaterialPropertyBlock();
        }

        // First Update, not Awake or Start: CharacterAnimator builds the gun cubes in its Start, and they must be
        // part of the flash, the blink and the afterimage.
        private void Initialize()
        {
            Transform model = _player.Model != null ? _player.Model : transform;
            _renderers = model.GetComponentsInChildren<MeshRenderer>(true);
            _savedColors = new Color[_renderers.Length];

            if (model != transform) // never move the player root itself
            {
                if (!TryGetComponent(out _punch)) _punch = gameObject.AddComponent<ModelPunch>();
                _punch.Bind(model, true); // unscaled: the knock returns even while the Devil screen freezes time
            }

            BuildGhosts();
            _ready = true;
        }

        private void OnEnable()
        {
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
            _player.DodgeStarted += OnDodgeStarted;
        }

        private void OnDisable()
        {
            _health.Damaged -= OnDamaged;
            _health.Died -= OnDied;
            _player.DodgeStarted -= OnDodgeStarted;
            if (_flashStage > 0) RestoreColors(_flashStage == 1 ? _flashWhite : _flashRed);
            _flashStage = 0;
            _blinking = false;
            SetHidden(false);
            HideGhosts();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _ghosts.Length; i++)
                if (_ghosts[i] != null && _ghosts[i].Root != null) Destroy(_ghosts[i].Root);
        }

        // ------------------------------------------------------------------ hurt

        private void OnDamaged(DamageInfo info, float remaining)
        {
            if (!_ready) return;

            Vector3 push = info.Direction;
            push.y = 0f;
            if (push.sqrMagnitude < 1e-6f)
            {
                push = transform.position - info.Point;
                push.y = 0f;
            }
            bool hasPush = push.sqrMagnitude > 1e-6f;
            if (hasPush) push.Normalize();

            StartFlash();
            if (hasPush && _punch != null) _punch.Punch(push * _knockDistance, 0f);
            // Flinch takes the direction the hit came from (toward the attacker); zero rocks straight back.
            if (_animator != null) _animator.Flinch(hasPush ? -push : Vector3.zero);

            PixelCameraRig rig = _player.CameraRig;
            if (rig != null && _trauma > 0f) rig.AddTrauma(_trauma);

            float fraction = _health.MaxHealth > 0f ? info.Amount / (_health.MaxHealth * 0.3f) : 1f;
            HurtPulse?.Invoke(Mathf.Lerp(_minPulse, 1f, Mathf.Clamp01(fraction)));

            if (remaining > 0f)
            {
                _blinkStart = Time.time + _whiteTime + _redTime; // the flash shows first
                _blinkEnd = Time.time + _health.InvulnerabilityAfterHit;
                _blinking = _blinkEnd > _blinkStart;
            }
        }

        private void OnDied(DamageInfo info)
        {
            _blinking = false;
            SetHidden(false);
        }

        private void StartFlash()
        {
            if (_flashStage == 0)
            {
                // Remember each part's colour as it is now (material colour or a contract grey), then go white.
                for (int i = 0; i < _renderers.Length; i++)
                {
                    MeshRenderer r = _renderers[i];
                    if (r == null) continue;
                    r.GetPropertyBlock(_block);
                    _savedColors[i] = _block.HasColor(BaseColorId) ? _block.GetColor(BaseColorId) : PixelImpactFeedback.PartColor(r);
                    _block.SetColor(BaseColorId, _flashWhite);
                    r.SetPropertyBlock(_block);
                }
            }
            else
            {
                SwapColor(_flashStage == 1 ? _flashWhite : _flashRed, _flashWhite);
            }
            _flashStage = 1;
            _flashTime = 0f;
        }

        // Sets the colour only on parts still showing the flash colour (another script may have written since).
        private void SwapColor(Color expected, Color next)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                MeshRenderer r = _renderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                if (_block.GetColor(BaseColorId) != expected) continue;
                _block.SetColor(BaseColorId, next);
                r.SetPropertyBlock(_block);
            }
        }

        private void RestoreColors(Color expected)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                MeshRenderer r = _renderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                if (_block.GetColor(BaseColorId) != expected) continue;
                _block.SetColor(BaseColorId, _savedColors[i]);
                r.SetPropertyBlock(_block);
            }
        }

        private void SetHidden(bool hidden)
        {
            if (_hidden == hidden) return;
            _hidden = hidden;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null) _renderers[i].enabled = !hidden;
        }

        // ------------------------------------------------------------------ dodge

        private void OnDodgeStarted()
        {
            if (!_ready) return;
            _dodgeStart = transform.position;
            _ghostsLeft = _ghosts.Length > 0 ? _ghostCount : 0;
            _ghostTimer = 0f;

            PixelImpactFeedback feedback = PixelImpactFeedback.Instance;
            if (feedback != null && _dustCubes > 0)
                feedback.Puff(transform.position + Vector3.up * 0.1f, Vector3.zero, _dustCubes, 0.14f, 2.5f, _dustColor);
        }

        private void SpawnGhost()
        {
            Ghost ghost = _ghosts[_nextGhost];
            _nextGhost = (_nextGhost + 1) % _ghosts.Length;

            // A step behind the body along the dodge, so the newest afterimage never z-fights the model.
            Vector3 travelled = transform.position - _dodgeStart;
            travelled.y = 0f;
            Vector3 offset = travelled.sqrMagnitude > 1e-6f ? -travelled.normalized * 0.25f : Vector3.zero;

            for (int i = 0; i < ghost.Parts.Length; i++)
            {
                Transform source = _sourceParts[i];
                Transform part = ghost.Parts[i];
                bool show = source != null && source.gameObject.activeInHierarchy;
                if (part.gameObject.activeSelf != show) part.gameObject.SetActive(show);
                if (!show) continue;
                part.SetPositionAndRotation(source.position + offset, source.rotation);
                part.localScale = source.lossyScale;
            }

            ghost.Age = 0f;
            ghost.Step = 0;
            ghost.Active = true;
            SetGhostColor(ghost, _ghostColor);
            ghost.Root.SetActive(true);
        }

        private void SetGhostColor(Ghost ghost, Color color)
        {
            _ghostBlock.SetColor(BaseColorId, color);
            for (int i = 0; i < ghost.Renderers.Length; i++) ghost.Renderers[i].SetPropertyBlock(_ghostBlock);
        }

        private void HideGhosts()
        {
            _ghostsLeft = 0;
            for (int i = 0; i < _ghosts.Length; i++)
            {
                if (_ghosts[i] == null || _ghosts[i].Root == null) continue;
                _ghosts[i].Active = false;
                _ghosts[i].Root.SetActive(false);
            }
        }

        private void BuildGhosts()
        {
            PixelImpactFeedback feedback = PixelImpactFeedback.Instance;
            Material material = feedback != null ? feedback.FlashMaterial : null;
            if (material == null || _ghostCount <= 0) return;

            int count = 0;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null && _renderers[i].TryGetComponent(out MeshFilter _)) count++;
            if (count == 0) return;

            _sourceParts = new Transform[count];
            var meshes = new Mesh[count];
            for (int i = 0, j = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null || !_renderers[i].TryGetComponent(out MeshFilter filter)) continue;
                _sourceParts[j] = _renderers[i].transform;
                meshes[j] = filter.sharedMesh;
                j++;
            }

            _ghosts = new Ghost[_ghostCount];
            for (int g = 0; g < _ghosts.Length; g++)
            {
                // No parent: parts copy world poses, so the root stays at the origin, unscaled.
                var root = new GameObject($"{name}_DodgeGhost_{g}");
                root.SetActive(false);
                var ghost = new Ghost
                {
                    Root = root,
                    Parts = new Transform[count],
                    Renderers = new MeshRenderer[count],
                };
                for (int i = 0; i < count; i++)
                {
                    var part = new GameObject(_sourceParts[i].name);
                    part.transform.SetParent(root.transform, false);
                    part.AddComponent<MeshFilter>().sharedMesh = meshes[i];
                    var meshRenderer = part.AddComponent<MeshRenderer>();
                    meshRenderer.sharedMaterial = material;
                    meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    meshRenderer.receiveShadows = false;
                    ghost.Parts[i] = part.transform;
                    ghost.Renderers[i] = meshRenderer;
                }
                _ghosts[g] = ghost;
            }
        }

        // ------------------------------------------------------------------ per frame

        private void Update()
        {
            if (!_ready)
            {
                Initialize();
                return;
            }

            if (_flashStage > 0)
            {
                _flashTime += Time.unscaledDeltaTime;
                if (_flashStage == 1 && _flashTime >= _whiteTime)
                {
                    SwapColor(_flashWhite, _flashRed);
                    _flashStage = 2;
                }
                if (_flashStage == 2 && _flashTime >= _whiteTime + _redTime)
                {
                    RestoreColors(_flashRed);
                    _flashStage = 0;
                }
            }

            if (_blinking)
            {
                float now = Time.time;
                if (now >= _blinkEnd || _health.IsDead)
                {
                    _blinking = false;
                    SetHidden(false);
                }
                else if (now >= _blinkStart)
                {
                    SetHidden(Mathf.FloorToInt((now - _blinkStart) * _blinkHz * 2f) % 2 == 1);
                }
            }

            float dt = Time.deltaTime;
            if (_ghostsLeft > 0)
            {
                _ghostTimer += dt;
                int spawned = _ghostCount - _ghostsLeft;
                if (_ghostTimer >= _ghostInterval * (spawned + 1))
                {
                    SpawnGhost();
                    _ghostsLeft--;
                }
                if (!_player.IsDodging) _ghostsLeft = 0;
            }

            for (int g = 0; g < _ghosts.Length; g++)
            {
                Ghost ghost = _ghosts[g];
                if (!ghost.Active) continue;
                ghost.Age += dt;
                if (ghost.Age >= _ghostLifetime)
                {
                    ghost.Active = false;
                    ghost.Root.SetActive(false);
                    continue;
                }
                if (ghost.Step == 0 && ghost.Age >= _ghostLifetime * 0.5f)
                {
                    // Second half: darker and a step smaller (hard steps, pixel look).
                    ghost.Step = 1;
                    SetGhostColor(ghost, _ghostFadeColor);
                    for (int i = 0; i < ghost.Parts.Length; i++) ghost.Parts[i].localScale *= 0.85f;
                }
            }
        }
    }
}
