Shader "Custom/DashedGrid"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _DashSize ("Dash Size", Float) = 0.2
        _GapSize ("Gap Size", Float) = 0.1
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float lineDistance : TEXCOORD0;
            };

            float4 _Color;
            float _DashSize;
            float _GapSize;

            Varyings vert (Attributes IN)
            {
                Varyings OUT;

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.lineDistance = IN.uv.x;

                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float patternLength = _DashSize + _GapSize;

                float m = fmod(IN.lineDistance, patternLength);

                if (m > _DashSize)
                    discard;

                return _Color;
            }

            ENDHLSL
        }
    }
}
