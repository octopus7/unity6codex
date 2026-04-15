Shader "McpTest/VoxelVillage/HouseRevealTransparent"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.0

        [HideInInspector] _RevealEnabled("Reveal Enabled", Float) = 0.0
        [HideInInspector] _RevealHeight("Reveal Height", Float) = 0.0
        [HideInInspector] _RevealFeather("Reveal Feather", Float) = 1.0
        [HideInInspector] _RevealAlpha("Reveal Alpha", Range(0.0, 1.0)) = 0.18
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back
            ZWrite Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Smoothness;
                float _RevealEnabled;
                float _RevealHeight;
                float _RevealFeather;
                half _RevealAlpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float GetRevealFactor(float3 positionWS)
            {
                if (_RevealEnabled < 0.5)
                {
                    return 0.0;
                }

                float feather = max(_RevealFeather, 0.0001);
                return saturate((positionWS.y - _RevealHeight) / feather);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalize(normalInputs.normalWS);
                output.shadowCoord = TransformWorldToShadowCoord(positionInputs.positionWS);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float revealFactor = GetRevealFactor(input.positionWS);
                clip(revealFactor - 0.001);

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirectionWS = SafeNormalize(GetWorldSpaceViewDir(input.positionWS));

                Light mainLight = GetMainLight(input.shadowCoord);
                half3 attenuatedLight = mainLight.color * (mainLight.distanceAttenuation * mainLight.shadowAttenuation);
                half3 diffuse = LightingLambert(attenuatedLight, mainLight.direction, normalWS);
                half specularPower = lerp(8.0h, 64.0h, saturate(_Smoothness));
                half3 specular = LightingSpecular(attenuatedLight, mainLight.direction, normalWS, viewDirectionWS, half4(0.08h, 0.08h, 0.08h, 1.0h), specularPower);
                half3 ambient = SampleSH(normalWS);

                half3 color = _BaseColor.rgb * (ambient + diffuse) + specular;
                color = MixFog(color, input.fogFactor);

                half alpha = lerp(1.0h, saturate(_RevealAlpha), revealFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Simple Lit"
}
