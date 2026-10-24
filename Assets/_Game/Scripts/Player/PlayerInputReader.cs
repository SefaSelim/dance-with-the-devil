using UnityEngine;
using UnityEngine.InputSystem;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Player input built in code (no .inputactions asset). Bindings follow GDD §4 (Kontroller).
    /// Reject = right mouse, EjectHeld = Q (whisper modules, GDD §7).
    /// Actions are enabled/disabled with the component. "Pressed" properties are true only on the press frame.
    /// <see cref="InputDelay"/> (Contract 6, GDD §8): when above 0, move, aim, held and pressed states are read from a
    /// time-stamped ring buffer (unscaled time, no allocations) that much in the past; a press is delivered exactly
    /// once. Restart is never delayed.
    /// The buffer is sized by time, not by frames: frames closer than <see cref="MinSampleInterval"/> are merged into one
    /// sample (presses OR-ed, latest move/aim/held kept), so at any frame rate it holds over a second of input; if it
    /// ever fills, the oldest sample is delivered early instead of being overwritten, so no press is lost.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        private const int BufferSize = 1024;
        private const float MinSampleInterval = 0.001f; // at most 1000 samples/s: 1024 samples cover > 1 s of delay
        private const int RebaseThreshold = BufferSize * 1024 * 1024;
        private const byte RocketBit = 1;
        private const byte MeleeBit = 2;
        private const byte DodgeBit = 4;
        private const byte RejectBit = 8;

        private struct InputSample
        {
            public float Time;
            public Vector2 Move;
            public Vector2 Aim;
            public bool FireHeld;
            public bool EjectHeld;
            public byte Pressed;
        }

        private InputAction _move;
        private InputAction _fire;
        private InputAction _rocket;
        private InputAction _melee;
        private InputAction _dodge;
        private InputAction _reject;
        private InputAction _eject;
        private InputAction _restart;
        private Vector2 _lastAimScreenPosition;

        private readonly InputSample[] _buffer = new InputSample[BufferSize];
        private int _written;   // samples written so far (serial)
        private int _consumed;  // serial of the next sample not yet delivered
        private InputSample _delayed;
        private byte _delayedPressed;
        private byte _carriedPressed; // presses of samples delivered early because the buffer was full
        private float _inputDelay;

        /// <summary>Seconds of input delay (default 0; Contract 6 sets 0.25). Min 0.</summary>
        public float InputDelay
        {
            get => _inputDelay;
            set => _inputDelay = Mathf.Max(0f, value);
        }

        private bool Delayed => _inputDelay > 0f;

        /// <summary>WASD / arrows, -1..1 per axis (diagonals normalised by the composite).</summary>
        public Vector2 MoveInput => Delayed ? _delayed.Move : LiveMove;
        public bool FireHeld => Delayed ? _delayed.FireHeld : LiveFireHeld;
        public bool RocketPressed => Delayed ? (_delayedPressed & RocketBit) != 0 : WasPressed(_rocket);
        public bool MeleePressed => Delayed ? (_delayedPressed & MeleeBit) != 0 : WasPressed(_melee);
        public bool DodgePressed => Delayed ? (_delayedPressed & DodgeBit) != 0 : WasPressed(_dodge);
        public bool RejectPressed => Delayed ? (_delayedPressed & RejectBit) != 0 : WasPressed(_reject);
        public bool EjectHeld => Delayed ? _delayed.EjectHeld : LiveEjectHeld;
        public bool RestartPressed => WasPressed(_restart);
        /// <summary>Mouse position in screen pixels (bottom-left origin). Last known value if there is no mouse.</summary>
        public Vector2 AimScreenPosition => Delayed ? _delayed.Aim : LiveAim;
        public bool HasMouse => Mouse.current != null;

        private Vector2 LiveMove => _move != null ? _move.ReadValue<Vector2>() : Vector2.zero;
        private bool LiveFireHeld => _fire != null && _fire.IsPressed();
        private bool LiveEjectHeld => _eject != null && _eject.IsPressed();
        private Vector2 LiveAim
        {
            get
            {
                Mouse mouse = Mouse.current;
                if (mouse != null) _lastAimScreenPosition = mouse.position.ReadValue();
                return _lastAimScreenPosition;
            }
        }

        private static bool WasPressed(InputAction action) => action != null && action.WasPressedThisFrame();

        private void Awake()
        {
            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            _fire = new InputAction("Fire", InputActionType.Button, "<Mouse>/leftButton");

            _rocket = new InputAction("Rocket", InputActionType.Button, "<Keyboard>/e");
            _rocket.AddBinding("<Mouse>/middleButton");

            _melee = new InputAction("Melee", InputActionType.Button, "<Keyboard>/f");
            _dodge = new InputAction("Dodge", InputActionType.Button, "<Keyboard>/space");
            _reject = new InputAction("Reject", InputActionType.Button, "<Mouse>/rightButton");
            _eject = new InputAction("EjectModifier", InputActionType.Button, "<Keyboard>/q");
            _restart = new InputAction("Restart", InputActionType.Button, "<Keyboard>/r");
        }

        private void OnEnable() => SetEnabled(true);

        private void OnDisable() => SetEnabled(false);

        private void OnDestroy()
        {
            _move?.Dispose();
            _fire?.Dispose();
            _rocket?.Dispose();
            _melee?.Dispose();
            _dodge?.Dispose();
            _reject?.Dispose();
            _eject?.Dispose();
            _restart?.Dispose();
        }

        private void Update()
        {
            // Always record, so turning the delay on mid-run has a history to read from.
            float now = Time.unscaledTime;
            var sample = new InputSample
            {
                Time = now,
                Move = LiveMove,
                Aim = LiveAim,
                FireHeld = LiveFireHeld,
                EjectHeld = LiveEjectHeld,
                Pressed = (byte)((WasPressed(_rocket) ? RocketBit : 0) | (WasPressed(_melee) ? MeleeBit : 0) |
                                 (WasPressed(_dodge) ? DodgeBit : 0) | (WasPressed(_reject) ? RejectBit : 0)),
            };

            bool merge = _written > _consumed && now - _buffer[(_written - 1) % BufferSize].Time < MinSampleInterval;
            if (merge)
            {
                // Very high frame rate: fold this frame into the newest undelivered sample (keeps its time stamp).
                ref InputSample newest = ref _buffer[(_written - 1) % BufferSize];
                newest.Move = sample.Move;
                newest.Aim = sample.Aim;
                newest.FireHeld = sample.FireHeld;
                newest.EjectHeld = sample.EjectHeld;
                newest.Pressed |= sample.Pressed;
            }
            else
            {
                int slot = _written % BufferSize;
                if (_written - _consumed >= BufferSize)
                {
                    // Full (only with a delay over ~1 s): deliver the oldest early rather than lose its presses.
                    _delayed = _buffer[slot];
                    _carriedPressed |= _buffer[slot].Pressed;
                    _consumed++;
                }
                _buffer[slot] = sample;
                _written++;
                if (_written >= RebaseThreshold)
                {
                    // Keep the serials far from int overflow; a multiple of BufferSize keeps every slot in place.
                    _written -= RebaseThreshold;
                    _consumed -= RebaseThreshold;
                }
            }

            if (!Delayed)
            {
                // Live mode: nothing old is replayed when a delay is switched on later.
                _consumed = _written;
                _delayed = sample;
                _delayed.Pressed = 0;
                _delayedPressed = 0;
                _carriedPressed = 0;
                return;
            }

            float cutoff = now - _inputDelay;
            byte pressed = _carriedPressed;
            _carriedPressed = 0;
            while (_consumed < _written)
            {
                InputSample next = _buffer[_consumed % BufferSize];
                if (next.Time > cutoff) break;
                _delayed = next;
                pressed |= next.Pressed;
                _consumed++;
            }
            _delayedPressed = pressed;
        }

        private void SetEnabled(bool value)
        {
            if (_move == null) return;
            Toggle(_move, value);
            Toggle(_fire, value);
            Toggle(_rocket, value);
            Toggle(_melee, value);
            Toggle(_dodge, value);
            Toggle(_reject, value);
            Toggle(_eject, value);
            Toggle(_restart, value);
        }

        private static void Toggle(InputAction action, bool value)
        {
            if (value) action.Enable();
            else action.Disable();
        }
    }
}
