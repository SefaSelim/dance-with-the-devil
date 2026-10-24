using UnityEngine;

namespace DanceWithTheDevil
{
    /// <summary>
    /// Shared shot geometry for the player's guns (GDD §4: shots go to the cursor point).
    /// <see cref="ShotDirection"/>: the flat direction from the muzzle to the aim point, so shots converge on the cursor
    /// instead of flying parallel to the body-to-cursor line from a muzzle held off to the side.
    /// <see cref="ClearSpawnPoint"/>: pulls the spawn point back along the body-to-muzzle line until a projectile of the
    /// given radius fits, so a wall right beside the gun neither eats the shot nor bounces it back into the shooter.
    /// Allocation free (one shared hit buffer, main thread only).
    /// </summary>
    public static class WeaponAim
    {
        private const int MaxHits = 16;
        private const float Skin = 0.02f;
        private const float PointBlankMargin = 0.3f;
        private static readonly RaycastHit[] Hits = new RaycastHit[MaxHits];

        /// <summary>
        /// Origin and flat direction of a shot toward <paramref name="aimPoint"/>. Normally: from
        /// <paramref name="muzzle"/> along <see cref="ShotDirection"/>. Point-blank (the aim point is no farther from the
        /// body than the muzzle is, plus a small margin, so the target may sit beside or behind the gun): from the body
        /// axis at muzzle height along <paramref name="aimDirection"/>, so the target is still hit.
        /// </summary>
        public static void ResolveShot(Vector3 bodyPosition, Vector3 muzzle, Vector3 aimPoint, Vector3 aimDirection,
            float minConvergeDistance, float maxConvergeAngle, out Vector3 origin, out Vector3 direction)
        {
            var bodyAxis = new Vector3(bodyPosition.x, muzzle.y, bodyPosition.z);
            float muzzleDistance = FlatDistance(bodyAxis, muzzle);
            if (FlatDistance(bodyAxis, aimPoint) < muzzleDistance + PointBlankMargin)
            {
                origin = bodyAxis;
                aimDirection.y = 0f;
                direction = aimDirection.sqrMagnitude > 1e-6f ? aimDirection.normalized : Vector3.forward;
                return;
            }
            origin = muzzle;
            direction = ShotDirection(muzzle, aimPoint, aimDirection, minConvergeDistance, maxConvergeAngle);
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x;
            float z = a.z - b.z;
            return Mathf.Sqrt(x * x + z * z);
        }

        /// <summary>
        /// Flat (y = 0) normalised direction from <paramref name="origin"/> toward <paramref name="aimPoint"/>.
        /// Returns the flattened <paramref name="fallback"/> when the aim point is closer than
        /// <paramref name="minDistance"/> (flat) or more than <paramref name="maxAngle"/> degrees away from it
        /// (cursor beside or behind the muzzle).
        /// </summary>
        public static Vector3 ShotDirection(Vector3 origin, Vector3 aimPoint, Vector3 fallback, float minDistance,
            float maxAngle)
        {
            fallback.y = 0f;
            fallback = fallback.sqrMagnitude > 1e-6f ? fallback.normalized : Vector3.forward;

            Vector3 toAim = aimPoint - origin;
            toAim.y = 0f;
            float sqr = toAim.sqrMagnitude;
            if (sqr < 1e-6f || sqr < minDistance * minDistance) return fallback;

            Vector3 direction = toAim / Mathf.Sqrt(sqr);
            return Vector3.Angle(direction, fallback) > maxAngle ? fallback : direction;
        }

        /// <summary>
        /// The point nearest to <paramref name="muzzle"/> on the horizontal line from the body axis (the vertical line
        /// through <paramref name="bodyPosition"/>, at the muzzle's height) to the muzzle where a sphere of
        /// <paramref name="radius"/> touches no wall. Colliders under <paramref name="ignoreRoot"/> and damageables
        /// (point-blank enemies are still hit) do not block.
        /// </summary>
        public static Vector3 ClearSpawnPoint(Vector3 bodyPosition, Vector3 muzzle, float radius, LayerMask mask,
            Transform ignoreRoot)
        {
            var start = new Vector3(bodyPosition.x, muzzle.y, bodyPosition.z);
            Vector3 delta = muzzle - start;
            float distance = delta.magnitude;
            if (distance < 1e-4f) return muzzle;

            Vector3 direction = delta / distance;
            int count = Physics.SphereCastNonAlloc(start, radius, direction, Hits, distance, mask,
                QueryTriggerInteraction.Ignore);
            float clear = distance;
            for (int i = 0; i < count; i++)
            {
                Collider collider = Hits[i].collider;
                if (collider == null) continue;
                if (ignoreRoot != null && collider.transform.IsChildOf(ignoreRoot)) continue;
                if (collider.GetComponentInParent<IDamageable>() != null) continue;

                float hitDistance = Hits[i].distance;
                if (hitDistance <= 0f) return start; // already touching at the body axis: start there
                clear = Mathf.Min(clear, hitDistance - Skin);
            }
            return start + direction * Mathf.Max(0f, clear);
        }
    }
}
