using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Renders an orthographic isometric game camera into a low-res RenderTexture and shows it full screen,
    /// point-upscaled (integer scale when possible, letterboxed). Follows a target with texel snapping and a
    /// sub-texel display offset so static geometry does not shimmer while panning; with a player target it leans a
    /// little toward the aim point, and the focus point is clamped to a world XZ box so the view never drifts far
    /// outside the arena (provisional decision D1).
    /// Use <see cref="ScreenPointToWorldRay"/> for mouse aiming (never Camera.main).
    /// </summary>
    [DefaultExecutionOrder(1000)] // after gameplay LateUpdates, so the follow uses the final target position
    [DisallowMultipleComponent]
    public sealed class PixelCameraRig : MonoBehaviour
    {
        /// <summary>Extra texels rendered on each side of the visible window, for the sub-texel shift and the shake.</summary>
        public const int Border = 8;
        private const float AimLeadFraction = 0.3f;      // lead = 30% of the flat distance to the aim, capped at _aimLead
        private const float AimLeadSmoothTime = 0.15f;   // the lead eases on its own so mouse jitter never shakes the view
        // At the start of a scene the aim point appears all at once (the first mouse read): the lead fades in over this
        // window instead, so the view does not jump up to 2 m in a few frames as the run begins.
        private const float LeadWarmupDelay = 0.3f;
        private const float LeadWarmupTime = 1f;
        private const float TraumaFullDistance = 10f;    // positional trauma: full within this flat distance of the focus
        private const float TraumaFarDistance = 25f;     // ...down to TraumaFarScale at this distance and beyond
        private const float TraumaFarScale = 0.25f;

        [Header("Cameras")]
        [Tooltip("Orthographic game camera. Its targetTexture is created and owned by this rig.")]
        [SerializeField] private Camera _gameCamera;

        [Header("Resolution")]
        [Tooltip("Visible low-res width in texels.")]
        [SerializeField, Min(16)] private int _width = 640;
        [Tooltip("Visible low-res height in texels.")]
        [SerializeField, Min(16)] private int _height = 360;
        [Tooltip("If the integer scale covers less than this fraction of the aspect-fit size, use a fractional fit instead.")]
        [SerializeField, Range(0f, 1f)] private float _minIntegerCoverage = 0.8f;
        [Tooltip("Letterbox / clear colour behind the image.")]
        [SerializeField] private Color _barColor = Color.black;
        [Tooltip("Sorting order of the display canvas. HUD canvases must use a higher value.")]
        [SerializeField] private int _displaySortingOrder = -1000;

        [Header("Post-processing (game camera only)")]
        [Tooltip("URP post-processing on the game camera (combo Volume pulse, base bloom). The display camera never has it.")]
        [SerializeField] private bool _postProcessing = true;
        [Tooltip("HDR intermediate buffer on the game camera so HDR VFX colours (> 1) reach Bloom; the result is still " +
                 "written to the LDR 8-bit pixel RenderTexture. Off = bloom only from near-white pixels.")]
        [SerializeField] private bool _hdr = true;

        [Header("Framing")]
        [Tooltip("Half of the visible world height (orthographic size) in metres.")]
        [SerializeField, Min(0.1f)] private float _orthographicSize = 9f;
        [Tooltip("Camera pitch in degrees (30-35; 35.264 is true isometric).")]
        [SerializeField, Range(10f, 89f)] private float _pitch = 30f;
        [Tooltip("Camera yaw in degrees.")]
        [SerializeField] private float _yaw = 45f;
        [Tooltip("Distance from the focus point back along the view direction.")]
        [SerializeField, Min(1f)] private float _distance = 50f;

        [Header("Follow")]
        [Tooltip("Transform to follow (the player).")]
        [SerializeField] private Transform _target;
        [Tooltip("Offset from the target position to the focus point. With no target, the fixed focus point.")]
        [SerializeField] private Vector3 _focusOffset = Vector3.zero;
        [Tooltip("SmoothDamp time in seconds. 0 = locked to target.")]
        [SerializeField, Min(0f)] private float _smoothTime = 0.08f;
        [Tooltip("Shift the displayed image by the snapped-away remainder (smooth sub-texel panning).")]
        [SerializeField] private bool _subTexelOffset = true;
        [Tooltip("Metres the view leans toward the aim point when the target has a PlayerController (0 = none).")]
        [SerializeField, Min(0f)] private float _aimLead = 2f;
        [Tooltip("World minimum (x, z) of the followed focus point. The clamp is off on an axis whose max is not above its min.")]
        [SerializeField] private Vector2 _boundsMin = new Vector2(-10f, -9f);
        [Tooltip("World maximum (x, z) of the followed focus point.")]
        [SerializeField] private Vector2 _boundsMax = new Vector2(10f, 9f);

        // Shake fields carry a "V2" suffix so the old scene values (3 texels, trauma^1.5) are dropped and these
        // defaults apply without a scene rebuild (2026-10-25 juice pass).
        [Header("Shake (trauma model, shown as an image offset so the snapped camera never moves)")]
        [Tooltip("Largest shake offset in low-res texels at full trauma. Clamped below the border (8) minus the remainder.")]
        [SerializeField, Range(0f, 7.4f)] private float _maxShakeTexelsV2 = 6.5f;
        [Tooltip("Shake = trauma^exponent. 1.25 makes medium events (kills, hits) read clearly; 2 would be subtler.")]
        [SerializeField, Range(1f, 3f)] private float _shakeExponentV2 = 1.25f;
        [Tooltip("Trauma lost per second.")]
        [SerializeField, Min(0f)] private float _traumaDecayV2 = 1.6f;
        [Tooltip("Noise frequency in Hz.")]
        [SerializeField, Min(0f)] private float _shakeFrequencyV2 = 22f;
        [Tooltip("Largest roll of the displayed image at full shake, degrees (needs DisplayGrade; off with reduce motion).")]
        [SerializeField, Range(0f, 3f)] private float _maxRollDegrees = 1.2f;
        [Tooltip("Zoom kick half-life in seconds (AddZoomKick).")]
        [SerializeField, Min(0.01f)] private float _zoomKickHalfLife = 0.07f;
        [Tooltip("Largest zoom kick (fraction).")]
        [SerializeField, Range(0f, 0.3f)] private float _maxZoomKick = 0.12f;

        /// <summary>Accessibility multiplier for all camera shake (0 = off).</summary>
        public static float GlobalShakeScale = 1f;

        private float _trauma;
        private Vector2 _shakeTexels;
        private float _rollRadians;
        private float _zoomKick;
        private float _zoom = 1f;

        private RenderTexture _renderTexture;
        private Camera _displayCamera;
        private GameObject _displayCanvasObject;
        private RawImage _rawImage;
        private RectTransform _rawImageRect;
        private Material _displayMaterial;

        private Rect _displayRect;           // screen pixels covered by the visible window
        private Vector2 _remainderTexels;    // snapped-away remainder, |r| <= 0.5
        private Vector3 _focus;
        private Vector3 _focusVelocity;
        private PlayerController _targetPlayer;
        private Vector3 _lead;
        private Vector3 _leadVelocity;
        private int _lastScreenWidth = -1;
        private int _lastScreenHeight = -1;

        public Camera GameCamera => _gameCamera;
        public RenderTexture RenderTexture => _renderTexture;
        /// <summary>World size of one low-res texel (metres).</summary>
        public float TexelWorldSize => 2f * _orthographicSize / _height;
        /// <summary>Screen-pixel rect where the visible low-res window is drawn.</summary>
        public Rect DisplayRect => _displayRect;
        public Quaternion ViewRotation => Quaternion.Euler(_pitch, _yaw, 0f);
        /// <summary>RT UV rect of the visible window this frame (remainder and shake included), as on the RawImage.</summary>
        public Rect DisplayUvRect => new Rect(WindowOffset.x / RtWidth, WindowOffset.y / RtHeight,
            (float)_width / RtWidth, (float)_height / RtHeight);
        /// <summary>Image roll this frame, radians (applied by the display material when <see cref="ApplyRollZoom"/>).</summary>
        public float Roll => ApplyRollZoom ? _rollRadians : 0f;
        /// <summary>Image zoom this frame (1 = none, 1.05 = 5% closer), applied with <see cref="Roll"/>.</summary>
        public float Zoom => ApplyRollZoom ? _zoom : 1f;
        /// <summary>
        /// Set by the display material owner (<see cref="DisplayGrade"/>) when its shader applies <see cref="Roll"/> and
        /// <see cref="Zoom"/>, so aiming and <see cref="WorldToDisplay01"/> include them. Off = no roll or zoom.
        /// </summary>
        public bool ApplyRollZoom { get; set; }
        /// <summary>Visible width / height in texels (aspect of the shown image).</summary>
        public float DisplayAspect => (float)_width / _height;
        /// <summary>Visible window height in world metres (2 × orthographic size).</summary>
        public float ViewHeightMetres => 2f * _orthographicSize;
        /// <summary>Current trauma 0..1.</summary>
        public float Trauma => _trauma;

        // Bottom-left of the visible window inside the RT, in texels.
        private Vector2 WindowOffset => new Vector2(Border + _remainderTexels.x + _shakeTexels.x,
            Border + _remainderTexels.y + _shakeTexels.y);
        /// <summary>The followed transform (null = fixed focus).</summary>
        public Transform Target => _target;
        /// <summary>Current world focus point (the centre of the view).</summary>
        public Vector3 Focus => _focus;

        /// <summary>
        /// Material of the full-screen display RawImage (e.g. the saturation/vignette grade). Null = default UI
        /// material. May be set before Awake; it is applied when the display is created.
        /// </summary>
        public Material DisplayMaterial
        {
            get => _displayMaterial;
            set
            {
                _displayMaterial = value;
                if (_rawImage != null) _rawImage.material = value;
            }
        }

        private int RtWidth => _width + 2 * Border;
        private int RtHeight => _height + 2 * Border;

        private void Awake()
        {
            if (_gameCamera == null)
            {
                Debug.LogError($"{nameof(PixelCameraRig)}: game camera is not assigned.", this);
                enabled = false;
                return;
            }

            CreateRenderTexture();
            ConfigureGameCamera();
            CreateDisplay();

            _targetPlayer = _target != null ? _target.GetComponentInParent<PlayerController>() : null;
            _focus = _target != null ? ClampFocus(_target.position + _focusOffset) : _focusOffset;
            _leadStartTime = Time.time;
            // Scenery between the camera and Defne turns see-through (no scene edit: added here).
            if (!TryGetComponent(out _fader)) _fader = gameObject.AddComponent<OcclusionFader>();
            _fader.Configure(_gameCamera, _target);
            UpdateLayout(force: true);
            UpdateCamera(snapImmediately: true);
        }

        private void OnDestroy()
        {
            if (_gameCamera != null && _gameCamera.targetTexture == _renderTexture)
                _gameCamera.targetTexture = null;

            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
                _renderTexture = null;
            }

            if (_displayCanvasObject != null) Destroy(_displayCanvasObject);
            if (_displayCamera != null) Destroy(_displayCamera.gameObject);
        }

        private void LateUpdate()
        {
            UpdateLayout(force: false);
            UpdateCamera(snapImmediately: false);
        }

        /// <summary>Sets the follow target. If <paramref name="snap"/> is true the camera jumps there.</summary>
        public void SetTarget(Transform target, bool snap = true)
        {
            _target = target;
            _targetPlayer = target != null ? target.GetComponentInParent<PlayerController>() : null;
            if (_fader != null) _fader.Configure(_gameCamera, target);
            if (snap && target != null)
            {
                _lead = Vector3.zero;
                _leadVelocity = Vector3.zero;
                _focus = ClampFocus(target.position + _focusOffset);
                _focusVelocity = Vector3.zero;
            }
        }

        /// <summary>
        /// Adds camera trauma (0..1). Shake = trauma^1.25 × 6.5 texels (0.3 ≈ 1.4 texels, 0.6 ≈ 3.4, 1 = 6.5), so
        /// small hits stay subtle but still move the image.
        /// </summary>
        public void AddTrauma(float amount)
        {
            _trauma = Mathf.Clamp01(_trauma + Mathf.Max(0f, amount));
        }

        /// <summary>
        /// Adds trauma but never past <paramref name="ceiling"/> (an existing higher trauma is kept). For frequent
        /// events (bullet hits, kills) so they jolt each time without stacking into a constant blur.
        /// Example: <c>rig.AddTraumaUpTo(0.1f, 0.35f);</c>
        /// </summary>
        public void AddTraumaUpTo(float amount, float ceiling)
        {
            if (amount <= 0f) return;
            _trauma = Mathf.Max(_trauma, Mathf.Min(_trauma + amount, Mathf.Clamp01(ceiling)));
        }

        /// <summary>
        /// A short zoom-in punch (0.05 = 5% closer) that decays with a 0.07 s half-life; shown by the display material.
        /// Example: <c>rig.AddZoomKick(0.05f);</c> on a rocket blast.
        /// </summary>
        public void AddZoomKick(float amount)
        {
            if (amount <= 0f) return;
            _zoomKick = Mathf.Min(_maxZoomKick, _zoomKick + amount);
        }

        /// <summary>
        /// World position → 0..1 position inside the visible game image (0,0 bottom-left, 1,1 top-right of the drawn
        /// window; outside 0..1 = off screen), including the sub-texel remainder, shake, roll and zoom of this frame.
        /// Inverse of <see cref="ScreenToGameViewport"/>. Multiply by <see cref="DisplayRect"/> size and add its position
        /// for screen pixels. Example: <c>Vector2 p = rig.WorldToDisplay01(enemy.position + Vector3.up * 2f);</c>
        /// </summary>
        public Vector2 WorldToDisplay01(Vector3 world)
        {
            if (_gameCamera == null) return new Vector2(0.5f, 0.5f);
            Vector3 viewport = _gameCamera.WorldToViewportPoint(world);
            Vector2 source = ViewportToWindow01(new Vector2(viewport.x, viewport.y), _width, _height, Border, WindowOffset - new Vector2(Border, Border));
            return SourceToDisplay(source, Roll, Zoom, DisplayAspect);
        }

        /// <summary>
        /// Adds trauma for an event at <paramref name="worldPosition"/>: full within 10 m (flat) of the view centre,
        /// falling to a quarter at 25 m and beyond, so blasts far off screen barely shake the followed view.
        /// </summary>
        public void AddTrauma(float amount, Vector3 worldPosition)
        {
            Vector3 delta = worldPosition - _focus;
            delta.y = 0f;
            float t = Mathf.InverseLerp(TraumaFullDistance, TraumaFarDistance, delta.magnitude);
            AddTrauma(amount * Mathf.Lerp(1f, TraumaFarScale, t));
        }

        /// <summary>
        /// Converts a screen position (pixels, bottom-left origin, e.g. Mouse.current.position) into a world
        /// ray from the low-res game camera, accounting for letterbox, scale, border and sub-texel offset.
        /// </summary>
        public Ray ScreenPointToWorldRay(Vector2 screenPos)
        {
            Vector2 viewport = ScreenToGameViewport(screenPos);
            return _gameCamera.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f));
        }

        /// <summary>True if the screen position is inside the drawn image (not on a letterbox bar).</summary>
        public bool IsInsideView(Vector2 screenPos) => _displayRect.Contains(screenPos);

        /// <summary>Screen position → game camera viewport (0..1 over the whole RT, border included).</summary>
        public Vector2 ScreenToGameViewport(Vector2 screenPos)
        {
            var display = new Vector2((screenPos.x - _displayRect.x) / _displayRect.width,
                (screenPos.y - _displayRect.y) / _displayRect.height);
            Vector2 source = DisplayToSource(display, Roll, Zoom, DisplayAspect);
            return Window01ToViewport(source, _width, _height, Border, WindowOffset - new Vector2(Border, Border));
        }

        // ---------------------------------------------------------------- display math (pure, tested in EditMode)

        /// <summary>
        /// Visible-window 0..1 → game camera viewport 0..1 (whole RT). <paramref name="offsetTexels"/> = remainder + shake.
        /// Example: <c>Window01ToViewport(new Vector2(0.5f, 0.5f), 960, 540, 8, Vector2.zero)</c> = (0.5, 0.5).
        /// </summary>
        public static Vector2 Window01ToViewport(Vector2 window01, int width, int height, int border, Vector2 offsetTexels)
        {
            float rtW = width + 2f * border, rtH = height + 2f * border;
            return new Vector2((border + offsetTexels.x + window01.x * width) / rtW,
                (border + offsetTexels.y + window01.y * height) / rtH);
        }

        /// <summary>Inverse of <see cref="Window01ToViewport"/>.</summary>
        public static Vector2 ViewportToWindow01(Vector2 viewport, int width, int height, int border, Vector2 offsetTexels)
        {
            float rtW = width + 2f * border, rtH = height + 2f * border;
            return new Vector2((viewport.x * rtW - border - offsetTexels.x) / width,
                (viewport.y * rtH - border - offsetTexels.y) / height);
        }

        /// <summary>
        /// Shown position (display 0..1) → sampled window position: the display material shows, at display point q, the
        /// window point c + R(roll)·(q − c) / zoom (aspect-correct, c = centre). Must match UI_DisplayGrade.shader.
        /// </summary>
        public static Vector2 DisplayToSource(Vector2 display, float roll, float zoom, float aspect)
        {
            if (roll == 0f && zoom == 1f) return display;
            float x = (display.x - 0.5f) * aspect, y = display.y - 0.5f;
            float c = Mathf.Cos(roll), s = Mathf.Sin(roll);
            float inv = 1f / Mathf.Max(0.01f, zoom);
            return new Vector2((c * x - s * y) * inv / aspect + 0.5f, (s * x + c * y) * inv + 0.5f);
        }

        /// <summary>Inverse of <see cref="DisplayToSource"/>.</summary>
        public static Vector2 SourceToDisplay(Vector2 source, float roll, float zoom, float aspect)
        {
            if (roll == 0f && zoom == 1f) return source;
            float x = (source.x - 0.5f) * aspect * zoom, y = (source.y - 0.5f) * zoom;
            float c = Mathf.Cos(roll), s = Mathf.Sin(roll);
            return new Vector2((c * x + s * y) / aspect + 0.5f, (-s * x + c * y) + 0.5f);
        }

        // ---------------------------------------------------------------- setup

        private void CreateRenderTexture()
        {
            // sRGB colour so Linear-space rendering is stored/sampled correctly; 24-bit depth; no MSAA, no mips.
            var desc = new RenderTextureDescriptor(RtWidth, RtHeight, RenderTextureFormat.ARGB32, 24)
            {
                sRGB = QualitySettings.activeColorSpace == ColorSpace.Linear,
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false,
            };

            _renderTexture = new RenderTexture(desc)
            {
                name = "PixelCameraRT",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0,
            };
            _renderTexture.Create();
        }

        private void ConfigureGameCamera()
        {
            _gameCamera.orthographic = true;
            // Keep one texel = 2*size/height world units although the RT is taller by the border.
            _gameCamera.orthographicSize = _orthographicSize * RtHeight / _height;
            _gameCamera.allowMSAA = false;
            // HDR only feeds Bloom: tonemapping is None in ArenaVolumeProfile, so the image above 1.0 clips as before
            // and the 8-bit point-filtered RT keeps the crisp pixel look (bloom halos are texel-sized and upscale with it).
            _gameCamera.allowHDR = _hdr;
            _gameCamera.allowDynamicResolution = false;
            _gameCamera.depth = -10f; // render before the display camera
            _gameCamera.targetTexture = _renderTexture;
            _gameCamera.ResetAspect(); // aspect from RT (642/362)

            var data = _gameCamera.GetUniversalAdditionalCameraData();
            data.renderType = CameraRenderType.Base;
            data.renderPostProcessing = _postProcessing;
            data.antialiasing = AntialiasingMode.None; // no FXAA/SMAA smearing of texel edges
            data.dithering = false;                    // no dither noise at 960×540
            data.stopNaN = false;
        }

        private void CreateDisplay()
        {
            // Display camera: only clears the backbuffer (letterbox bars). Renders no layers.
            var camGo = new GameObject("PixelDisplayCamera");
            camGo.transform.SetParent(transform, false);
            _displayCamera = camGo.AddComponent<Camera>();
            _displayCamera.cullingMask = 0;
            _displayCamera.clearFlags = CameraClearFlags.SolidColor;
            _displayCamera.backgroundColor = _barColor;
            _displayCamera.orthographic = true;
            _displayCamera.allowMSAA = false;
            _displayCamera.allowHDR = false;
            _displayCamera.depth = 0f;
            var displayData = _displayCamera.GetUniversalAdditionalCameraData();
            displayData.renderType = CameraRenderType.Base;
            displayData.renderPostProcessing = false;
            displayData.antialiasing = AntialiasingMode.None;
            displayData.renderShadows = false;

            // Overlay canvas with one RawImage. No CanvasScaler: canvas units == screen pixels.
            _displayCanvasObject = new GameObject("PixelDisplayCanvas", typeof(RectTransform));
            _displayCanvasObject.transform.SetParent(transform, false);
            var canvas = _displayCanvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = _displaySortingOrder;
            canvas.pixelPerfect = false;

            var imageGo = new GameObject("PixelDisplayImage", typeof(RectTransform));
            imageGo.transform.SetParent(_displayCanvasObject.transform, false);
            _rawImage = imageGo.AddComponent<RawImage>();
            _rawImage.texture = _renderTexture;
            _rawImage.raycastTarget = false;
            _rawImage.color = Color.white;
            _rawImage.material = _displayMaterial;

            _rawImageRect = _rawImage.rectTransform;
            _rawImageRect.anchorMin = Vector2.zero;   // bottom-left anchored: anchoredPosition == screen pixels
            _rawImageRect.anchorMax = Vector2.zero;
            _rawImageRect.pivot = Vector2.zero;
        }

        // ---------------------------------------------------------------- per frame

        private void UpdateLayout(bool force)
        {
            int sw = Screen.width;
            int sh = Screen.height;
            if (!force && sw == _lastScreenWidth && sh == _lastScreenHeight) return;
            _lastScreenWidth = sw;
            _lastScreenHeight = sh;

            float fit = Mathf.Min((float)sw / _width, (float)sh / _height);
            int integerScale = Mathf.FloorToInt(fit);
            float scale = (integerScale >= 1 && integerScale / fit >= _minIntegerCoverage) ? integerScale : fit;

            float w = Mathf.Round(_width * scale);
            float h = Mathf.Round(_height * scale);
            float x = Mathf.Floor((sw - w) * 0.5f); // whole pixels: keeps screen pixels aligned to texels
            float y = Mathf.Floor((sh - h) * 0.5f);
            _displayRect = new Rect(x, y, w, h);

            _rawImageRect.anchoredPosition = new Vector2(x, y);
            _rawImageRect.sizeDelta = new Vector2(w, h);
        }

        // Unscaled time: shake keeps moving through hitstop.
        private void UpdateShake()
        {
            float dt = Time.unscaledDeltaTime;
            _trauma = Mathf.Max(0f, _trauma - _traumaDecayV2 * dt);
            _zoomKick *= Mathf.Exp(-0.6931472f / _zoomKickHalfLife * dt);
            if (_zoomKick < 1e-4f) _zoomKick = 0f;

            float scale = Mathf.Max(0f, GlobalShakeScale);
            float amount = _trauma > 0f ? Mathf.Pow(_trauma, _shakeExponentV2) : 0f;
            // Clamped so remainder (≤0.5) + shake always stays inside the border.
            float shake = Mathf.Min(amount * _maxShakeTexelsV2 * scale, Border - 0.6f);
            float t = Time.unscaledTime * _shakeFrequencyV2;
            _shakeTexels = shake > 0f
                ? new Vector2((Mathf.PerlinNoise(t, 0.37f) * 2f - 1f) * shake, (Mathf.PerlinNoise(0.71f, t) * 2f - 1f) * shake)
                : Vector2.zero;

            // Roll and zoom: only through the display material, never with reduce motion.
            bool motion = ApplyRollZoom && !UiKit.ReduceMotion;
            _rollRadians = motion
                ? (Mathf.PerlinNoise(t * 0.8f, 5.3f) * 2f - 1f) * amount * _maxRollDegrees * Mathf.Deg2Rad * Mathf.Min(scale, 1.5f)
                : 0f;
            float zoomKick = motion ? _zoomKick * Mathf.Min(scale, 1.5f) : 0f;
            // Enough extra zoom that the rolled image still covers the window corners (no clamped streaks).
            float cover = Mathf.Abs(_rollRadians) * (DisplayAspect + 1f / DisplayAspect) * 0.5f;
            _zoom = 1f + zoomKick + cover;
        }

        private void UpdateCamera(bool snapImmediately)
        {
            if (_target != null)
            {
                Vector3 goal = ClampFocus(_target.position + _focusOffset + UpdateLead(snapImmediately));
                _focus = (snapImmediately || _smoothTime <= 0f)
                    ? goal
                    : Vector3.SmoothDamp(_focus, goal, ref _focusVelocity, _smoothTime);
            }

            Quaternion rotation = ViewRotation;
            Vector3 desired = _focus - rotation * Vector3.forward * _distance;

            // Snap x/y in camera space to the texel grid (anchored at the world origin, fixed rotation).
            float texel = TexelWorldSize;
            Vector3 local = Quaternion.Inverse(rotation) * desired;
            var snapped = new Vector3(
                Mathf.Round(local.x / texel) * texel,
                Mathf.Round(local.y / texel) * texel,
                local.z);

            _remainderTexels = _subTexelOffset
                ? new Vector2((local.x - snapped.x) / texel, (local.y - snapped.y) / texel)
                : Vector2.zero;

            _gameCamera.transform.SetPositionAndRotation(rotation * snapped, rotation);

            UpdateShake();

            // Show the window shifted by the remainder and shake (border keeps it inside the RT).
            _rawImage.uvRect = DisplayUvRect;
        }

        private float _leadStartTime;
        private OcclusionFader _fader;

        // Flat look-ahead toward the player's aim point, eased on its own (scaled time: it waits through hitstop).
        private Vector3 UpdateLead(bool snap)
        {
            Vector3 desired = Vector3.zero;
            if (_aimLead > 0f && _targetPlayer != null)
            {
                Vector3 toAim = _targetPlayer.AimPoint - _target.position;
                toAim.y = 0f;
                desired = Vector3.ClampMagnitude(toAim * AimLeadFraction, _aimLead);
                desired *= Mathf.SmoothStep(0f, 1f, (Time.time - _leadStartTime - LeadWarmupDelay) / LeadWarmupTime);
            }

            if (snap)
            {
                _lead = desired;
                _leadVelocity = Vector3.zero;
            }
            else
            {
                _lead = Vector3.SmoothDamp(_lead, desired, ref _leadVelocity, AimLeadSmoothTime);
            }
            return _lead;
        }

        private Vector3 ClampFocus(Vector3 focus)
        {
            if (_boundsMax.x > _boundsMin.x) focus.x = Mathf.Clamp(focus.x, _boundsMin.x, _boundsMax.x);
            if (_boundsMax.y > _boundsMin.y) focus.z = Mathf.Clamp(focus.z, _boundsMin.y, _boundsMax.y);
            return focus;
        }
    }
}
