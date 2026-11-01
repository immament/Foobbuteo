Shader "Foobbuteo/GameRoomSurface"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [Toggle] _Range ("Range overlay", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Range;
            CBUFFER_END
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }
            float Segment(float2 p, float2 a, float2 b)
            {
                float2 ab = b - a;
                return length(p - a - ab * saturate(dot(p - a, ab) / dot(ab, ab)));
            }
            float BoxLine(float2 p, float2 minimum, float2 maximum)
            {
                float2 q = abs(p - (minimum + maximum) * 0.5) - (maximum - minimum) * 0.5;
                return abs(length(max(q, 0)) + min(max(q.x, q.y), 0));
            }
            half3 Pitch(float2 uv)
            {
                // Match the existing 656 x 477 field sprite and world-space gameplay coordinates.
                float2 p = float2(uv.x, 1 - uv.y) * float2(656, 477);
                float pixel = max(length(ddx(p)), length(ddy(p)));
                float halfWidth = max(0.55, pixel * 0.75);
                float feather = max(pixel * 0.65, 0.01);
                float markingDistance = BoxLine(p, float2(21, 31), float2(630, 450));
                markingDistance = min(markingDistance, Segment(p, float2(325.5, 31), float2(325.5, 450)));
                markingDistance = min(markingDistance, abs(length(p - float2(325.5, 240.5)) - 53.5));
                markingDistance = min(markingDistance, BoxLine(p, float2(21, 115), float2(116, 366)));
                markingDistance = min(markingDistance, BoxLine(p, float2(534, 115), float2(630, 366)));
                markingDistance = min(markingDistance, BoxLine(p, float2(21, 172), float2(53, 309)));
                markingDistance = min(markingDistance, BoxLine(p, float2(598, 172), float2(630, 309)));
                float leftArc = max(abs(length(p - float2(81.5, 240.5)) - 56.5), 116 - p.x);
                float rightArc = max(abs(length(p - float2(571.5, 240.5)) - 56.5), p.x - 534);
                markingDistance = min(markingDistance, min(leftArc, rightArc));
                float2 corner = float2(p.x < 325.5 ? 21 : 630, p.y < 240.5 ? 31 : 450);
                float cornerArc = max(abs(length(p - corner) - 6),
                    max(max(21 - p.x, p.x - 630), max(31 - p.y, p.y - 450)));
                markingDistance = min(markingDistance, cornerArc);
                markingDistance = min(markingDistance, length(p - float2(81.5, 240.5)));
                markingDistance = min(markingDistance, length(p - float2(571.5, 240.5)));
                float goals = min(BoxLine(p, float2(7, 205), float2(21, 277)),
                    BoxLine(p, float2(630, 205), float2(644, 277)));
                markingDistance = min(markingDistance, goals);
                half stripe = smoothstep(-0.12, 0.12, sin(p.x * 3.14159265 / 32));
                half3 turf = lerp(SRGBToLinear(half3(0.16, 0.34, 0.22)),
                    SRGBToLinear(half3(0.18, 0.37, 0.24)), stripe);
                half coverage = 1 - smoothstep(halfWidth - feather, halfWidth + feather, markingDistance);
                return lerp(turf, SRGBToLinear(half3(0.88, 0.93, 0.89)), coverage);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 sample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                if (_Range > 0.5)
                {
                    // These sprites have a 9% fill and a 20% outline; keep the outline distinct.
                    sample.a = lerp(min(sample.a, 0.1h), sample.a, smoothstep(0.1h, 0.15h, sample.a));
                }
                else
                {
                    sample.rgb = Pitch(input.uv);
                }
                return sample * input.color;
            }
            ENDHLSL
        }
    }
}
