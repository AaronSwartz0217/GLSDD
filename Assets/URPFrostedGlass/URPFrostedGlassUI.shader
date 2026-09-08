Shader "UI/URP Frosted Glass Diffraction"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _TintColor ("Glass Tint", Color) = (0.92, 0.97, 1.0, 0.16)
        _Opacity ("Glass Effect Strength", Range(0, 1)) = 0.72
        _MaskThreshold ("PNG Alpha Mask Threshold", Range(0, 0.5)) = 0.025
        _MaskSoftness ("PNG Alpha Mask Softness", Range(0.001, 0.25)) = 0.03
        _SpriteOverlay ("Baked PNG Overlay", Range(0, 1)) = 0.22
        _CornerRadius ("Corner Radius (Pixels)", Range(0, 256)) = 64
        _Inset ("Edge Inset (Pixels)", Range(0, 16)) = 2
        _BorderWidth ("Border Width (Pixels)", Range(0, 12)) = 1.5
        _BorderColor ("Border Color", Color) = (1, 1, 1, 0.72)
        _Refraction ("Refraction (Pixels)", Range(0, 12)) = 2.5
        _RefractionEdgeWidth ("Refraction Edge Width", Range(0.05, 1)) = 0.8
        _LensStrength ("Lens Refraction Strength", Range(0, 0.35)) = 0.08
        _LensPower ("Lens Superellipse Power", Range(2, 24)) = 8
        _Diffraction ("RGB Diffraction (Pixels)", Range(0, 4)) = 0.8
        _BlurRadius ("Blur Radius (Pixels)", Range(0, 16)) = 4
        _BlurStrength ("Blur Strength", Range(0, 1)) = 0.72
        _Brightness ("Brightness", Range(0.5, 2)) = 1.04
        _Saturation ("Saturation", Range(0, 2)) = 1.06
        _LuminancePreservation ("Luminance Preservation", Range(0, 1)) = 0.85
        _Exposure ("Exposure", Range(0.5, 2)) = 1.10
        _ShadowLift ("Shadow Lift", Range(0, 0.25)) = 0.035
        _TopHighlight ("Top Highlight", Range(0, 1)) = 0.18
        _BottomShade ("Bottom Shade", Range(0, 1)) = 0.08
        [HideInInspector] _RectSize ("Rect Size", Vector) = (600, 240, 0, 0)

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
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
            Name "FrostedGlassUI"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _TintColor;
                half4 _BorderColor;
                float4 _RectSize;
                half _Opacity;
                half _MaskThreshold;
                half _MaskSoftness;
                half _SpriteOverlay;
                half _CornerRadius;
                half _Inset;
                half _BorderWidth;
                half _Refraction;
                half _RefractionEdgeWidth;
                half _LensStrength;
                half _LensPower;
                half _Diffraction;
                half _BlurRadius;
                half _BlurStrength;
                half _Brightness;
                half _Saturation;
                half _LuminancePreservation;
                half _Exposure;
                half _ShadowLift;
                half _TopHighlight;
                half _BottomShade;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            float RoundedBoxSDF(float2 p, float2 halfSize, float radius)
            {
                radius = min(radius, min(halfSize.x, halfSize.y));
                float2 q = abs(p) - (halfSize - radius);
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
            }

            half3 BlurScene(float2 uv, float radiusPixels)
            {
                float2 px = radiusPixels / _ScreenParams.xy;
                half3 c = SampleSceneColor(uv) * 0.20h;
                c += SampleSceneColor(uv + float2( px.x, 0)) * 0.12h;
                c += SampleSceneColor(uv + float2(-px.x, 0)) * 0.12h;
                c += SampleSceneColor(uv + float2(0,  px.y)) * 0.12h;
                c += SampleSceneColor(uv + float2(0, -px.y)) * 0.12h;
                c += SampleSceneColor(uv + px) * 0.08h;
                c += SampleSceneColor(uv - px) * 0.08h;
                c += SampleSceneColor(uv + float2(px.x, -px.y)) * 0.08h;
                c += SampleSceneColor(uv + float2(-px.x, px.y)) * 0.08h;
                return c;
            }

            half3 AdjustSaturation(half3 c, half saturation)
            {
                half luma = dot(c, half3(0.299h, 0.587h, 0.114h));
                return lerp(luma.xxx, c, saturation);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Derive the rendered rectangle size from UV derivatives. This lets
                // differently sized UI Images share one material and still keep a
                // pixel-accurate radius and border width.
                float2 uvFootprint = max(float2(fwidth(input.uv.x), fwidth(input.uv.y)), float2(1e-5, 1e-5));
                float2 rectSize = 1.0 / uvFootprint;
                float2 p = (input.uv - 0.5) * rectSize;
                float2 halfSize = rectSize * 0.5 - _Inset;
                float sdf = RoundedBoxSDF(p, halfSize, _CornerRadius);
                float aa = max(fwidth(sdf), 0.75);
                half inside = 1.0h - smoothstep(-aa, aa, sdf);

                half4 bakedSprite = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                // The baked PNG may intentionally use a very low fill alpha. Remap
                // that alpha into a full-strength effect mask, while retaining the
                // original alpha separately for its border/gloss artwork.
                half pngMask = smoothstep(
                    max(0.0h, _MaskThreshold - _MaskSoftness),
                    _MaskThreshold + _MaskSoftness,
                    bakedSprite.a);
                half effectMask = inside * pngMask;
                clip(effectMask - 0.001h);

                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float depth = saturate(-sdf / max(_CornerRadius, 1.0));
                float2 normalizedBox = abs(p) / max(halfSize, float2(1.0, 1.0));
                float superellipse = pow(saturate(normalizedBox.x), _LensPower)
                                   + pow(saturate(normalizedBox.y), _LensPower);
                float lensEdge = smoothstep(0.05, 1.0, saturate(superellipse));

                // Adapted from the Shadertoy lens formulation: scale the sampled
                // screen UV around this UI rectangle's center. The eighth-power
                // superellipse keeps the lens field rectangular with soft corners.
                float2 panelCenterUV = screenUV - (input.uv - 0.5) * rectSize / _ScreenParams.xy;
                float lensScale = 1.0 - _LensStrength * lensEdge;
                float2 lensUV = panelCenterUV + (screenUV - panelCenterUV) * lensScale;

                float2 radialNormal = normalize(p + float2(1e-4, 1e-4));
                float2 alphaGradient = float2(ddx(bakedSprite.a), ddy(bakedSprite.a));
                float gradientWeight = saturate(dot(abs(alphaGradient), float2(64.0, 64.0)));
                float2 edgeNormal = normalize(-alphaGradient + float2(1e-5, 1e-5));
                float2 refractNormal = normalize(lerp(radialNormal, edgeNormal, gradientWeight));
                float refractionBand = 1.0 - smoothstep(0.02, max(_RefractionEdgeWidth, 0.05), depth);
                float edgeWeight = saturate(refractionBand + gradientWeight);
                float2 refractOffset = refractNormal * (_Refraction * edgeWeight) / _ScreenParams.xy;
                float2 refractedUV = clamp(lensUV + refractOffset, 0.001, 0.999);

                half3 scene = SampleSceneColor(screenUV);
                half3 blurred = BlurScene(refractedUV, _BlurRadius);
                // Perceptual response: low slider values remain useful while the
                // upper half ramps more decisively toward the blurred result.
                half blurInput = saturate(_BlurStrength);
                half blurMix = 1.0h - (1.0h - blurInput) * (1.0h - blurInput);
                half3 glass = lerp(scene, blurred, blurMix);

                // Two extra samples provide restrained chromatic diffraction without
                // repeating the full nine-tap blur for each RGB channel.
                float2 chromaOffset = refractNormal * (_Diffraction * edgeWeight) / _ScreenParams.xy;
                half shiftedR = SampleSceneColor(clamp(refractedUV + chromaOffset, 0.001, 0.999)).r;
                half shiftedB = SampleSceneColor(clamp(refractedUV - chromaOffset, 0.001, 0.999)).b;
                glass.r = lerp(glass.r, shiftedR, saturate(_Diffraction * 0.28h));
                glass.b = lerp(glass.b, shiftedB, saturate(_Diffraction * 0.28h));
                half sourceLuma = dot(scene, half3(0.299h, 0.587h, 0.114h));
                half processedLuma = dot(glass, half3(0.299h, 0.587h, 0.114h));
                half lumaGain = clamp((sourceLuma + 0.02h) / (processedLuma + 0.02h), 0.75h, 1.5h);
                glass *= lerp(1.0h, lumaGain, _LuminancePreservation);
                glass = AdjustSaturation(glass, _Saturation) * _Brightness;
                glass = lerp(glass, glass * _TintColor.rgb, _TintColor.a);
                glass *= _Exposure;
                half darkMask = 1.0h - saturate(dot(glass, half3(0.299h, 0.587h, 0.114h)));
                glass = lerp(glass, half3(1.0h, 1.0h, 1.0h), _ShadowLift * darkMask);

                half border = 1.0h - smoothstep(_BorderWidth - aa, _BorderWidth + aa, abs(sdf));
                half top = saturate(input.uv.y - 0.5h) * 2.0h;
                half bottom = saturate(0.5h - input.uv.y) * 2.0h;
                half innerEdge = 1.0h - smoothstep(0.0h, max(_CornerRadius * 0.65h, 1.0h), -sdf);
                glass += top * innerEdge * _TopHighlight;
                glass *= 1.0h - bottom * innerEdge * _BottomShade;
                // The effect strength blends processed and original scene color
                // inside the shader. Output alpha remains solid within the mask so
                // the sampled background is not blended with itself a second time.
                glass = lerp(scene, glass, _Opacity);
                glass = lerp(glass, _BorderColor.rgb, border * _BorderColor.a);

                // Retain the baked PNG's border/gloss artwork as a subtle overlay.
                half artwork = saturate(bakedSprite.a * _SpriteOverlay);
                glass = lerp(glass, bakedSprite.rgb, artwork);

                half alpha = effectMask * input.color.a;
                return half4(glass * input.color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
