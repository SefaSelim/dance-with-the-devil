// ImpactEvent.cs
// One event drives every feedback channel on the same frame (VFX, decal, light, shake, hitstop,
// rumble, audio, UI). Raise it once from the hit/fire code; subscribers react in the same call.

using System;
using UnityEngine;

public enum SurfaceType { Default, Metal, Stone, Wood, Dirt, Water, Glass, Flesh }

public enum ImpactKind { Bullet, Melee, Explosion, MuzzleFire, Land }

public struct ImpactEvent
{
    public ImpactKind kind;
    public Vector3 position;
    public Vector3 normal;      // surface normal at the hit (or fire direction for MuzzleFire)
    public Vector3 direction;   // incoming travel direction of the projectile/blade
    [Range(0f, 1f)] public float intensity; // weight 0..1 — scales size, shake, hitstop, rumble
    public float radius;        // Explosion: blast radius in metres for the ring and burst size (0 = feedback default)
    public SurfaceType surface;
    public Transform hitTransform;  // for parenting decals to movers / target flash
    public GameObject instigator;
    public bool instigatorIsLocalPlayer;
    public bool isCrit;         // the hit was a critical hit (Bullet/Melee per target; Explosion: any target critted)
}

public static class ImpactBus
{
    public static event Action<ImpactEvent> OnImpact;

    public static void Raise(ImpactEvent e) => OnImpact?.Invoke(e);

    /// <summary>Helper for raycast weapons.</summary>
    public static ImpactEvent FromRaycast(RaycastHit hit, Vector3 rayDirection, float intensity, SurfaceType surface, ImpactKind kind = ImpactKind.Bullet)
    {
        return new ImpactEvent
        {
            kind = kind,
            position = hit.point,
            normal = hit.normal,
            direction = rayDirection.normalized,
            intensity = intensity,
            surface = surface,
            hitTransform = hit.collider != null ? hit.collider.transform : null,
        };
    }
}
