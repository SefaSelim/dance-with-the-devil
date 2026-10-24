// Char_Outline: inverted-hull outline for the cube-part characters (GDD §13 "1 px dış çizgi").
// Author: Dance with the Devil team (Claude Code, art lane), 2026-10-24.
// Target: URP 17 (Unity 6), Forward+ PC renderer, 640x360 orthographic game camera. Budget: one extra draw per outlined
// part, vertex-only work, a constant-colour fragment, no texture samples. Variants: instancing only
// (multi_compile_instancing); no other keywords.
// Used as the SECOND material on a part's MeshRenderer: Unity draws the single cube sub-mesh once per material,
// so the part renders normally with its own material and again with this one as a slightly larger, back-face-only
// hull. Only the ring that sticks out past the part is visible, which reads as a dark 1-texel outline.
//
// The hull is grown along the part's own axes, not along the vertex normals: a cube's normals are split per face, so a
// normal push tears the hull open at every corner. Growing each corner by the same world width on X, Y and Z gives a
// closed box that is `width` larger on every side, whatever the part's (non-uniform) scale.
//
// Width: on an orthographic camera it is given in low-res texels and follows the camera's size, so it stays 1 texel
// when the orthographic size changes. On a perspective camera (Scene view) it falls back to a fixed width in metres.
// No ShadowCaster / DepthNormals pass: the outline never casts shadows and stays out of SSAO.
Shader "Dance/Character Outline"
{
    Properties
    {
        [Header(Colour)]
        _OutlineColor ("Outline Color", Color) = (0.043, 0.035, 0.063, 1)
        [Header(Width)]
        _OutlineTexels ("Width (low-res texels, orthographic camera)", Range(0, 3)) = 1
        _OutlineWidth ("Width (metres, perspective camera)", Range(0, 0.2)) = 0.04
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
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineTexels;
                float _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings OutlineVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);

                float4x4 objectToWorld = GetObjectToWorldMatrix();
                float3 axisX = SafeNormalize(objectToWorld._m00_m10_m20);
                float3 axisY = SafeNormalize(objectToWorld._m01_m11_m21);
                float3 axisZ = SafeNormalize(objectToWorld._m02_m12_m22);

                // World size of one low-res texel: the visible height (2 x ortho size) over the target's pixel height.
                float texelWorld = 2.0 * unity_OrthoParams.y / max(_ScreenParams.y, 1.0);
                float width = lerp(_OutlineWidth, _OutlineTexels * texelWorld, unity_OrthoParams.w);

                float3 corner = sign(input.positionOS.xyz);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS += (axisX * corner.x + axisY * corner.y + axisZ * corner.z) * width;

                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 OutlineFragment(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
