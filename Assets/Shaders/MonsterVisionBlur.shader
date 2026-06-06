Shader "Hidden/MonsterVisionBlur"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "MonsterVisionBlur"
            ZTest Always ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D_X(_BlitTexture);
            SAMPLER(sampler_BlitTexture);

            float4 _BlitTexture_TexelSize;
            float _BlurStart;
            float _BlurEnd;
            float _MaxBlurRadius;
            float4 _TintColor;
            float _TintStrength;
            float _TintUseBlur;
            float _TintMin;
            float4 _VignetteColor;
            float _VignetteIntensity;
            float _VignetteSmoothness;
            float _VignetteRadius;
            float _DebugSolid;
            float4 _DebugColor;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert (Attributes v)
            {
                Varyings o;
                o.positionHCS = GetFullScreenTriangleVertexPosition(v.vertexID);
                o.uv = GetFullScreenTriangleTexCoord(v.vertexID);
                return o;
            }

            float SampleBlurAmount(float2 uv)
            {
                float rawDepth = SampleSceneDepth(uv);
                float2 ndc = uv * 2.0 - 1.0;
                float3 ws = ComputeWorldSpacePosition(ndc, rawDepth, UNITY_MATRIX_I_VP);
                float dist = distance(ws, _WorldSpaceCameraPos);
                float t = saturate((dist - _BlurStart) / max(0.0001, (_BlurEnd - _BlurStart)));
                return t;
            }

            half4 Frag (Varyings i) : SV_Target
            {
                if (_DebugSolid > 0.5)
                    return _DebugColor;

                float blurT = SampleBlurAmount(i.uv);
                float radius = _MaxBlurRadius * blurT;
                float2 texel = _BlitTexture_TexelSize.xy;

                half4 original = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, i.uv);

                // 13-tap blur (two rings) for smoother result
                float2 r = texel * radius;
                float2 r2 = r * 2.0;

                half4 blur = original * 0.18;
                blur += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, i.uv + float2( r.x, 0)) * 0.09;
                blur += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, i.uv + float2(-r.x, 0)) * 0.09;
                blur += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, i.uv + float2(0,  r.y)) * 0.09;
                blur += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, i.uv + float2(0, -r.y)) * 0.09;
                blur += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, i.uv + float2( r.x,  r.y)) * 0.07;
                blur += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, i.uv + float2(-r.x,  r.y)) * 0.07;
                blur += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, i.uv + float2( r.x, -r.y)) * 0.07;
                blur += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, i.uv + float2(-r.x, -r.y)) * 0.07;
                blur += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, i.uv + float2( r2.x, 0)) * 0.045;
                blur += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, i.uv + float2(-r2.x, 0)) * 0.045;
                blur += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, i.uv + float2(0,  r2.y)) * 0.045;
                blur += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_BlitTexture, i.uv + float2(0, -r2.y)) * 0.045;

                // Tint less when close, more when far; applied to blurred color
                float tintBase = (_TintUseBlur > 0.5) ? lerp(_TintMin, 1.0, blurT) : 1.0;
                float tintT = saturate(_TintStrength) * tintBase;
                half3 tintedBlur = lerp(blur.rgb, blur.rgb * _TintColor.rgb, tintT);

                // Blend original vs tinted blur by blur amount
                half3 color = lerp(original.rgb, tintedBlur, blurT);

                // Vignette
                float2 p = i.uv - 0.5;
                p.x *= (_ScreenParams.x / _ScreenParams.y);
                float dist = length(p);
                float inner = max(0.0, _VignetteRadius - _VignetteSmoothness);
                float v = smoothstep(inner, _VignetteRadius, dist);
                color = lerp(color, _VignetteColor.rgb, v * saturate(_VignetteIntensity));

                return half4(color, original.a);
            }
            ENDHLSL
        }
    }
}
