// VFX_ParticleUnlit.shader
// One particle/trail shader for Built-in and URP (the pass has no LightMode tag, so URP draws it
// through its SRPDefaultUnlit path). Not for HDRP — use a Shader Graph equivalent there.
//
// Features
//  - Premultiplied "alpha-additive" blending: _Additive 0 = alpha blend (smoke), 1 = additive (glow).
//    One blend state, so no fragile per-material blend keywords.
//  - HDR tint * intensity (needs HDR camera + Bloom to glow).
//  - Erosion (dissolve by alpha/height) from the particle custom vertex stream Custom1.x.
//  - Per-particle random seed from StableRandom.x to decorrelate noise.
//  - Optional UV distortion noise and UV scrolling (trails, energy).
//  - Optional soft particles (needs the camera depth texture; in URP enable "Depth Texture").
//
// Expected ParticleSystemRenderer vertex streams (in this order):
//   Position, Color, UV (TEXCOORD0.xy), Custom1.x (TEXCOORD0.z), StableRandom.x (TEXCOORD0.w)
// Missing streams are safe: erosion defaults to 0. Plain meshes (e.g. BladeTrail) work too.

Shader "VFX/ParticleUnlit"
{
    Properties
    {
        _MainTex ("Texture (RGB colour, A mask / height)", 2D) = "white" {}
        [HDR] _TintColor ("Tint (HDR)", Color) = (1,1,1,1)
        _Intensity ("Intensity", Float) = 1
        _Additive ("Additive (0 = alpha, 1 = additive)", Range(0,1)) = 1
        _ErosionSoftness ("Erosion Softness", Range(0.01,1)) = 0.25
        _Scroll ("Main UV Scroll (xy)", Vector) = (0,0,0,0)
        _NoiseTex ("Noise (R)", 2D) = "gray" {}
        _NoiseStrength ("Noise UV Distortion", Range(0,0.5)) = 0
        _NoiseErosion ("Noise Breaks Up Erosion Edge", Range(0,1)) = 0.35
        _NoiseScroll ("Noise UV Scroll (xy)", Vector) = (0,0,0,0)
        [Toggle(_SOFTPARTICLES_ON)] _SoftParticles ("Soft Particles", Float) = 0
        _SoftFade ("Soft Fade Distance (m)", Range(0.01,3)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull [_Cull]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _SOFTPARTICLES_ON
            #include "UnityCG.cginc"

            sampler2D _MainTex;  float4 _MainTex_ST;
            sampler2D _NoiseTex; float4 _NoiseTex_ST;
            float4 _TintColor;
            float  _Intensity;
            float  _Additive;
            float  _ErosionSoftness;
            float4 _Scroll;
            float  _NoiseStrength;
            float  _NoiseErosion;
            float4 _NoiseScroll;
            float  _SoftFade;
            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float4 uv     : TEXCOORD0; // xy uv, z erosion, w random
            };

            struct v2f
            {
                float4 pos     : SV_POSITION;
                float4 color   : COLOR;
                float2 uv      : TEXCOORD0;
                float2 noiseUV : TEXCOORD1;
                float  erosion : TEXCOORD2;
            #ifdef _SOFTPARTICLES_ON
                float4 projPos : TEXCOORD3;
            #endif
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                float seed = v.uv.w;
                o.uv = TRANSFORM_TEX(v.uv.xy, _MainTex) + _Scroll.xy * _Time.y;
                o.noiseUV = TRANSFORM_TEX(v.uv.xy, _NoiseTex) + _NoiseScroll.xy * _Time.y + seed * 17.13;
                o.erosion = v.uv.z;
            #ifdef _SOFTPARTICLES_ON
                o.projPos = ComputeScreenPos(o.pos);
                COMPUTE_EYEDEPTH(o.projPos.z);
            #endif
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float n = tex2D(_NoiseTex, i.noiseUV).r;
                float2 uv = i.uv + (n - 0.5) * _NoiseStrength;
                float4 tex = tex2D(_MainTex, uv);

                // Height used for erosion: texture alpha, optionally broken up by noise.
                float height = tex.a * lerp(1.0, saturate(n * 1.6), _NoiseErosion * step(0.001, i.erosion));
                // erosion 0 = untouched, 1 = fully eaten. Remapped so both ends are exact.
                float e = i.erosion * (1.0 + _ErosionSoftness);
                float erodeMask = smoothstep(e - _ErosionSoftness, e, height);
                float a = tex.a * erodeMask * i.color.a * _TintColor.a;

                float3 rgb = tex.rgb * i.color.rgb * _TintColor.rgb * _Intensity;

            #ifdef _SOFTPARTICLES_ON
                float sceneZ = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.projPos)));
                a *= saturate((sceneZ - i.projPos.z) / _SoftFade);
            #endif

                // Premultiplied output. Additive = keep colour, contribute no coverage.
                return float4(rgb * a, a * (1.0 - _Additive));
            }
            ENDCG
        }
    }
    Fallback Off
}
