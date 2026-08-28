Shader "UI/URP Frosted Glass Diffraction"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _TintColor ("Glass Tint", Color) = (0.92, 0.97, 1.0, 1.0)
        _Opacity ("Glass Opacity", Range(0, 1)) = 0.38
        _CornerRadius ("Corner Radius (Pixels)", Range(0, 256)) = 64
        _Inset ("Edge Inset (Pixels)", Range(0, 16)) = 2
        _BorderWidth ("Border Width (Pixels)", Range(0, 12)) = 1.5
        _BorderColor ("Border Color", Color) = (1, 1, 1, 0.72)
        _Refraction ("Refraction", Range(0, 24)) = 7
        _Diffraction ("RGB Diffraction", Range(0, 8)) = 2.2
        _BlurRadius ("Blur Radius", Range(0, 12)) = 3.5
        _Brightness ("Brightness", Range(0.5, 2)) = 1.08
        _Saturation ("Saturation", Range(0, 2)) = 1.08
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
                half _CornerRadius;
                half _Inset;
                half _BorderWidth;
                half _Refraction;
                half _Diffraction;
                half _BlurRadius;
                half _Brightness;
                half _Saturation;
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
                clip(inside - 0.001h);

                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float depth = saturate(-sdf / max(_CornerRadius, 1.0));
                float2 normal = normalize(p + float2(1e-4, 1e-4));
                float2 refractOffset = normal * (_Refraction * depth) / _ScreenParams.xy;
                float2 chromaOffset = normal * (_Diffraction * pow(max(depth, 1e-3), 0.15)) / _ScreenParams.xy;

                half3 glass;
                glass.r = BlurScene(screenUV + refractOffset + chromaOffset, _BlurRadius).r;
                glass.g = BlurScene(screenUV + refractOffset, _BlurRadius).g;
                glass.b = BlurScene(screenUV + refractOffset - chromaOffset, _BlurRadius).b;
                glass = AdjustSaturation(glass, _Saturation) * _Brightness;
                glass *= _TintColor.rgb;

                half border = 1.0h - smoothstep(_BorderWidth - aa, _BorderWidth + aa, abs(sdf));
                half top = saturate(input.uv.y - 0.5h) * 2.0h;
                half bottom = saturate(0.5h - input.uv.y) * 2.0h;
                half innerEdge = 1.0h - smoothstep(0.0h, max(_CornerRadius * 0.65h, 1.0h), -sdf);
                glass += top * innerEdge * _TopHighlight;
                glass *= 1.0h - bottom * innerEdge * _BottomShade;
                glass = lerp(glass, _BorderColor.rgb, border * _BorderColor.a);

                half spriteAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                half alpha = inside * spriteAlpha * input.color.a;
                alpha *= lerp(_Opacity, 1.0h, border * _BorderColor.a);
                return half4(glass * input.color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
