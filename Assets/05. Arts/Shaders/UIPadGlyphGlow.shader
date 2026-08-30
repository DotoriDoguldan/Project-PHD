// 패드 버튼 전용 UI 셰이더.
// 버튼 아트에서 채도가 높은 픽셀(색 문양·색 링)만 골라 _Glow 만큼 밝힌다 —
// 금속 베젤·검은 바탕은 채도가 낮아 그대로 남고, 문양만 네온처럼 빛난다.
// _Glow 는 PadButton 이 연출(출제 강조·눌림)마다 0→1→0 으로 굴린다.
// UI/Default 를 바탕으로 했고 스텐실(마스크) 속성을 그대로 둬 캔버스 마스크와도 호환된다.
Shader "PHD/UI/PadGlyphGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Glow ("Glow (0~1, 스크립트가 굴림)", Range(0, 1)) = 0
        _GlowStrength ("Glow Strength (최대 밝기 배율)", Range(0, 3)) = 1.5
        _SatThreshold ("Saturation Threshold (이 채도부터 빛남)", Range(0, 1)) = 0.25

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
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
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _MainTex_ST;
            half _Glow;
            half _GlowStrength;
            half _SatThreshold;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half4 c = tex2D(_MainTex, i.texcoord) * i.color;

                // 채도 = (최대-최소)/최대. 문양·색 링은 높고 금속 베젤·검은 바탕은 낮다.
                half mx = max(c.r, max(c.g, c.b));
                half mn = min(c.r, min(c.g, c.b));
                half sat = mx > 0.0039 ? (mx - mn) / mx : 0;
                half mask = smoothstep(_SatThreshold, 1, sat);

                // 제 색을 키우고(_GlowStrength) 흰 심을 살짝 얹어 네온처럼 보이게 한다.
                half glow = _Glow * _GlowStrength * mask;
                c.rgb += c.rgb * glow + mx * glow * 0.35;

                return c;
            }
            ENDCG
        }
    }
}
