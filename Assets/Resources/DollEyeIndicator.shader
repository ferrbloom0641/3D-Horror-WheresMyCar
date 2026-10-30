Shader "ParkingGarage/DollEyeIndicator"
{
    Properties
    {
        _MainTex ("Soft indicator", 2D) = "white" {}
        [HDR] _Color ("Indicator color", Color) = (2,0,0,1)
        _PhoneView ("Phone view", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _PhoneView;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                if (_PhoneView > 0.5h)
                {
                    // Analytic circular mask: never show the quad's square edge.
                    half radius = length(input.uv * 2.0h - 1.0h);
                    alpha = 1.0h - smoothstep(0.7h, 1.0h, radius);
                }
                return half4(_Color.rgb, alpha * _Color.a);
            }
            ENDHLSL
        }
    }
}
