Shader "Hidden/URPFrostedGlass/BakerPreview"
{
    Properties
    {
        _MainTex ("Main", 2D) = "white" {}
        _BackgroundTex ("Background", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _BackgroundTex;
            float4 _BackgroundTex_TexelSize;
            float4 _BackgroundUV;
            float4 _TintColor;
            float _PanelAspect;
            float _CornerRadiusN;
            float _BorderWidthN;
            float _FillOpacity;
            float _BorderOpacity;
            float _TopHighlight;
            float _BottomShade;
            float _GlossIntensity;
            float _GlossWidth;
            float _GlossPosition;
            float _GlossAngle;
            float _EffectStrength;
            float _RefractionPixels;
            float _LensStrength;
            float _LensPower;
            float _DiffractionPixels;
            float _BlurRadiusPixels;
            float _BlurStrength;

            float RoundedBoxSDF(float2 p, float2 halfSize, float radius)
            {
                radius = min(radius, min(halfSize.x, halfSize.y));
                float2 q = abs(p) - (halfSize - radius);
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
            }

            fixed3 SampleBackground(float2 uv)
            {
                return tex2D(_BackgroundTex, saturate(uv)).rgb;
            }

            fixed3 BlurBackground(float2 uv)
            {
                float2 px = _BackgroundTex_TexelSize.xy * _BlurRadiusPixels;
                fixed3 c = SampleBackground(uv) * 0.20;
                c += SampleBackground(uv + float2( px.x, 0)) * 0.12;
                c += SampleBackground(uv + float2(-px.x, 0)) * 0.12;
                c += SampleBackground(uv + float2(0,  px.y)) * 0.12;
                c += SampleBackground(uv + float2(0, -px.y)) * 0.12;
                c += SampleBackground(uv + px) * 0.08;
                c += SampleBackground(uv - px) * 0.08;
                c += SampleBackground(uv + float2(px.x, -px.y)) * 0.08;
                c += SampleBackground(uv + float2(-px.x, px.y)) * 0.08;
                return c;
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * float2(_PanelAspect, 1.0);
                float2 halfSize = float2(_PanelAspect, 1.0) * 0.5;
                float sdf = RoundedBoxSDF(p, halfSize, _CornerRadiusN);
                float aa = max(fwidth(sdf), 0.0005);
                float coverage = 1.0 - smoothstep(-aa, aa, sdf);
                clip(coverage - 0.001);

                float2 normalizedBox = abs(p) / max(halfSize, float2(0.001, 0.001));
                float superellipse = pow(saturate(normalizedBox.x), _LensPower)
                                   + pow(saturate(normalizedBox.y), _LensPower);
                float lensEdge = smoothstep(0.05, 1.0, saturate(superellipse));

                float2 backgroundUV = _BackgroundUV.xy + i.uv * _BackgroundUV.zw;
                float2 backgroundCenter = _BackgroundUV.xy + _BackgroundUV.zw * 0.5;
                float2 lensUV = backgroundCenter + (backgroundUV - backgroundCenter) * (1.0 - _LensStrength * lensEdge);

                float2 normal = normalize(p + float2(1e-5, 1e-5));
                float edgeDistance = saturate(-sdf / max(_CornerRadiusN, 0.001));
                float edgeWeight = 1.0 - smoothstep(0.05, 0.8, edgeDistance);
                float2 refractionOffset = normal * _RefractionPixels * _BackgroundTex_TexelSize.xy * edgeWeight;
                float2 refractedUV = lensUV + refractionOffset;

                fixed3 original = SampleBackground(backgroundUV);
                fixed3 blurred = BlurBackground(refractedUV);
                fixed3 processed = lerp(original, blurred, _BlurStrength);

                float2 diffractionOffset = normal * _DiffractionPixels * _BackgroundTex_TexelSize.xy * edgeWeight;
                fixed3 redSample = SampleBackground(refractedUV + diffractionOffset);
                fixed3 blueSample = SampleBackground(refractedUV - diffractionOffset);
                float diffractionMix = saturate(_DiffractionPixels * 0.28);
                processed.r = lerp(processed.r, redSample.r, diffractionMix);
                processed.b = lerp(processed.b, blueSample.b, diffractionMix);

                float border = 1.0 - smoothstep(_BorderWidthN - aa, _BorderWidthN + aa, abs(sdf));
                float innerEdge = 1.0 - smoothstep(0.0, max(_CornerRadiusN * 0.55, 0.001), -sdf);
                float highlight = (1.0 - i.uv.y) * innerEdge * _TopHighlight;
                float shade = i.uv.y * innerEdge * _BottomShade;
                float angle = radians(_GlossAngle);
                float glossCoordinate = (i.uv.x - 0.5) * sin(angle) + (i.uv.y - 0.5) * cos(angle) + 0.5;
                float glossDistance = abs(glossCoordinate - _GlossPosition);
                float gloss = (1.0 - smoothstep(_GlossWidth * 0.25, _GlossWidth * 0.5, glossDistance)) * _GlossIntensity;

                processed = lerp(processed, _TintColor.rgb, _FillOpacity * 0.42 + gloss * 0.35);
                processed *= 1.0 - shade * 0.3;
                processed += highlight;
                processed = lerp(original, processed, _EffectStrength);
                processed = lerp(processed, fixed3(1.0, 1.0, 1.0), border * _BorderOpacity);
                return fixed4(processed, coverage);
            }
            ENDCG
        }
    }
}
