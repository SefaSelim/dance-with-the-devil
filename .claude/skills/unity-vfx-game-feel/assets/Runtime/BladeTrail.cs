// BladeTrail.cs
// Smooth weapon/blade trail built as a custom mesh. Replaces TrailRenderer for fast swings:
// TrailRenderer adds one segment per frame, so a 150-degree swing over 4 frames looks polygonal.
// This samples the blade base and tip every frame and subdivides between samples with a
// Catmull-Rom spline, so the arc stays smooth at any frame rate.
//
// Mesh layout: U = age (0 at the blade, 1 at the oldest point), V = base (0) -> tip (1).
// Use with VFX_ParticleUnlit (Cull Off) and a trail texture whose alpha fades along U;
// erode or scroll for a carved, energetic look. Colour/alpha over age comes from the gradient.
//
// Usage: add to the weapon, assign base and tip transforms, call StartTrail()/StopTrail() from
// animation events (relay from the Animator's GameObject if the weapon is a child).
// The mesh lives on its own unparented GameObject in world space.
// Runs on scaled time, so it freezes during hitstop — which reads as impact.

using System.Collections.Generic;
using UnityEngine;

public class BladeTrail : MonoBehaviour
{
    public Transform basePoint;
    public Transform tipPoint;
    public Material material;

    [Tooltip("Seconds a point stays alive. 0.08-0.2 for melee.")]
    public float trailTime = 0.14f;
    [Tooltip("Interpolated points between two frame samples. 4-8 is smooth for very fast swings.")]
    [Range(1, 16)] public int subdivisions = 6;
    [Tooltip("Max raw samples kept (frames).")]
    public int maxSamples = 40;
    [Tooltip("Colour and alpha over age (0 = at the blade). HDR via the material tint.")]
    public Gradient colorOverAge;
    [Tooltip("Fraction of blade length kept over age, collapsing toward the tip. 1 = constant width (use erosion instead).")]
    public AnimationCurve widthOverAge = AnimationCurve.Linear(0f, 1f, 1f, 0.6f);
    public bool emitOnStart = false;

    struct Sample { public Vector3 b, t; public float time; }

    readonly List<Sample> samples = new List<Sample>();
    readonly List<Vector3> verts = new List<Vector3>();
    readonly List<Color> colors = new List<Color>();
    readonly List<Vector2> uvs = new List<Vector2>();
    readonly List<int> tris = new List<int>();

    Mesh mesh;
    GameObject meshGO;
    bool emitting;

    void Awake()
    {
        if (colorOverAge == null || colorOverAge.colorKeys.Length == 0)
        {
            colorOverAge = new Gradient();
            colorOverAge.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.3f), new GradientAlphaKey(0f, 1f) });
        }
        mesh = new Mesh { name = "BladeTrailMesh" };
        mesh.MarkDynamic();
        meshGO = new GameObject(name + "_TrailMesh");
        meshGO.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = meshGO.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        emitting = emitOnStart;
    }

    void OnDestroy()
    {
        if (meshGO) Destroy(meshGO);
        if (mesh) Destroy(mesh);
    }

    public void StartTrail()
    {
        samples.Clear(); // never connect the previous swing to this one
        emitting = true;
        AddSample();
    }

    public void StopTrail() { emitting = false; }

    void AddSample()
    {
        if (!basePoint || !tipPoint) return;
        samples.Add(new Sample { b = basePoint.position, t = tipPoint.position, time = Time.time });
        while (samples.Count > maxSamples) samples.RemoveAt(0);
    }

    void LateUpdate()
    {
        if (emitting) AddSample();

        float now = Time.time;
        while (samples.Count > 0 && now - samples[0].time > trailTime) samples.RemoveAt(0);

        BuildMesh(now);
    }

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    void BuildMesh(float now)
    {
        verts.Clear(); colors.Clear(); uvs.Clear(); tris.Clear();
        mesh.Clear();
        int n = samples.Count;
        if (n < 2) return;

        // Walk from newest to oldest so U = 0 is at the blade.
        for (int i = n - 1; i >= 1; i--)
        {
            Sample s1 = samples[i], s0 = samples[i - 1];
            Sample sPrev = samples[Mathf.Min(i + 1, n - 1)];
            Sample sNext = samples[Mathf.Max(i - 2, 0)];
            int steps = (i == 1) ? subdivisions + 1 : subdivisions; // include the last point once
            for (int k = 0; k < steps; k++)
            {
                float t = k / (float)subdivisions;
                Vector3 b = CatmullRom(sPrev.b, s1.b, s0.b, sNext.b, t);
                Vector3 tp = CatmullRom(sPrev.t, s1.t, s0.t, sNext.t, t);
                float time = Mathf.Lerp(s1.time, s0.time, t);
                float age = Mathf.Clamp01((now - time) / Mathf.Max(0.0001f, trailTime));

                Vector3 bw = Vector3.Lerp(tp, b, widthOverAge.Evaluate(age));
                Color c = colorOverAge.Evaluate(age);

                verts.Add(bw); colors.Add(c); uvs.Add(new Vector2(age, 0f));
                verts.Add(tp); colors.Add(c); uvs.Add(new Vector2(age, 1f));
            }
        }

        int pairs = verts.Count / 2;
        for (int p = 0; p < pairs - 1; p++)
        {
            int a = p * 2, b = a + 1, c = a + 2, d = a + 3;
            tris.Add(a); tris.Add(b); tris.Add(c);
            tris.Add(b); tris.Add(d); tris.Add(c);
        }

        mesh.SetVertices(verts);
        mesh.SetColors(colors);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
    }
}
