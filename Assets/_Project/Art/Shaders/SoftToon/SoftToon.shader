// CampanhaRio SoftToon (URP, hand-written HLSL: one readable formula, kept identical to the Blender preview material in
// ArtSource/pipeline/common.py -> soft_toon_material. Change one, change the other.)
//
//   wrapped = (N.L + wrap) / (1 + wrap)
//   light   = smoothstep(center - soft, center + soft, wrapped) * lerp(1, shadow, receive), shadow faded in over N.L 0..0.05
//   direct  = sun * light + shadowTint * (1 - light)                      (coloured shadows, never black)
//   ambient = equator + (sky - equator) * saturate(N.y) + (ground - equator) * saturate(-N.y)
//   color   = albedo * (direct + ambient) + sun * rim  [+ foliage translucency]
//   albedo  = _BaseMap * _BaseColor * vertex colour (opt.) * height gradient * top tint
//
// Globals (set by DayCycle): _CR_ShadowTint, _CR_AmbientSky / _CR_AmbientEquator / _CR_AmbientGround (linear, with the
// strengths already applied). Presets (material inspector values): Default, Rock/Cliff (top tint), Foliage (alpha clip,
// translucency, wind, LOD dither cross-fade), Character.
Shader "CampanhaRio/SoftToon"
{
    Properties
    {
        [Header(Albedo)]
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        [Toggle] _UseVertexColor ("Multiply Vertex Color (sRGB)", Float) = 0
        _GradientBottom ("Gradient Bottom Tint", Color) = (1, 1, 1, 1)
        _GradientTop ("Gradient Top Tint", Color) = (1, 1, 1, 1)
        _GradientHeights ("Gradient Heights (x bottom, y top, object space m)", Vector) = (0, 1, 0, 0)

        [Header(Soft light)]
        _Wrap ("Wrap (0 Lambert .. 1 full wrap)", Range(0, 1)) = 0.5
        _RampCenter ("Ramp Center (on wrapped N.L)", Range(0, 1)) = 0.45
        _RampSoftness ("Ramp Softness", Range(0.01, 0.5)) = 0.28
        _ReceiveShadows ("Received Shadow Strength", Range(0, 1)) = 0.85
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.16
        _RimPower ("Rim Power", Range(0.5, 8)) = 3.5

        [Header(Rock and Cliff)]
        _TopTint ("Top Tint (moss, dust)", Color) = (1, 1, 1, 1)
        _TopTintAmount ("Top Tint Amount", Range(0, 1)) = 0
        _TopTintSharpness ("Top Tint Spread", Range(0.05, 0.5)) = 0.35

        [Header(Foliage)]
        [Toggle(_ALPHATEST_ON)] _AlphaClip ("Alpha Clip", Float) = 0
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _Translucency ("Translucency (backlight)", Range(0, 1)) = 0
        _AOStrength ("Contact AO Strength", Range(0, 1)) = 1
        [Toggle(_GROUND_TINT)] _GroundTintOn ("Ground Tint (per instance, grass)", Float) = 0
        [Toggle(_WIND)] _Wind ("Wind Sway (vertex colour alpha = weight)", Float) = 0
        _WindStrength ("Wind Strength (m)", Range(0, 0.5)) = 0.08
        _WindSpeed ("Wind Speed", Range(0, 4)) = 1.2

        [HideInInspector] _Preset ("Preset", Float) = 0
        [HideInInspector] _Cull ("Cull", Float) = 2
        // Boats: stencil bit 1 marks the hull, so the river water drawn over it skips the shore foam (RiverWater.shader)
        [HideInInspector] _StencilRef ("Stencil Ref", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 0
        [HideInInspector] _StencilComp ("Stencil Comp (0 = off)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor, _GradientBottom, _GradientTop, _TopTint;
            float4 _GradientHeights;
            half _UseVertexColor, _Wrap, _RampCenter, _RampSoftness, _ReceiveShadows, _RimStrength, _RimPower;
            half _TopTintAmount, _TopTintSharpness, _Cutoff, _Translucency, _WindStrength, _WindSpeed, _Preset, _Cull, _AOStrength;
        CBUFFER_END

        // Gentle sway: weight from the vertex colour ALPHA (0 at the trunk, 1 at the tips; RGB is the colour), phase from the world position
        float3 ApplyWind(float3 positionOS, half4 color)
        {
            #if defined(_WIND)
                float3 world = TransformObjectToWorld(positionOS);
                float phase = _Time.y * _WindSpeed + dot(world.xz, float2(0.35, 0.21));
                float3 sway = float3(sin(phase), 0, cos(phase * 0.8)) * _WindStrength * color.a;
                positionOS += TransformWorldToObjectDir(sway, false);
            #endif
            return positionOS;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            Stencil { Ref [_StencilRef] WriteMask [_StencilWriteMask] Comp [_StencilComp] Pass Replace } // off unless a material turns it on (only the boats)

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _WIND
            #pragma shader_feature_local _GROUND_TINT
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

            float4 _CR_ShadowTint, _CR_AmbientSky, _CR_AmbientEquator, _CR_AmbientGround;

            // The grass: each instance takes the colour of the ground it grows from (GrassField sets it per instance)
            #if defined(_GROUND_TINT)
                UNITY_INSTANCING_BUFFER_START(GroundTint)
                    UNITY_DEFINE_INSTANCED_PROP(float4, _GroundTint)
                UNITY_INSTANCING_BUFFER_END(GroundTint)
            #endif

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half4 color : TEXCOORD3;
                float heightOS : TEXCOORD4;
                half fog : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 pos = ApplyWind(input.positionOS.xyz, input.color);
                VertexPositionInputs p = GetVertexPositionInputs(pos);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.color = input.color;
                o.heightOS = input.positionOS.y;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(i.positionCS);
                #endif
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                #if defined(_ALPHATEST_ON)
                    clip(tex.a * _BaseColor.a - _Cutoff);
                #endif

                float3 N = normalize(i.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light sun = GetMainLight(shadowCoord, i.positionWS, half4(1, 1, 1, 1));

                // Albedo
                half3 albedo = tex.rgb * _BaseColor.rgb;
                if (_UseVertexColor > 0.5) albedo *= SRGBToLinear(i.color.rgb);
                #if defined(_GROUND_TINT) && defined(UNITY_INSTANCING_ENABLED)
                    albedo *= UNITY_ACCESS_INSTANCED_PROP(GroundTint, _GroundTint).rgb;
                #endif
                half g = saturate((i.heightOS - _GradientHeights.x) / max(_GradientHeights.y - _GradientHeights.x, 1e-4));
                albedo *= lerp(_GradientBottom.rgb, _GradientTop.rgb, g);
                half up = N.y;
                half topT = smoothstep(1.0 - _TopTintSharpness * 2.0, 1.0, up) * _TopTintAmount;
                albedo *= lerp(half3(1, 1, 1), _TopTint.rgb, topT);

                // Soft light: wrap, a wide smooth ramp, received shadows softened
                half ndl = dot(N, sun.direction);
                half wrapped = (ndl + _Wrap) / (1.0 + _Wrap);
                half ramp = smoothstep(_RampCenter - _RampSoftness, _RampCenter + _RampSoftness, wrapped);
                // Received shadows fade out at the terminator (self-shadow acne lives there; the ramp is dark there anyway)
                half nearLit = smoothstep(0.0, 0.05, ndl);
                half received = lerp(1.0, lerp(1.0, sun.shadowAttenuation, nearLit), _ReceiveShadows);
                half light = ramp * received;
                half3 direct = sun.color * light + _CR_ShadowTint.rgb * (1.0 - light);

                // Ambient: sky / equator / ground gradient by the normal
                half3 ambient = _CR_AmbientEquator.rgb + (_CR_AmbientSky.rgb - _CR_AmbientEquator.rgb) * saturate(up)
                              + (_CR_AmbientGround.rgb - _CR_AmbientEquator.rgb) * saturate(-up);
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.positionCS));
                    ambient *= lerp(1.0, ao.indirectAmbientOcclusion, _AOStrength); // contact darkening (soft SSAO): under and around objects
                    direct *= lerp(1.0, ao.directAmbientOcclusion, _AOStrength);   // a little on the sunlit side too (URP's Direct Lighting Strength)
                #endif

                half3 color = albedo * (direct + ambient);

                // Rim: a soft light edge, stronger on the lit side
                half rim = pow(1.0 - saturate(dot(N, V)), _RimPower) * _RimStrength * (0.3 + 0.7 * light);
                color += sun.color * rim;

                // Foliage: light through the leaves when the sun is behind them
                half back = pow(saturate(dot(V, -sun.direction)), 4.0) * _Translucency;
                color += albedo * sun.color * back;

                color = MixFog(color, i.fog);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex VertShadow
            #pragma fragment FragDepth
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _WIND
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct VS { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };

            VS VertShadow(A input)
            {
                VS o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                float3 positionWS = TransformObjectToWorld(ApplyWind(input.positionOS.xyz, input.color));
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDir = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDir = _LightDirection;
                #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
                cs = ApplyShadowClamping(cs);
                o.positionCS = cs;
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half4 FragDepth(VS i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                #if defined(_ALPHATEST_ON)
                    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a - _Cutoff);
                #endif
                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(i.positionCS);
                #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex VertDepth
            #pragma fragment FragDepth
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _WIND
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct VS { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };

            VS VertDepth(A input)
            {
                VS o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                o.positionCS = TransformObjectToHClip(ApplyWind(input.positionOS.xyz, input.color));
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half4 FragDepth(VS i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                #if defined(_ALPHATEST_ON)
                    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a - _Cutoff);
                #endif
                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(i.positionCS);
                #endif
                return i.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex VertDN
            #pragma fragment FragDN
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _WIND
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct VS { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; UNITY_VERTEX_INPUT_INSTANCE_ID };

            VS VertDN(A input)
            {
                VS o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                o.positionCS = TransformObjectToHClip(ApplyWind(input.positionOS.xyz, input.color));
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            half4 FragDN(VS i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                #if defined(_ALPHATEST_ON)
                    clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a - _Cutoff);
                #endif
                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(i.positionCS);
                #endif
                return half4(NormalizeNormalPerPixel(i.normalWS), 0);
            }
            ENDHLSL
        }
    }
    CustomEditor "CampanhaRio.Editor.SoftToonGUI"
    FallBack Off
}
