Shader "TerrainLab/WaterDepthShore"
{
    Properties
    {
        _ShallowColor ("Shallow Color", Color) = (0.42, 0.73, 0.88, 1)
        _MidGreenColor ("Mid Green Color", Color) = (0.31, 0.63, 0.54, 1)
        _DeepBlueColor ("Deep Blue Color", Color) = (0.08, 0.27, 0.58, 1)
        _FoamColor ("Foam Color", Color) = (0.96, 0.98, 1, 0.95)

        _DepthBlueStart ("Depth Blue Start", Float) = 2
        _DepthBlueRange ("Depth Blue Range", Float) = 22
        _DepthGreenMin ("Depth Green Min", Float) = 3
        _DepthGreenPeak ("Depth Green Peak", Float) = 8
        _DepthGreenMax ("Depth Green Max", Float) = 14
        _DepthGreenStrength ("Depth Green Strength", Range(0, 1)) = 0.35
        _ShallowAlpha ("Shallow Alpha", Range(0, 1)) = 0.52
        _DeepAlpha ("Deep Alpha", Range(0, 1)) = 0.9

        _ShoreDepthMin ("Shore Depth Min", Float) = 0.05
        _ShoreDepthMax ("Shore Depth Max", Float) = 2
        _ShoreSpeed ("Shore Speed", Float) = 1.2
        _ShoreStrength ("Shore Strength", Range(0, 2)) = 0.65
        _ShoreNoiseScale ("Shore Noise Scale", Float) = 0.08
        _ShoreDistanceMap ("Shore Distance Map", 2D) = "black" {}
        _ShoreMapWorldMin ("Shore Map World Min", Vector) = (0, 0, 0, 0)
        _ShoreMapWorldSize ("Shore Map World Size", Vector) = (1, 1, 0, 0)
        _ShoreDistanceMax ("Shore Distance Max", Float) = 10

        _NormalMapA ("Normal Map A", 2D) = "bump" {}
        _NormalMapB ("Normal Map B", 2D) = "bump" {}
        _NormalWorldScaleA ("Normal World Scale A", Float) = 0.08
        _NormalWorldScaleB ("Normal World Scale B", Float) = 0.14
        _NormalSpeedA ("Normal Speed A", Vector) = (0.04, 0.02, 0, 0)
        _NormalSpeedB ("Normal Speed B", Vector) = (-0.03, 0.015, 0, 0)
        _NormalStrength ("Normal Strength", Range(0, 2)) = 0.75

        _WaterSpecColor ("Specular Color", Color) = (1, 1, 1, 1)
        _WaterSpecStrength ("Specular Strength", Range(0, 4)) = 1.2
        _WaterSpecPower ("Specular Power", Float) = 96
        _WaterFresnelStrength ("Fresnel Strength", Range(0, 2)) = 0.75
        _WaterFresnelPower ("Fresnel Power", Float) = 4
        _WaterReflectionHorizonColor ("Reflection Horizon Color", Color) = (0.42, 0.54, 0.63, 1)
        _WaterReflectionSkyColor ("Reflection Sky Color", Color) = (0.66, 0.78, 0.91, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
        }

        LOD 100
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            sampler2D _NormalMapA;
            sampler2D _NormalMapB;
            sampler2D _ShoreDistanceMap;

            fixed4 _ShallowColor;
            fixed4 _MidGreenColor;
            fixed4 _DeepBlueColor;
            fixed4 _FoamColor;

            float _DepthBlueStart;
            float _DepthBlueRange;
            float _DepthGreenMin;
            float _DepthGreenPeak;
            float _DepthGreenMax;
            float _DepthGreenStrength;
            float _ShallowAlpha;
            float _DeepAlpha;

            float _ShoreDepthMin;
            float _ShoreDepthMax;
            float _ShoreSpeed;
            float _ShoreStrength;
            float _ShoreNoiseScale;
            float4 _ShoreMapWorldMin;
            float4 _ShoreMapWorldSize;
            float _ShoreDistanceMax;

            float _NormalWorldScaleA;
            float _NormalWorldScaleB;
            float4 _NormalSpeedA;
            float4 _NormalSpeedB;
            float _NormalStrength;

            fixed4 _WaterSpecColor;
            float _WaterSpecStrength;
            float _WaterSpecPower;
            float _WaterFresnelStrength;
            float _WaterFresnelPower;
            fixed4 _WaterReflectionHorizonColor;
            fixed4 _WaterReflectionSkyColor;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(0.1031, 0.11369));
                p += dot(p, p.yx + 33.33);
                return frac((p.x + p.y) * p.x);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float rawDepth = SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos));
                float sceneEyeDepth = LinearEyeDepth(rawDepth);
                float waterEyeDepth = -UnityWorldToViewPos(i.worldPos).z;
                float thickness = max(0.0, sceneEyeDepth - waterEyeDepth);

                float2 normalUvA = (i.worldPos.xz * _NormalWorldScaleA) + (_Time.y * _NormalSpeedA.xy);
                float2 normalUvB = (i.worldPos.xz * _NormalWorldScaleB) + (_Time.y * _NormalSpeedB.xy);
                float3 normalA = UnpackNormal(tex2D(_NormalMapA, normalUvA));
                float3 normalB = UnpackNormal(tex2D(_NormalMapB, normalUvB));
                float2 combinedNormalXY = ((normalA.xy + normalB.xy) * 0.5) * _NormalStrength;
                float3 normalTS = normalize(float3(combinedNormalXY.x, combinedNormalXY.y, 1.0));

                // Water mesh lies on XZ plane: tangent=X, bitangent=Z, normal=Y.
                float3 waterNormal = normalize(float3(normalTS.x, normalTS.z, normalTS.y));
                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                if (_WorldSpaceLightPos0.w > 0.0001)
                {
                    lightDir = normalize(_WorldSpaceLightPos0.xyz - i.worldPos);
                }

                float depthBlueT = saturate((thickness - _DepthBlueStart) / max(0.0001, _DepthBlueRange));
                float3 waterColor = lerp(_ShallowColor.rgb, _DeepBlueColor.rgb, depthBlueT);

                float up = smoothstep(_DepthGreenMin, _DepthGreenPeak, thickness);
                float down = 1.0 - smoothstep(_DepthGreenPeak, _DepthGreenMax, thickness);
                float greenMask = saturate(up * down * _DepthGreenStrength);
                waterColor = lerp(waterColor, _MidGreenColor.rgb, greenMask);

                float baseAlpha = lerp(_ShallowAlpha, _DeepAlpha, depthBlueT);

                float2 mapSize = max(_ShoreMapWorldSize.xy, float2(0.001, 0.001));
                float2 shoreUv = (i.worldPos.xz - _ShoreMapWorldMin.xy) / mapSize;
                float2 uvSat = saturate(shoreUv);
                float inBounds = step(0.0, shoreUv.x) * step(shoreUv.x, 1.0) * step(0.0, shoreUv.y) * step(shoreUv.y, 1.0);
                float distanceNorm = tex2D(_ShoreDistanceMap, uvSat).r;
                float distanceMeters = distanceNorm * max(0.001, _ShoreDistanceMax);
                float shoreMask = (1.0 - smoothstep(_ShoreDepthMin, _ShoreDepthMax, distanceMeters)) * inBounds;

                float shoreMaskTight = saturate((shoreMask - 0.12) / 0.88);
                shoreMaskTight *= shoreMaskTight;

                float noiseScale = max(0.001, _ShoreNoiseScale);
                float noiseA = Hash21(i.worldPos.xz * noiseScale);
                float noiseB = Hash21((i.worldPos.xz + 37.1) * (noiseScale * 0.55));
                float phase = (noiseA * 0.65 + noiseB * 0.35) * 6.2831853;
                float pulse = 0.5 + (0.5 * sin((_Time.y * _ShoreSpeed) + phase));

                // Soft clamp avoids hard saturation plateaus when ShoreStrength is high.
                float shorelineLinear = shoreMaskTight * lerp(0.35, 1.0, pulse) * _ShoreStrength;
                float shoreline = 1.0 - exp(-shorelineLinear);

                float3 halfDir = normalize(lightDir + viewDir);
                float specNdotL = saturate(dot(waterNormal, lightDir));
                float specNdotH = saturate(dot(waterNormal, halfDir));
                float specular = pow(specNdotH, max(1.0, _WaterSpecPower)) * _WaterSpecStrength * specNdotL;
                float3 specularColor = _WaterSpecColor.rgb * _LightColor0.rgb * specular;

                float fresnel = pow(1.0 - saturate(dot(waterNormal, viewDir)), max(0.01, _WaterFresnelPower)) * _WaterFresnelStrength;
                float3 reflectDir = reflect(-viewDir, waterNormal);
                float reflectT = saturate((reflectDir.y * 0.5) + 0.5);
                float3 reflectionColor = lerp(_WaterReflectionHorizonColor.rgb, _WaterReflectionSkyColor.rgb, reflectT) * fresnel;

                float3 color = lerp(waterColor, _FoamColor.rgb, shoreline);
                color += reflectionColor + specularColor;
                float alpha = saturate(baseAlpha + (shoreline * _FoamColor.a * 0.5) + (fresnel * 0.12));
                return fixed4(color, alpha);
            }
            ENDCG
        }
    }

    FallBack Off
}
