// C-02: картинка с кассеты VHS для uGUI (RawImage) — на неё выводится RenderTexture ночной записи.
// Яркость отдельно от цвета: цвет размыт и сдвинут вправо (полоса цветности VHS), строки дрожат, снизу —
// срыв переключения головок, по кадру ползёт полоса ошибки трекинга, тёмные места шумят (камера тянет
// усиление в темноте), чёрный приподнят. _Static — потеря сигнала («снег»), _Tracking — сила ошибки трекинга.
Shader "KFB/VhsTape"
{
    Properties
    {
        [PerRendererData] _MainTex ("Tape", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Static ("Signal loss", Range(0, 1)) = 0
        _Tracking ("Tracking error", Range(0, 1)) = 0.1
        _Gain ("Low-light gain", Range(0.5, 6)) = 1.8
        _Saturation ("Saturation", Range(0, 1.5)) = 0.65

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }

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
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
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
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float _Static, _Tracking, _Gain, _Saturation;

            static const float Lines = 240.0;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            float hash(float2 p)
            {
                p = frac(p * float2(0.1031, 0.1030));
                p += dot(p, p.yx + 33.33);
                return frac((p.x + p.y) * p.x);
            }

            float3 toYiq(float3 c)
            {
                return float3(dot(c, float3(0.299, 0.587, 0.114)),
                              dot(c, float3(0.596, -0.274, -0.322)),
                              dot(c, float3(0.211, -0.523, 0.312)));
            }

            float3 toRgb(float3 yiq)
            {
                return float3(yiq.x + 0.956 * yiq.y + 0.621 * yiq.z,
                              yiq.x - 0.272 * yiq.y - 0.647 * yiq.z,
                              yiq.x - 1.106 * yiq.y + 1.703 * yiq.z);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.texcoord;
                float frame = floor(_Time.y * 29.97);
                float row = floor(uv.y * Lines);

                // Строки чуть гуляют по горизонтали; полоса трекинга ползёт снизу вверх и рвёт строки сильнее.
                float jitter = (hash(float2(row, frame)) - 0.5) * 0.0022 * (1.0 + _Tracking * 4.0);
                float bandY = frac(_Time.y * 0.045 + 0.35);
                float bandWidth = 0.025 + 0.09 * _Tracking;
                float band = saturate(1.0 - abs(uv.y - bandY) / bandWidth) * _Tracking;
                jitter += band * (hash(float2(row * 1.7, frame + 11.0)) - 0.35) * 0.09;
                // Переключение головок: нижние строки уезжают вбок.
                float headSwitch = saturate(1.0 - uv.y / 0.03);
                jitter += headSwitch * (0.03 + 0.04 * hash(float2(row, frame + 3.0)));
                uv.x += jitter;

                float3 sharp = tex2D(_MainTex, uv).rgb;
                float texel = _MainTex_TexelSize.x;
                float3 smear = 0;
                [unroll] for (int k = -3; k <= 3; k++)
                    smear += tex2D(_MainTex, uv + float2((k * 1.2 - 1.5) * texel, 0)).rgb;
                smear /= 7.0;

                // Яркость своя (с лёгкой «окантовкой» резкости), цвет — от размытого и сдвинутого сигнала.
                float3 yiq = toYiq(sharp);
                float lumaBlur = toYiq(tex2D(_MainTex, uv - float2(texel * 1.5, 0)).rgb).x;
                yiq.x += (yiq.x - lumaBlur) * 0.6;
                float3 chroma = toYiq(smear);
                float3 color = toRgb(float3(yiq.x, chroma.y * _Saturation, chroma.z * _Saturation));

                // Камера в темноте тянет усиление: картинка светлее, чёрный серый, тени шумят.
                color = max(color, 0) * _Gain;
                color = color / (1.0 + color * 0.45);
                float luma = dot(color, float3(0.299, 0.587, 0.114));
                float grain = hash(floor(uv * float2(360.0, Lines)) + frame * 1.37) - 0.5;
                color += grain * (0.015 + 0.035 * (1.0 - saturate(luma * 2.0)));
                color = color * 0.9 + float3(0.035, 0.04, 0.045);
                color *= float3(0.97, 1.02, 1.0);

                // Полоса трекинга светлеет и сыплет точками; редкие белые чёрточки выпадений.
                color += band * hash(float2(floor(uv.x * 220.0), row + frame)) * 0.55;
                float dropRow = hash(float2(row, floor(frame * 0.5)));
                float dropX = hash(float2(row + 5.0, floor(frame * 0.5)));
                if (dropRow > 0.996 - _Tracking * 0.01 && abs(uv.x - dropX) < 0.06) color += 0.6;
                color += headSwitch * hash(float2(uv.x * 300.0, frame)) * 0.25;

                // Строчная структура и виньетка кинескопа.
                color *= 0.92 + 0.08 * sin(uv.y * Lines * 3.14159);
                float2 centered = i.texcoord - 0.5;
                color *= saturate(1.15 - dot(centered, centered) * 1.1);

                // Потеря сигнала — «снег».
                float snow = hash(floor(i.texcoord * float2(300.0, Lines)) + frame * 3.1);
                color = lerp(color, snow.xxx * 0.85 + 0.05, _Static);

                return fixed4(saturate(color), 1.0) * i.color;
            }
            ENDCG
        }
    }
}
