Shader "Labs/WaterShorelineLab/DepthShoreline"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.09, 0.57, 0.64, 0.65)
        _DeepColor ("Deep Color", Color) = (0.03, 0.16, 0.24, 0.72)
        _FoamColor ("Foam Color", Color) = (0.92, 0.97, 1.0, 0.75)
        _WaveAmplitude ("Wave Amplitude", Range(0, 1)) = 0.12
        _WaveFrequency ("Wave Frequency", Range(0, 5)) = 0.75
        _WaveSpeed ("Wave Speed", Range(0, 4)) = 1.3
        _ShoreDistance ("Shore Distance", Range(0.01, 5)) = 0.6
        _ShoreSoftness ("Shore Softness", Range(0.001, 3)) = 0.25
        _ColorBlendDistance ("Color Blend Distance", Range(0.05, 10)) = 2.2
        _FoamBreakupTex ("Foam Breakup Texture", 2D) = "gray" {}
        _FoamBreakupScale ("Foam Breakup Scale", Range(0.1, 8)) = 1.55
        _FoamBreakupStrength ("Foam Breakup Strength", Range(0, 1)) = 0.78
        _FoamBreakupContrast ("Foam Breakup Contrast", Range(0.5, 3)) = 1.45
        _FoamBreakupScroll ("Foam Breakup Scroll", Range(0, 2)) = 0.2
        _FoamTiling ("Foam Tiling", Range(0.1, 10)) = 2.0
        _FoamSpeed ("Foam Speed", Range(0, 5)) = 0.9
        _FoamMotion ("Foam Motion", Range(0, 2)) = 0.22
        _FoamWarpStrength ("Foam Warp Strength", Range(0, 1)) = 0.22
        _FoamThreshold ("Foam Threshold", Range(0, 1)) = 0.48
        _FoamFeather ("Foam Feather", Range(0.001, 0.5)) = 0.14
        _FoamBandOuterWidth ("Foam Outer Width", Range(0.01, 1)) = 0.18
        _FoamBandOuterSoftness ("Foam Outer Softness", Range(0.001, 0.5)) = 0.13
        _FoamBandOuterStrength ("Foam Outer Strength", Range(0, 2)) = 1.0
        _FoamBandInnerOffset ("Foam Inner Offset", Range(0, 1.25)) = 0.22
        _FoamBandInnerWidth ("Foam Inner Width", Range(0.01, 1)) = 0.22
        _FoamBandInnerSoftness ("Foam Inner Softness", Range(0.001, 0.5)) = 0.12
        _FoamBandInnerStrength ("Foam Inner Strength", Range(0, 2)) = 0.62
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 200
        Cull Back
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _CameraDepthTexture;
            sampler2D _FoamBreakupTex;

            float4 _ShallowColor;
            float4 _DeepColor;
            float4 _FoamColor;
            float _WaveAmplitude;
            float _WaveFrequency;
            float _WaveSpeed;
            float _ShoreDistance;
            float _ShoreSoftness;
            float _ColorBlendDistance;
            float _FoamBreakupScale;
            float _FoamBreakupStrength;
            float _FoamBreakupContrast;
            float _FoamBreakupScroll;
            float _FoamTiling;
            float _FoamSpeed;
            float _FoamMotion;
            float _FoamWarpStrength;
            float _FoamThreshold;
            float _FoamFeather;
            float _FoamBandOuterWidth;
            float _FoamBandOuterSoftness;
            float _FoamBandOuterStrength;
            float _FoamBandInnerOffset;
            float _FoamBandInnerWidth;
            float _FoamBandInnerSoftness;
            float _FoamBandInnerStrength;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float eyeDepth : TEXCOORD2;
            };

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);

                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));

                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            v2f vert(appdata v)
            {
                v2f o;

                float time = _Time.y * _WaveSpeed;
                float waveA = sin(v.vertex.x * _WaveFrequency + time);
                float waveB = cos(v.vertex.z * (_WaveFrequency * 1.23) + time * 0.77);
                float wave = (waveA + waveB) * 0.5;

                float4 local = v.vertex;
                local.y += wave * _WaveAmplitude;

                float4 world = mul(unity_ObjectToWorld, local);
                float4 clip = mul(UNITY_MATRIX_VP, world);

                o.vertex = clip;
                o.worldPos = world.xyz;
                o.screenPos = ComputeScreenPos(clip);
                o.eyeDepth = -mul(UNITY_MATRIX_V, world).z;

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float rawSceneDepth = SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos));
                float sceneDepth = LinearEyeDepth(rawSceneDepth);
                float depthDelta = max(sceneDepth - i.eyeDepth, 0.0);

                float shoreDistance = max(_ShoreDistance, 0.001);
                float shoreSoftness = max(_ShoreSoftness, 0.001);
                float shoreMask = 1.0 - smoothstep(shoreDistance, shoreDistance + shoreSoftness, depthDelta);
                float shore01 = saturate(depthDelta / shoreDistance);

                float colorBlendDistance = max(_ColorBlendDistance, 0.001);
                float shallowMask = saturate(1.0 - depthDelta / colorBlendDistance);

                float t = _Time.y * _FoamSpeed;
                float motion = t * _FoamMotion;
                float2 baseFoamUv = i.worldPos.xz * _FoamTiling;
                float2 driftA = float2(0.11, 0.07) * motion;
                float2 driftB = float2(-0.06, 0.10) * (motion * 0.83);
                float warp = (valueNoise(baseFoamUv * 0.6 + driftB + 9.7) - 0.5) * _FoamWarpStrength;
                float breakupScroll = t * _FoamBreakupScroll;
                float2 breakupUvA = i.worldPos.xz * _FoamBreakupScale + float2(0.08, 0.03) * breakupScroll + warp * 0.7;
                float2 breakupUvB = i.worldPos.xz * (_FoamBreakupScale * 1.37) + float2(-0.05, 0.07) * breakupScroll - warp * 0.6;
                float breakupA = tex2D(_FoamBreakupTex, breakupUvA).r;
                float breakupB = tex2D(_FoamBreakupTex, breakupUvB + 11.3).r;
                float breakupMix = lerp(breakupA, breakupB, 0.42);
                breakupMix = saturate((breakupMix - 0.5) * _FoamBreakupContrast + 0.5);

                float2 foamUvA = baseFoamUv + driftA + warp;
                float2 foamUvB = baseFoamUv * 1.91 + driftB - warp + 13.1;
                float phase = 0.5 + 0.5 * sin(t * 0.35);
                float foamNoiseA = lerp(valueNoise(foamUvA), valueNoise(foamUvB), phase);
                float foamNoiseB = lerp(valueNoise(foamUvA + 5.7), valueNoise(foamUvB - 3.2), 1.0 - phase);

                float feather = max(_FoamFeather, 0.001);
                float outerThreshold = saturate(_FoamThreshold - 0.06);
                float innerThreshold = saturate(_FoamThreshold + 0.03);
                float outerPattern = smoothstep(outerThreshold, min(outerThreshold + feather, 0.999), foamNoiseA);
                float innerPattern = smoothstep(innerThreshold, min(innerThreshold + feather, 0.999), foamNoiseB);
                float breakupOuter = smoothstep(0.37, 0.74, breakupMix);
                float breakupInner = smoothstep(0.33, 0.70, 1.0 - breakupMix + foamNoiseB * 0.18);
                outerPattern *= lerp(1.0, breakupOuter, _FoamBreakupStrength);
                innerPattern *= lerp(1.0, breakupInner, _FoamBreakupStrength * 0.9);

                float outerWidth = max(_FoamBandOuterWidth, 0.001);
                float outerSoftness = max(_FoamBandOuterSoftness, 0.001);
                float outerBand = 1.0 - smoothstep(outerWidth, outerWidth + outerSoftness, shore01);

                float innerStart = saturate(_FoamBandInnerOffset);
                float innerEnd = innerStart + max(_FoamBandInnerWidth, 0.001);
                float innerSoftness = max(_FoamBandInnerSoftness, 0.001);
                float innerBandIn = smoothstep(innerStart - innerSoftness, innerStart + innerSoftness, shore01);
                float innerBandOut = 1.0 - smoothstep(innerEnd - innerSoftness, innerEnd + innerSoftness, shore01);
                float innerBand = saturate(innerBandIn * innerBandOut);

                float gentlePulse = 0.9 + 0.1 * sin(t * 0.6 + foamNoiseA * 6.2831);
                float foamMask = shoreMask * gentlePulse *
                    saturate(
                        outerBand * outerPattern * _FoamBandOuterStrength +
                        innerBand * innerPattern * _FoamBandInnerStrength
                    );

                float3 baseColor = lerp(_DeepColor.rgb, _ShallowColor.rgb, shallowMask);
                float3 finalColor = baseColor + _FoamColor.rgb * foamMask;
                float alpha = lerp(_DeepColor.a, _ShallowColor.a, shallowMask);
                alpha = saturate(alpha + _FoamColor.a * foamMask);

                return float4(finalColor, alpha);
            }
            ENDCG
        }
    }

    FallBack Off
}
