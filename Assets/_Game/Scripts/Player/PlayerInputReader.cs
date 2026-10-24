using UnityEngine;
using UnityEngine.InputSystem;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Player input built in code (no .inputactions asset). Bindings follow GDD §4 (Kontroller).
    /// Reject (right mouse) and Eject modifier (Q) are exposed for the module system but not used yet.
    /// Actions are enabled/disabled with the component. "Pressed" properties are true only on the press frame.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        private InputAction _move;
        private InputAction _fire;
        private InputAction _rocket;
        private InputAction _melee;
        private InputAction _dodge;
        private InputAction _reject;
        private InputAction _eject;
        private InputAction _restart;
        private Vector2 _lastAimScreenPosition;

        /// <summary>WASD / arrows, -1..1 per axis (diagonals normalised by the composite).</summary>
        public Vector2 MoveInput => _move != null ? _move.ReadValue<Vector2>() : Vector2.zero;
        public bool FireHeld => _fire != null && _fire.IsPressed();
        public bool RocketPressed => _rocket != null && _rocket.WasPressedThisFrame();
        public bool MeleePressed => _melee != null && _melee.WasPressedThisFrame();
        public bool DodgePressed => _dodge != null && _dodge.WasPressedThisFrame();
        public bool RejectPressed => _reject != null && _reject.WasPressedThisFrame();
        public bool EjectHeld => _eject != null && _eject.IsPressed();
        public bool RestartPressed => _restart != null && _restart.WasPressedThisFrame();
        /// <summary>Mouse position in screen pixels (bottom-left origin). Last known value if there is no mouse.</summary>
        public Vector2 AimScreenPosition
        {
            get
            {
                Mouse mouse = Mouse.current;
                if (mouse != null) _lastAimScreenPosition = mouse.position.ReadValue();
                return _lastAimScreenPosition;
            }
        }
        public bool HasMouse => Mouse.current != null;

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
