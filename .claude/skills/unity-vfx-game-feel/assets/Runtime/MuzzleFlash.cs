// MuzzleFlash.cs
// Fires an attached, multi-layer muzzle effect deterministically every shot.
// Why Emit() instead of Play(): calling Play() on a non-looping system that is still alive does
// nothing, so rapid fire drops flashes; Stop+Clear+Play would delete the previous shots' smoke.
// So each layer system is set to loop with its Emission module DISABLED (it never emits by itself)
// and this script emits exact particle counts per shot. Existing smoke keeps drifting in world space.
//
// The builder (MuzzleFlashBuilderExample.cs) creates a prefab already wired to this component.

using UnityEngine;

public class MuzzleFlash : MonoBehaviour
{
    [System.Serializable]
    public struct Layer
    {
        public ParticleSystem system;
        public Vector2Int countRange;            // inclusive
        [Range(0f, 1f)] public float chance;     // probability this layer fires on a shot
        public bool scaleWithBigFlash;
    }

    public Layer[] layers;
    public LightFlash lightFlash;
    [Tooltip("Occasional bigger flash breaks repetition without changing the read.")]
    [Range(0f, 1f)] public float bigFlashChance = 0.15f;
    public float bigFlashScale = 1.35f;

    public void Fire(float intensity = 1f)
    {
        bool big = Random.value < bigFlashChance;
        float sizeScale = (big ? bigFlashScale : 1f) * Mathf.Lerp(0.85f, 1.15f, intensity);

        if (layers != null)
        {
            foreach (var l in layers)
            {
                if (l.system == null || Random.value > l.chance) continue;
                int count = Random.Range(l.countRange.x, l.countRange.y + 1);
                if (count <= 0) continue;
                if (!l.system.isPlaying) l.system.Play(false);

                if (l.scaleWithBigFlash && !Mathf.Approximately(sizeScale, 1f))
                    EmitScaled(l.system, count, sizeScale);
                else
                    l.system.Emit(count);
            }
        }

        if (lightFlash) lightFlash.Flash((big ? 1.3f : 1f) * intensity);
    }

    static void EmitScaled(ParticleSystem ps, int count, float scale)
    {
        var main = ps.main;
        for (int i = 0; i < count; i++)
        {
            var ep = new ParticleSystem.EmitParams();
            if (main.startSize3D)
            {
                ep.startSize3D = new Vector3(
                    Sample(main.startSizeX) * scale,
                    Sample(main.startSizeY) * scale,
                    Sample(main.startSizeZ) * scale);
            }
            else
            {
                ep.startSize = Sample(main.startSize) * scale;
            }
            ps.Emit(ep, 1);
        }
    }

    static float Sample(ParticleSystem.MinMaxCurve c)
    {
        switch (c.mode)
        {
            case ParticleSystemCurveMode.Constant: return c.constant;
            case ParticleSystemCurveMode.TwoConstants: return Random.Range(c.constantMin, c.constantMax);
            default: return c.Evaluate(0f, Random.value);
        }
    }
}
