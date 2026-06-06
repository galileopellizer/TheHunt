Shader "Hidden/SoundOrb"
{
    Properties
    {
        _Color      ("Orb Color",  Color)  = (1, 0.15, 0.05, 1)
        _Intensity  ("Intensity",  Range(0,1)) = 1
        _CoreSize   ("Core Size",  Range(0,1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Transparent"
            "RenderType"     = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "SoundOrb"

            // Additive blend — stacks light naturally, no hard edges
            Blend One One
            ZWrite Off
            ZTest Always   // visible through walls — it's a sound cue, not a sight cue
            Cull Front     // render inside of sphere so it looks like a glow volume

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _Intensity;
                float  _CoreSize;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 viewDirWS   : TEXCOORD1;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);

                // World-space normal + view dir for fresnel
                VertexNormalInputs ni = GetVertexNormalInputs(v.normalOS);
                o.normalWS  = ni.normalWS;

                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                o.viewDirWS  = GetWorldSpaceNormalizeViewDir(posWS);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 N = normalize(i.normalWS);
                float3 V = normalize(i.viewDirWS);

                // Fresnel: bright at grazing angles (rim), soft at centre
                float rim = 1.0 - saturate(dot(N, V));

                // Smooth falloff curve: hard rim glow + faint core
                float core   = saturate(1.0 - rim) * _CoreSize;
                float glow   = pow(rim, 2.5);
                float shape  = saturate(glow + core);

                half3 col = _Color.rgb * shape * _Intensity;
                return half4(col, 1.0); // alpha unused (additive)
            }
            ENDHLSL
        }
    }
}
