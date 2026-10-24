// Full-screen display grade for the pixel camera's RawImage (PixelCameraRig.DisplayMaterial).
// UI/Default structure (stencil, RectMask2D clip rect, alpha clip, vertex colour) plus:
//   _Saturation 0..1  (1 = original colours, 0 = grey; Devil freeze and Contract 1 "Rengin")
//   _Vignette   0..1  (screen-edge darkening; Contract 5 "Zamanın")
// Overlay canvases draw after URP renders, so the built-in UnityCG/UnityUI includes are used, as in UI/Default.
// Author: ui-programmer agent (Phase 5A). Pipeline: URP 17, Screen Space Overlay canvas. Target: desktop.
// Screen bending (DisplayGrade.cs, technical-artist 2026-10-25): up to 4 shockwave rings (_Waves: centre in display
// 0..1, radius and width in image-height units, aspect-correct; _WaveStrength.x: displacement) and the rig's roll and
// zoom kick (_Kick: cos/zoom, sin/zoom, aspect). The warped UV is clamped to the visible window (_UvRect), so the
// RT border never shows. Must match PixelCameraRig.DisplayToSource.
// Budget: one full-screen quad, one texture sample, ~70 ALU (4 rings unrolled). Variants: UNITY_UI_CLIP_RECT x UNITY_UI_ALPHACLIP (4).
Shader "DanceWithTheDevil/UI/DisplayGrade"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Grade)]
        _Saturation ("Saturation", Range(0, 1)) = 1
        _Vignette ("Vignette", Range(0, 1)) = 0
        _VignetteColor ("Vignette Colour", Color) = (0,0,0,1)
        _VignetteInner ("Vignette Inner Radius", Range(0, 1)) = 0.3
        _VignetteOuter ("Vignette Outer Radius", Range(0, 1.5)) = 0.8

        [Header(Screen warp)]
        _UvRect ("Visible UV Rect (xy offset, zw size)", Vector) = (0, 0, 1, 1)
        _Kick ("Roll Zoom (cos/zoom, sin/zoom, aspect)", Vector) = (1, 0, 1.7777778, 0)
        _WaveGlow ("Shockwave Edge Glow", Range(0, 20)) = 1.2

        [Header(UI Stencil and Mask)]
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            half _Saturation;
            half _Vignette;
            fixed4 _VignetteColor;
            half _VignetteInner;
            half _VignetteOuter;

            float4 _UvRect;
            float4 _Kick;
            float4 _Waves[4];        // xy centre (display 0..1), z radius, w width (image-height units)
            float4 _WaveStrength[4]; // x displacement (image-height units)
            half _WaveGlow;

            // Stretches the centre distance so the 16:9 corners (|uv-0.5| = 0.707) reach the outer radius.
            static const half VignetteDistanceScale = 1.1;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // Display position inside the visible window (0..1).
                float2 q = (IN.texcoord - _UvRect.xy) / max(_UvRect.zw, 1e-5);
                float aspect = max(_Kick.z, 1e-3);

                // Shockwave rings: a smooth bump around each radius pushes the image outward (samples closer to the
                // centre) and lifts it slightly, so the ring reads as a bending, bright edge.
                float2 offset = 0;
                float glow = 0;
                [unroll] for (int i = 0; i < 4; i++)
                {
                    float2 d = q - _Waves[i].xy;
                    d.x *= aspect;
                    float len = length(d);
                    float x = (len - _Waves[i].z) / max(_Waves[i].w, 1e-4);
                    float b = saturate(1.0 - x * x);
                    b *= b;
                    float s = b * _WaveStrength[i].x;
                    offset += (d / max(len, 1e-4)) * s;
                    glow += s;
                }
                offset.x /= aspect;
                float2 w = q - offset;

                // Roll and zoom about the centre, aspect-correct (PixelCameraRig.DisplayToSource).
                float2 p = w - 0.5;
                p.x *= aspect;
                p = float2(_Kick.x * p.x - _Kick.y * p.y, _Kick.y * p.x + _Kick.x * p.y);
                p.x /= aspect;
                w = saturate(p + 0.5);
                float2 uv = _UvRect.xy + w * _UvRect.zw;

                half4 color = (tex2D(_MainTex, uv) + _TextureSampleAdd) * IN.color;
                color.rgb += (half)(glow * _WaveGlow);

                // Saturation: lerp toward Rec.709 luminance.
                half luminance = dot(color.rgb, half3(0.2126, 0.7152, 0.0722));
                color.rgb = lerp(luminance.xxx, color.rgb, saturate(_Saturation));

                // Vignette over the visible window (screen-fixed: it does not shake or warp).
                float2 centered = q - 0.5;
                half edge = smoothstep(_VignetteInner, _VignetteOuter, length(centered) * VignetteDistanceScale);
                color.rgb = lerp(color.rgb, _VignetteColor.rgb, edge * saturate(_Vignette));

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
        ENDCG
        }
    }
}
