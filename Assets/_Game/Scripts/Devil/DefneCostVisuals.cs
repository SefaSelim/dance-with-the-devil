using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// The contract costs that show on Defne's model (MAIN_GDD §8): hair colour drains to grey (1 "Rengin"),
    /// face parts burst into cubes and disappear one by one (4 "Yüzün", minimum scope: whole parts instead of single
    /// voxels; the hit area stays, K-13), the whole body turns grey (6 "Kendin"), and the grey shell crumbles into cubes at the
    /// always-yes ending. Colours are set through a MaterialPropertyBlock (_BaseColor), so shared materials are not
    /// touched. Runs on unscaled time so it also animates while time is frozen.
    /// Put it on the player root; the model is the child named "Model" unless assigned.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DefneCostVisuals : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private const string DefaultModelName = "Model";

        [Tooltip("Root of Defne's cube parts. Empty = the child named 'Model'.")]
        [SerializeField] private Transform _model;
        [Tooltip("Parts whose name starts with this lose their colour with contract 1.")]
        [SerializeField] private string _hairPrefix = "Hair_";
        [Tooltip("Face parts hidden in this order with contract 4 (GameObjects deactivated; collider unchanged).")]
        [SerializeField] private string[] _faceParts = { "Hair_Fringe", "Glasses_L", "Glasses_R", "Hair_Side_L" };
        [Tooltip("Seconds for a part to fade to grey (unscaled).")]
        [SerializeField, Min(0f)] private float _greyFadeDuration = 1.2f;
        [Tooltip("Delay before the first face part goes, then between parts, seconds (unscaled).")]
        [SerializeField, Min(0f)] private float _facePartInterval = 0.35f;
        [Tooltip("Brightness of the grey relative to the part's luminance.")]
        [SerializeField, Range(0f, 2f)] private float _greyBrightness = 0.85f;
        [Tooltip("Cube speed multiplier when the shell crumbles.")]
        [SerializeField, Min(0f)] private float _crumbleForce = 0.6f;
        [Tooltip("Contract 4: each face part bursts into cubes as it goes (so the loss reads at play scale).")]
        [SerializeField] private bool _burstFaceParts = true;
        [Tooltip("Cube speed multiplier of a face part's burst.")]
        [SerializeField, Min(0f)] private float _faceBurstForce = 0.5f;

        private MeshRenderer[] _renderers = System.Array.Empty<MeshRenderer>();
        private Color[] _baseColors;
        private float[] _grey;
        private float[] _greyTarget;
        private bool[] _isHair;
        private Transform[] _faceTransforms;
        private MaterialPropertyBlock _block;
        private int _faceNext = -1;
        private float _faceTimer;
        private bool _fading;
        private readonly Transform[] _onePart = new Transform[1];
        private readonly Color[] _oneColor = new Color[1];
        private bool _partsSettled;

        public Transform Model => _model;
        public bool IsCrumbled { get; private set; }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            if (_model == null) _model = FindDirectChild(transform, DefaultModelName);
            Cache();
        }

        /// <summary>Contract 1: hair parts lerp to grey.</summary>
        public void DrainHair()
        {
            for (int i = 0; i < _renderers.Length; i++)
                if (_isHair[i]) _greyTarget[i] = 1f;
            _fading = true;
        }

        /// <summary>Contract 4: face parts disappear one by one.</summary>
        public void RemoveFaceParts()
        {
            if (_faceTransforms == null || _faceTransforms.Length == 0) return;
            _faceNext = 0;
            _faceTimer = 0f;
        }

        /// <summary>Contract 6: every part lerps to grey.</summary>
        public void TurnAllGrey()
        {
            for (int i = 0; i < _greyTarget.Length; i++) _greyTarget[i] = 1f;
            _fading = true;
        }

        /// <summary>
        /// Always-yes ending: bursts the visible parts, in their current (grey) colours, into cubes through
        /// <see cref="PixelImpactFeedback.Shatter"/> and hides the model. Cubes simulate on scaled time.
        /// </summary>
        public void Crumble(Vector3 point)
        {
            if (IsCrumbled || _model == null) return;
            IsCrumbled = true;

            int visible = 0;
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i] != null && _renderers[i].gameObject.activeInHierarchy) visible++;

            var parts = new Transform[visible];
            var colors = new Color[visible];
            for (int i = 0, j = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null || !_renderers[i].gameObject.activeInHierarchy) continue;
                parts[j] = _renderers[i].transform;
                colors[j] = CurrentColor(i);
                j++;
            }

            if (PixelImpactFeedback.Instance != null)
                PixelImpactFeedback.Instance.Shatter(parts, colors, point, Vector3.up, _crumbleForce);
            _model.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!_partsSettled) SettleParts();
            float dt = Time.unscaledDeltaTime;
            if (_fading) TickGrey(dt);
            if (_faceNext >= 0) TickFace(dt);
        }

        // CharacterAnimator builds Defne's rifle cubes in its Start, after this Awake cached the parts: pick them up
        // once on the first frame (contract 6 greys the gun too, the crumble bursts it), unless a cost already began.
        private void SettleParts()
        {
            _partsSettled = true;
            if (_model == null || _fading || _faceNext >= 0) return;
            for (int i = 0; i < _greyTarget.Length; i++)
                if (_greyTarget[i] > 0f || _grey[i] > 0f) return;
            if (_model.GetComponentsInChildren<MeshRenderer>(true).Length != _renderers.Length) Cache();
        }

        private void TickGrey(float dt)
        {
            float step = _greyFadeDuration > 0f ? dt / _greyFadeDuration : 1f;
            bool any = false;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (Mathf.Approximately(_grey[i], _greyTarget[i])) continue;
                _grey[i] = Mathf.MoveTowards(_grey[i], _greyTarget[i], step);
                Apply(i);
                any = true;
            }
            _fading = any;
        }

        private void TickFace(float dt)
        {
            _faceTimer += dt;
            while (_faceNext >= 0 && _faceTimer >= _facePartInterval)
            {
                _faceTimer -= _facePartInterval;
                Transform part = _faceTransforms[_faceNext];
                if (part != null)
                {
                    if (_burstFaceParts) BurstPart(part);
                    part.gameObject.SetActive(false);
                }
                _faceNext++;
                if (_faceNext >= _faceTransforms.Length) _faceNext = -1;
            }
        }

        /// <summary>One <see cref="PixelImpactFeedback.Shatter"/> call for a whole part in its current colour.</summary>
        private void BurstPart(Transform part)
        {
            if (PixelImpactFeedback.Instance == null || !part.gameObject.activeInHierarchy) return;
            Color color = Color.gray;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null || _renderers[i].transform != part) continue;
                color = CurrentColor(i);
                break;
            }
            _onePart[0] = part;
            _oneColor[0] = color;
            PixelImpactFeedback.Instance.Shatter(_onePart, _oneColor, part.position, Vector3.up, _faceBurstForce);
            _onePart[0] = null;
        }

        private void Cache()
        {
            if (_model == null)
            {
                Debug.LogWarning($"{nameof(DefneCostVisuals)}: no model (child '{DefaultModelName}') found.", this);
                _baseColors = System.Array.Empty<Color>();
                _grey = _greyTarget = System.Array.Empty<float>();
                _isHair = System.Array.Empty<bool>();
                _faceTransforms = System.Array.Empty<Transform>();
                return;
            }

            _renderers = _model.GetComponentsInChildren<MeshRenderer>(true);
            int n = _renderers.Length;
            _baseColors = new Color[n];
            _grey = new float[n];
            _greyTarget = new float[n];
            _isHair = new bool[n];
            for (int i = 0; i < n; i++)
            {
                _baseColors[i] = PixelImpactFeedback.PartColor(_renderers[i]);
                _isHair[i] = !string.IsNullOrEmpty(_hairPrefix) && _renderers[i].name.StartsWith(_hairPrefix);
            }

            _faceTransforms = new Transform[_faceParts != null ? _faceParts.Length : 0];
            for (int f = 0; f < _faceTransforms.Length; f++)
            {
                for (int i = 0; i < n; i++)
                {
                    if (_renderers[i].name != _faceParts[f]) continue;
                    _faceTransforms[f] = _renderers[i].transform;
                    break;
                }
            }
        }

        private Color CurrentColor(int i)
        {
            Color c = _baseColors[i];
            float lum = (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) * _greyBrightness;
            return Color.Lerp(c, new Color(lum, lum, lum, c.a), _grey[i]);
        }

        private void Apply(int i)
        {
            MeshRenderer r = _renderers[i];
            if (r == null) return;
            r.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, CurrentColor(i));
            r.SetPropertyBlock(_block);
        }

        private static Transform FindDirectChild(Transform parent, string childName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == childName) return child;
            }
            return null;
        }
    }
}
