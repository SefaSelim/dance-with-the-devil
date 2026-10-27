#ifndef DANCE_OCCLUSION_CUT_INCLUDED
#define DANCE_OCCLUSION_CUT_INCLUDED

// Circle cut-out around Defne for "Dance/Lit Occlusion" (a copy of URP Lit). OcclusionFader swaps the scenery that
// covers her to this shader and sets the globals each frame:
//   _OcclusionTarget.xyz  Defne's chest, world space
//   _OcclusionCircle.x    radius as a fraction of the screen height (0 = no hole)
//   _OcclusionCircle.y    feather width, same unit (the dithered rim)
// Inside the circle the fragment is clipped with a 4×4 ordered dither (opaque, depth-correct, pixel-art friendly),
// in every pass that writes colour or depth, so nothing behind the hole is hidden by a depth prepass.

float4 _OcclusionTarget;
float4 _OcclusionCircle;

static const float DanceBayer4[16] =
{
     0.0,  8.0,  2.0, 10.0,
    12.0,  4.0, 14.0,  6.0,
     3.0, 11.0,  1.0,  9.0,
    15.0,  7.0, 13.0,  5.0
};

void OcclusionClip(float4 positionCS)
{
    float radius = _OcclusionCircle.x;
    if (radius <= 0.0001) return;

    float2 fragmentUV = GetNormalizedScreenSpaceUV(positionCS);
    float4 targetCS = TransformWorldToHClip(_OcclusionTarget.xyz);
    float4 targetScreen = ComputeScreenPos(targetCS);
    float2 targetUV = targetScreen.xy / max(targetScreen.w, 1e-5);

    float2 delta = fragmentUV - targetUV;
    delta.x *= _ScaledScreenParams.x / _ScaledScreenParams.y; // circle, not ellipse
    float feather = max(_OcclusionCircle.y, 1e-4);
    float keep = saturate((length(delta) - (radius - feather)) / feather); // 0 inside .. 1 outside

    uint2 pixel = uint2(positionCS.xy) & 3u;
    float threshold = (DanceBayer4[pixel.y * 4u + pixel.x] + 0.5) / 16.0;
    clip(keep - threshold);
}

#endif
