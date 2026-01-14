Shader "Hidden/VHSRetroFeature"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _RGBShiftAmount ("RGB Shift Amount", Range(0, 0.05)) = 0.012
        _RGBShiftSpeed ("RGB Shift Speed", Range(0, 5)) = 1.5
        _RGBShiftCurve ("RGB Shift Curve", Range(0.1, 3)) = 1.2
        _PixelSize ("Pixel Size", Range(1, 15)) = 4.0
        _PixelScanlines ("Pixel Scanlines", Range(0, 1)) = 0.6
        _GrainAmount ("Grain Amount", Range(0, 0.5)) = 0.15
        _GrainSize ("Grain Size", Range(0.5, 3)) = 1.2
        _NoiseIntensity ("Noise Intensity", Range(0, 0.3)) = 0.08
        _ScanlineIntensity ("Scanline Intensity", Range(0, 1)) = 0.45
        _ScanlineSpeed ("Scanline Speed", Range(0, 5)) = 0.8
        _ScanlineCount ("Scanline Count", Range(200, 800)) = 480
        _ScanlineSharpness ("Scanline Sharpness", Range(0.5, 2)) = 1.2
        _Vignette ("Vignette Intensity", Range(0, 2)) = 0.6
        _VignetteSoftness ("Vignette Softness", Range(0.1, 2)) = 0.8
        _VignetteRoundness ("Vignette Roundness", Range(0.5, 3)) = 1.5
        _GlitchIntensity ("Glitch Intensity", Range(0, 1)) = 0.25
        _GlitchSpeed ("Glitch Speed", Range(0, 10)) = 3.0
        _GlitchBlockSize ("Glitch Block Size", Range(0.005, 0.05)) = 0.015
        _Contrast ("Contrast", Range(0.8, 1.5)) = 1.08
        _Saturation ("Saturation", Range(0.8, 1.4)) = 1.12
        _Brightness ("Brightness", Range(-0.2, 0.2)) = 0.02
        _Gamma ("Gamma", Range(0.8, 1.2)) = 1.05
        _TapeNoise ("Tape Noise", Range(0, 0.2)) = 0.06
        _TapeSpeed ("Tape Speed", Range(0, 5)) = 0.5
        _TapeWobble ("Tape Wobble", Range(0, 0.02)) = 0.0
        _TrackingNoise ("Tracking Noise", Range(0, 0.1)) = 0.03
        _TrackingSpeed ("Tracking Speed", Range(0, 5)) = 1.2
        _DropoutIntensity ("Dropout Intensity", Range(0, 0.1)) = 0.02
        _DropoutSpeed ("Dropout Speed", Range(0, 10)) = 4.0
        _TimeSpeed ("Time Speed", Range(0, 3)) = 1.0
        [Toggle] _EnablePixel ("Enable Pixelation", Float) = 1
        [Toggle] _EnableGlitch ("Enable Glitch", Float) = 1
        [Toggle] _EnableRGB ("Enable RGB Shift", Float) = 1
        [Toggle] _EnableGrain ("Enable Grain", Float) = 1
        [Toggle] _EnableScan ("Enable Scanlines", Float) = 1
        [Toggle] _EnableTape ("Enable Tape Effects", Float) = 0
        [Toggle] _EnableTracking ("Enable Tracking", Float) = 1
        [Toggle] _EnableDropout ("Enable Dropout", Float) = 1
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "Queue" = "Overlay"
        }
        
        LOD 100
        Cull Off
        ZWrite Off
        ZTest Always
        Blend Off

        Pass
        {
            Name "VHS_EFFECT"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;
            
            float _RGBShiftAmount;
            float _RGBShiftSpeed;
            float _RGBShiftCurve;
            float _PixelSize;
            float _PixelScanlines;
            float _GrainAmount;
            float _GrainSize;
            float _NoiseIntensity;
            float _ScanlineIntensity;
            float _ScanlineSpeed;
            float _ScanlineCount;
            float _ScanlineSharpness;
            float _Vignette;
            float _VignetteSoftness;
            float _VignetteRoundness;
            float _GlitchIntensity;
            float _GlitchSpeed;
            float _GlitchBlockSize;
            float _Contrast;
            float _Saturation;
            float _Brightness;
            float _Gamma;
            float _TapeNoise;
            float _TapeSpeed;
            float _TapeWobble;
            float _TrackingNoise;
            float _TrackingSpeed;
            float _DropoutIntensity;
            float _DropoutSpeed;
            float _TimeSpeed;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0)
                    o.uv.y = 1.0 - o.uv.y;
                #endif
                return o;
            }

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;
                for (int i = 0; i < 3; i++)
                {
                    value += amplitude * noise(p * frequency);
                    frequency *= 2.0;
                    amplitude *= 0.5;
                }
                return value;
            }

            float3 applySaturation(float3 color, float saturation)
            {
                float luminance = dot(color, float3(0.299, 0.587, 0.114));
                return lerp(float3(luminance, luminance, luminance), color, saturation);
            }

            float3 applyContrast(float3 color, float contrast)
            {
                return saturate(((color - 0.5) * contrast) + 0.5);
            }

            float3 applyGamma(float3 color, float gamma)
            {
                return pow(color, 1.0 / gamma);
            }

            float3 applyRGBShift(float2 uv, float amount, float time)
            {
                float shift = amount * (0.7 + 0.3 * sin(time * _RGBShiftSpeed));
                shift = pow(shift, _RGBShiftCurve);
                float2 shiftedR = uv + float2(shift * 1.0, shift * 0.1);
                float2 shiftedG = uv + float2(shift * -0.5, shift * -0.05);
                float2 shiftedB = uv + float2(shift * -1.0, shift * 0.2);
                shiftedR = saturate(shiftedR);
                shiftedG = saturate(shiftedG);
                shiftedB = saturate(shiftedB);
                float r = tex2D(_MainTex, shiftedR).r;
                float g = tex2D(_MainTex, shiftedG).g;
                float b = tex2D(_MainTex, shiftedB).b;
                return float3(r, g, b);
            }

            float2 applyTapeWobble(float2 uv, float time)
            {
                return uv;
            }

            float2 applyTracking(float2 uv, float time)
            {
                if (_TrackingNoise > 0.001)
                {
                    float tracking = noise(float2(uv.y * 25.0, time * _TrackingSpeed)) * _TrackingNoise * 0.005;
                    uv.y += tracking;
                }
                return saturate(uv);
            }

            float2 applyGlitch(float2 uv, float time)
            {
                if (_GlitchIntensity > 0.001)
                {
                    float glitch = hash(float2(time * _GlitchSpeed, uv.y * 50.0));
                    if (glitch < _GlitchIntensity * 0.3)
                    {
                        float block = floor(uv.y / _GlitchBlockSize);
                        float offset = (hash(float2(time, block)) - 0.5) * _GlitchIntensity * 0.08;
                        uv.x += offset;
                    }
                }
                return saturate(uv);
            }

            float applyDropout(float2 uv, float time)
            {
                float signal = 1.0;
                if (_DropoutIntensity > 0.001)
                {
                    float dropout = fbm(uv * 8.0 + time * _DropoutSpeed);
                    if (dropout < _DropoutIntensity)
                    {
                        signal = lerp(0.0, 1.0, dropout / _DropoutIntensity);
                    }
                }
                return signal;
            }

            float applyVignette(float2 uv)
            {
                float2 centered = uv - 0.5;
                float dist = length(centered * float2(_VignetteRoundness, 1.0));
                float vignette = 1.0 - smoothstep(0.0, _Vignette * 0.7, dist);
                vignette = pow(vignette, _VignetteSoftness);
                return vignette;
            }

            float applyScanlines(float2 uv, float time)
            {
                float scan = frac(uv.y * _ScanlineCount + time * _ScanlineSpeed);
                scan = abs(scan - 0.5) * 2.0;
                scan = pow(scan, _ScanlineSharpness);
                return 1.0 - scan * _ScanlineIntensity;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float time = _Time.y * _TimeSpeed;
                uv = applyTapeWobble(uv, time);
                uv = applyTracking(uv, time);
                uv = applyGlitch(uv, time);
                float3 color = tex2D(_MainTex, uv).rgb;
                if (_RGBShiftAmount > 0.001)
                {
                    color = applyRGBShift(uv, _RGBShiftAmount, time);
                }
                if (_PixelSize > 1.01)
                {
                    float2 pixelUV = floor(uv * _PixelSize) / _PixelSize;
                    pixelUV = saturate(pixelUV);
                    float3 pixelColor = tex2D(_MainTex, pixelUV).rgb;
                    if (_PixelScanlines > 0.01)
                    {
                        float pixelScan = frac(pixelUV.y * _ScanlineCount * 0.5);
                        pixelScan = abs(pixelScan - 0.5) * 2.0;
                        pixelColor *= 1.0 - pixelScan * _PixelScanlines * 0.3;
                    }
                    color = pixelColor;
                }
                if (_ScanlineIntensity > 0.001)
                {
                    float scanline = applyScanlines(uv, time);
                    color *= scanline;
                }
                if (_GrainAmount > 0.001)
                {
                    float grain = fbm(uv * _GrainSize * 50.0 + time) * _GrainAmount;
                    color += (grain - 0.5) * 0.02;
                }
                if (_TapeNoise > 0.001)
                {
                    float tapeNoise = noise(uv * 3.0 + time * _TapeSpeed) * _TapeNoise;
                    color += (tapeNoise - 0.5) * 0.01;
                }
                float dropout = applyDropout(uv, time);
                color *= dropout;
                color = applyContrast(color, _Contrast);
                color = applySaturation(color, _Saturation);
                color = applyGamma(color, _Gamma);
                color += _Brightness;
                if (_Vignette > 0.001)
                {
                    float vignette = applyVignette(uv);
                    color *= vignette;
                }
                color = saturate(color);
                return fixed4(color, 1.0);
            }
            ENDCG
        }
    }
    
    Fallback Off
}
