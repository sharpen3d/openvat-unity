Shader "Custom/VATShader"
{
    Properties
    {
        [MainTexture] _BaseColor ("Base Color", 2D) = "white" {}
        [NoScaleOffset] _openVAT_main ("VAT Texture", 2D) = "white" {}
        _minValues ("Min Values", Vector) = (0, 0, 0, 0)
        _maxValues ("Max Values", Vector) = (1, 1, 1, 0)
        _frame ("Frame", Float) = 0
        _frames ("Total Frames", Float) = 1
        _resolutionY ("VAT Frame Count", Float) = 512
        _speed ("Animation Speed", Float) = 1
        _ResetTime ("Reset Time", Float) = 0
        _InitialFrameOffset ("Initial Frame Offset", Float) = 0
        [ToggleUI] _UseTime ("Use Time", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define BASE_COLOR_TINT float4(0.5, 0.5, 0.5, 1)

            CBUFFER_START(UnityPerMaterial)
                float4 _openVAT_main_TexelSize;
                float3 _minValues;
                float3 _maxValues;
                float _frame;
                float _frames;
                float _resolutionY;
                float _speed;
                float _ResetTime;
                float _InitialFrameOffset;
                float _UseTime;
            CBUFFER_END

            TEXTURE2D(_BaseColor);
            SAMPLER(sampler_BaseColor);
            TEXTURE2D(_openVAT_main);
            SAMPLER(sampler_openVAT_main);

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float3 RemapVAT(float3 color)
            {
                float3 remapped;
                remapped.r = _minValues.r + color.r * (_maxValues.r - _minValues.r);
                remapped.g = _minValues.g + color.g * (_maxValues.g - _minValues.g);
                remapped.b = _minValues.b + color.b * (_maxValues.b - _minValues.b);
                return remapped * 0.01;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                float currentFrame = _UseTime > 0.5 ? (_Time.y - _ResetTime) * _speed * 30.0 : _frame;
                currentFrame += _InitialFrameOffset;
                float frameFraction = frac(currentFrame / _frames);
                float frameIndex = frameFraction * (_frames - 1.0);

                float2 vatUV = float2(IN.uv2.x, IN.uv2.y - frameIndex / _resolutionY);
                float3 vatPosition = RemapVAT(SAMPLE_TEXTURE2D_LOD(_openVAT_main, sampler_openVAT_main, vatUV, 0).rgb);

                OUT.positionCS = TransformObjectToHClip(IN.positionOS + vatPosition);
                OUT.uv = IN.uv;

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                half4 baseColor = SAMPLE_TEXTURE2D(_BaseColor, sampler_BaseColor, IN.uv) * BASE_COLOR_TINT;

                return baseColor;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
