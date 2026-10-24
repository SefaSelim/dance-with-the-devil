using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// A flat wedge of cubes on the floor that flashes for a moment (audit F14: the reject pulse's 60° / 3 m cone
    /// becomes visible, hit or whiff). Arc of cubes at the reach plus the two edge lines; it snaps out to the reach,
    /// holds, then shrinks in hard steps. Plain class: the owner calls <see cref="Tick"/> every frame (unscaled).
    /// </summary>
    public sealed class ConeFlash
    {
        private const int ArcCapacity = 14;
        private const int EdgeBoxes = 4;
        private const float GrowTime = 0.05f;
        private const float HoldTime = 0.09f;
        private const float FadeTime = 0.09f;
        private const float Thickness = 0.16f;
        private const float Height = 0.08f;

        private readonly BoxMeshBatch _batch;
        private Vector3 _origin;
        private Vector3 _direction;
        private float _halfAngle;
        private float _range;
        private float _age;
        private bool _active;

        public ConeFlash(string name, Material material)
        {
            _batch = new BoxMeshBatch(name, material, ArcCapacity + 2 * EdgeBoxes);
        }

        public bool IsActive => _active;

        /// <summary>
        /// Shows the wedge from the floor point <paramref name="origin"/> along the flat <paramref name="direction"/>:
        /// full angle <paramref name="angleDegrees"/>, reach <paramref name="range"/> metres.
        /// </summary>
        public void Play(Vector3 origin, Vector3 direction, float angleDegrees, float range, Color color)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-6f) return;
            _origin = origin + Vector3.up * 0.04f;
            _direction = direction.normalized;
            _halfAngle = Mathf.Clamp(angleDegrees, 1f, 359f) * 0.5f * Mathf.Deg2Rad;
            _range = Mathf.Max(0.3f, range);
            _age = 0f;
            _active = true;
            _batch.SetColor(color);
            Draw(_range * 0.45f, 1f);
        }

        public void Tick(float deltaTime)
        {
            if (!_active) return;
            _age += deltaTime;

            float reach;
            float k;
            if (_age < GrowTime)
            {
                float x = _age / GrowTime;
                reach = Mathf.Lerp(0.45f, 1f, 1f - (1f - x) * (1f - x)) * _range;
                k = 1f;
            }
            else if (_age < GrowTime + HoldTime)
            {
                reach = _range;
                k = 1f;
            }
            else
            {
                float x = (_age - GrowTime - HoldTime) / FadeTime;
                if (x >= 1f)
                {
                    _active = false;
                    _batch.Hide();
                    return;
                }
                reach = _range;
                k = x < 0.5f ? 0.6f : 0.3f; // hard steps, no soft fade (pixel look)
            }
            Draw(reach, k);
        }

        public void Hide()
        {
            _active = false;
            _batch.Hide();
        }

        public void Dispose() => _batch.Destroy();

        private void Draw(float reach, float k)
        {
            _batch.Begin();
            float baseYaw = Mathf.Atan2(_direction.x, _direction.z); // radians, 0 = +z
            float thickness = Thickness * k;
            float height = Height;

            // Arc at the reach.
            float arcLength = 2f * _halfAngle * reach;
            int arcCount = Mathf.Clamp(Mathf.RoundToInt(arcLength / 0.35f), 3, ArcCapacity);
            float segment = arcLength / arcCount * 0.7f;
            for (int i = 0; i < arcCount; i++)
            {
                float a = baseYaw - _halfAngle + (i + 0.5f) * (2f * _halfAngle / arcCount);
                var radial = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                _batch.AddBox(_origin + radial * reach + Vector3.up * (height * 0.5f), radial,
                    new Vector3(segment * Mathf.Lerp(0.5f, 1f, k), height, thickness));
            }

            // The two edge lines, dashed, from near the player to the reach.
            float inner = Mathf.Min(0.6f, reach * 0.3f);
            float step = (reach - inner) / EdgeBoxes;
            for (int side = -1; side <= 1; side += 2)
            {
                float a = baseYaw + side * _halfAngle;
                var radial = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                for (int i = 0; i < EdgeBoxes; i++)
                {
                    float d = inner + (i + 0.5f) * step;
                    _batch.AddBox(_origin + radial * d + Vector3.up * (height * 0.5f), radial,
                        new Vector3(thickness, height, step * 0.6f * Mathf.Lerp(0.5f, 1f, k)));
                }
            }
            _batch.End();
        }
    }
}
