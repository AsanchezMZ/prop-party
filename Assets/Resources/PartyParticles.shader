Shader "PropParty/Layered Particles"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _Shape ("Shape: mist, ring, yolk, shell", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Shape;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _BaseColor;
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2 - 1;
                float radius = length(p);
                half alpha = 1;
                half shine = 0;
                if (_Shape < 0.5)
                    alpha = pow(saturate(1 - radius), 2);
                else if (_Shape < 1.5)
                    alpha = 1 - smoothstep(0.055, 0.13, abs(radius - 0.73));
                else if (_Shape < 2.5)
                {
                    float angle = atan2(p.y, p.x);
                    float edge = 0.7 + sin(angle * 5) * 0.11 + cos(angle * 9) * 0.06;
                    alpha = 1 - smoothstep(edge - 0.06, edge, radius);
                    shine = pow(saturate(1 - length(p - float2(-0.22, 0.24)) * 3.5), 3) * 0.55;
                }
                return half4(lerp(input.color.rgb, half3(1, 1, 0.9), shine), input.color.a * alpha);
            }
            ENDHLSL
        }
    }
}
