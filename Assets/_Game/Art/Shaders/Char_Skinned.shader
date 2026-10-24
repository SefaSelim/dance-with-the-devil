// Char_Skinned: textured, skinned characters (Defne's imported model) in the flat-lit style of the cube parts.
// Author: Dance with the Devil team (Claude Code), 2026-10-24.
// Target: URP 17 (Unity 6), Forward+ PC renderer. One texture sample, main light (with shadows) + spherical-harmonics
// ambient, two-band wrap so the low-res camera reads clear light and shadow sides.
// Gameplay hooks, set per renderer through a MaterialPropertyBlock (DefneCostVisuals, HurtFeedback):
//   _BaseColor   tint multiplied over the texture; HurtFeedback writes white/red here, so it also lifts toward
//                _BaseColor when _FlashAmount > 0 (a white tint alone would not show on a texture).
//   _FlashAmount 0..1, how far the colour is replaced by _BaseColor (hit flash).
//   _Saturation  1 = full colour, 0 = grey (contract costs 1 and 6).
// ShadowCaster / DepthOnly / DepthNormals come from URP Lit (UsePass), which skins and reads only _BaseMap/_Cutoff.
Shader "Dance/Character Skinned"
{
    Properties
    {
        [Header(Surface)]
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Tint / Flash Color", Color) = (1, 1, 1, 1)
        [Header(Gameplay hooks)]
        _FlashAmount ("Flash Amount", Range(0, 1)) = 0
        _Saturation ("Saturation", Range(0, 1)) = 1
        [Header(Lighting)]
        _ShadowTint ("Shadow Tint", Color) = (0.55, 0.47, 0.62, 1)
        _LightSteps ("Light Bands", Range(1, 4)) = 2
        _AmbientStrength ("Ambient Strength", Range(0, 1)) = 0.35
        _FlashInShadow ("Flash Strength In Shadow", Range(0, 1)) = 0.6
        [Header(Readability)]
        _Brightness ("Brightness", Range(0.5, 3)) = 1.45
        _RimColor ("Rim Color", Color) = (1, 0.82, 0.6, 1)
        _RimStrength ("Rim Strength", Range(0, 2)) = 0.7
        _RimThreshold ("Rim Threshold (1 - N.V)", Range(0, 1)) = 0.55
        [HideInInspector] _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            // Main-light shadow variants as URP Lit uses them (no screen-space shadows: the renderer has none).
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _FlashAmount;
                half _Saturation;
                half4 _ShadowTint;
                half _LightSteps;
                half _AmbientStrength;
                half _FlashInShadow;
                half _Brightness;
                half4 _RimColor;
                half _RimStrength;
                half _RimThreshold;
                half _Cutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;

                // Rec. 601 luma for the grey of the saturation cost.
                half grey = dot(albedo, half3(0.299h, 0.587h, 0.114h));
                albedo = lerp(grey.xxx, albedo, _Saturation);
                albedo = lerp(albedo * _BaseColor.rgb, _BaseColor.rgb, _FlashAmount);

                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                // Half-Lambert wrap, rounded to _LightSteps bands so light and shadow read as flat areas.
                half wrap = saturate(dot(normalWS, mainLight.direction) * 0.5h + 0.5h);
                half steps = max(1.0h, _LightSteps);
                half band = saturate(floor(wrap * steps + 0.5h) / steps);
                half lit = band * mainLight.shadowAttenuation;
                half3 lightColor = lerp(_ShadowTint.rgb, half3(1, 1, 1), lit) * mainLight.color;
                half3 ambient = SampleSH(normalWS) * _AmbientStrength;

                // The model's texture is dark (olive and brown) and she is ~35 px tall on a dark floor: lift it, and add a
                // hard warm rim so her silhouette reads against the floor at the low-res camera.
                half3 color = albedo * (lightColor + ambient) * _Brightness;
                half3 viewWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half rim = step(_RimThreshold, 1.0h - saturate(dot(normalWS, viewWS)));
                color += rim * _RimColor.rgb * _RimStrength * (0.35h + 0.65h * lit);
                color = lerp(color, _BaseColor.rgb, _FlashAmount * _FlashInShadow); // flashes stay readable in shadow
                return half4(color, 1);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }

    FallBack Off
}
