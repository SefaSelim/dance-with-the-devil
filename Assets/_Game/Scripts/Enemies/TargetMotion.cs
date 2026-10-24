using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Smoothed flat velocity of the enemies' target (the player), shared by every enemy brain so they can lead a
    /// moving player (F2). Updated at most once per frame by the first brain that asks. The raw speed is capped, so a
    /// dodge burst (3 m in 0.18 s) does not throw every prediction across the arena, and the smoothing lag
    /// (~0.12 s) is what makes a sudden change of direction a real escape. Runs on scaled time: frozen while
    /// Time.timeScale is 0.
    /// </summary>
    public static class TargetMotion
    {
        private const float SmoothingTime = 0.12f;
        private const float MaxTrackedSpeed = 7f;
        private const float StaleAfter = 0.25f;

        private static Transform _target;
        private static Vector3 _lastPosition;
        private static Vector3 _velocity;
        private static float _lastTime;
        private static int _frame = -1;

        /// <summary>Smoothed flat velocity of <paramref name="target"/> in m/s (zero for a new or idle target).</summary>
        public static Vector3 Velocity(Transform target)
        {
            if (target == null) return Vector3.zero;

            int frame = Time.frameCount;
            if (target != _target)
            {
                _target = target;
                _lastPosition = target.position;
                _lastTime = Time.time;
                _velocity = Vector3.zero;
                _frame = frame;
                return _velocity;
            }
            if (frame == _frame) return _velocity;
            _frame = frame;

            float now = Time.time;
            float elapsed = now - _lastTime;
            if (elapsed <= 0f) return _velocity; // time frozen (contract screen, hitstop)

            Vector3 position = target.position;
            Vector3 raw = (position - _lastPosition) / elapsed;
            raw.y = 0f;
            _lastPosition = position;
            _lastTime = now;

            if (elapsed > StaleAfter)
            {
                // Nobody asked for a while (no enemies alive): start over instead of averaging a long jump.
                _velocity = Vector3.zero;
                return _velocity;
            }

            raw = Vector3.ClampMagnitude(raw, MaxTrackedSpeed);
            float blend = 1f - Mathf.Exp(-elapsed / SmoothingTime);
            _velocity = Vector3.Lerp(_velocity, raw, blend);
            return _velocity;
        }

        /// <summary>
        /// Where <paramref name="target"/> will be after <paramref name="seconds"/> if it keeps its current velocity,
        /// with the lead scaled by <paramref name="share"/> (0 = no lead, 1 = full) and at most
        /// <paramref name="maxDistance"/> metres ahead (flat).
        /// </summary>
        public static Vector3 Predict(Transform target, float seconds, float share, float maxDistance)
        {
            if (target == null) return Vector3.zero;
            Vector3 lead = Velocity(target) * (Mathf.Max(0f, seconds) * Mathf.Max(0f, share));
            return target.position + Vector3.ClampMagnitude(lead, Mathf.Max(0f, maxDistance));
        }
    }
}
