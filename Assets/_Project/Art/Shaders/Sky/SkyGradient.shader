// CampanhaRio sky: a soft vertical gradient (horizon -> top, horizon -> ground) with a warm glow around the sun.
// Colours come from DayCycle (globals, linear): _CR_SkyTop, _CR_SkyHorizon, _CR_SkyGround, _CR_SunGlow, _CR_SunDir.
Shader "CampanhaRio/SkyGradient"
{
    Properties
    {
        _HorizonSharpness ("Horizon Sharpness", Range(0.5, 6)) = 1.6
        _GlowSize ("Sun Glow Size", Range(1, 64)) = 8
        _DiscSize ("Sun Disc Size", Range(0, 0.02)) = 0.004
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _CR_SkyTop, _CR_SkyHorizon, _CR_SkyGround, _CR_SunGlow, _CR_SunDir;
            CBUFFER_START(UnityPerMaterial)
                float _HorizonSharpness, _GlowSize, _DiscSize;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            V Vert(A i)
            {
                V o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.dir = i.positionOS.xyz;
                return o;
            }

            half4 Frag(V i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float up = saturate(d.y);
                float down = saturate(-d.y);
                float3 sky = lerp(_CR_SkyHorizon.rgb, _CR_SkyTop.rgb, 1.0 - pow(1.0 - up, _HorizonSharpness));
                sky = lerp(sky, _CR_SkyGround.rgb, saturate(down * 4.0));
                float s = saturate(dot(d, normalize(_CR_SunDir.xyz)));
                sky += _CR_SunGlow.rgb * (pow(s, _GlowSize) * 0.6 + smoothstep(1.0 - _DiscSize, 1.0 - _DiscSize * 0.5, s) * 2.0);
                return half4(sky, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
