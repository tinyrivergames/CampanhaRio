// CampanhaRio river water (ported from CayaCozy; restyled later) (URP, transparent, receives shadows). Hand-written HLSL instead of Shader Graph:
// see Docs/TECH_DECISIONS.md. Parameter guide: Docs/WATER_GUIDE.md.
//
// Mesh data (from RiverMeshBuilder / WaterArea):
//   UV0.x  across the river (0 left bank .. 1 right bank)     UV0.y  meters along the river
//   UV1    (meters from the centerline, meters along)         -> isotropic world-scale texture coordinates
//   COLOR  R = current strength (0..1), G = obstacle foam, B = obstacle proximity (calm rings)
Shader "CampanhaRio/RiverWater"
{
    Properties
    {
        [Header(Depth and color)]
        _ShallowColor ("Shallow Color", Color) = (0.36, 0.81, 0.75, 1)
        _MidColor ("Mid Color", Color) = (0.18, 0.61, 0.6, 1)
        _DeepColor ("Deep Color", Color) = (0.09, 0.31, 0.35, 1)
        _DepthRange ("Depth Range (m to reach Deep)", Range(0.2, 6)) = 2.2
        _Clarity ("Clarity (m of visible bed)", Range(0.05, 4)) = 0.9
        _RefractionStrength ("Refraction Strength", Range(0, 0.1)) = 0.025

        [Header(Flow)]
        _FlowMaxSpeed ("Flow Max Speed (m/s, set by the mesh builder)", Float) = 6
        _FlowSpeedScale ("Flow Speed Scale", Range(0, 3)) = 1
        _FlowCycleLength ("Flow Cycle Length (s)", Range(0.5, 8)) = 3
        _CalmDrift ("Calm Drift (m/s)", Range(0, 0.3)) = 0.05
        [NoScaleOffset][Normal] _NormalTex ("Normal Noise", 2D) = "bump" {}
        _NormalStrengthCalm ("Normal Strength Calm", Range(0, 1)) = 0.12
        _NormalStrengthRapid ("Normal Strength Rapid", Range(0, 1.5)) = 0.75
        _SwellScale ("Swell Scale (m)", Range(1, 40)) = 11
        _RippleScale ("Ripple Scale (m)", Range(0.2, 8)) = 2.4

        [Header(Sky glints on ripples)]
        _GlintColor ("Glint Color", Color) = (0.91, 0.97, 0.96, 1)
        _GlintOpacity ("Glint Opacity", Range(0, 1)) = 0.6
        _GlintThreshold ("Glint Threshold (how much brighter sky a ripple must catch)", Range(0.02, 0.6)) = 0.16
        _GlintSharpness ("Glint Sharpness", Range(0, 1)) = 0.55
        _GlintAnisotropy ("Glint Anisotropy (stretch along the flow)", Range(0, 8)) = 3.5
        _GlintWarp ("Glint Warp (meandering)", Range(0, 3)) = 1.2
        _GlintScale ("Glint Ripple Size (m)", Range(0.3, 6)) = 2.2
        _GlintLifetimeScale ("Glint Lifetime Patch Size (m)", Range(2, 60)) = 14
        _GlintCoverage ("Glint Coverage", Range(0, 1)) = 0.45

        [Header(Legacy flow streaks)]
        [Toggle] _LegacyStreaks ("Legacy Texture Streaks (off = glints only)", Float) = 0
        [NoScaleOffset] _FoamTex ("Foam Texture (R streaks, G foam, B noise)", 2D) = "black" {}
        _StreakDensity ("Streak Density", Range(0, 1)) = 0.55
        _StreakLength ("Streak Length (m per tile)", Range(1, 60)) = 16
        _StreakWidth ("Streak Width (m per tile across)", Range(0.5, 40)) = 16
        _StreakOpacity ("Streak Opacity", Range(0, 1)) = 0.7
        _FoamColor ("Foam Color", Color) = (0.95, 0.98, 0.97, 1)
        _FoamShadowColor ("Foam Shadow Color", Color) = (0.75, 0.89, 0.88, 1)

        [Header(Shore and obstacle foam)]
        _ShoreFoamWidth ("Shore Foam Width (m)", Range(0, 1.5)) = 0.2
        _ShoreFoamNoise ("Shore Foam Noise", Range(0, 1)) = 0.85
        _ShoreFoamPulse ("Shore Foam Pulse", Range(0, 4)) = 1.2
        _ObstacleFoamStrength ("Obstacle Foam Strength", Range(0, 2)) = 0.8
        _RingStrength ("Calm Ring Strength", Range(0, 1)) = 0.35
        _BoilStrength ("Boils: lighter bulges in fast water", Range(0, 1)) = 0.45
        _BoilScale ("Boil Size (m)", Range(1, 20)) = 6
        _RapidsFoam ("Rapids Whitewater Patches", Range(0, 1)) = 0.6

        [Header(Reflection)]
        _ReflectionStrength ("Reflection Strength", Range(0, 1)) = 0.9
        _ReflectionDistortion ("Reflection Distortion", Range(0, 0.2)) = 0.025
        _ReflectionBreakup ("Reflection Breakup In Current", Range(0, 1)) = 0.65
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 3
        _SkyTopColor ("Sky Top (fallback)", Color) = (0.25, 0.5, 0.78, 1)
        _SkyHorizonColor ("Sky Horizon (fallback)", Color) = (0.66, 0.83, 0.93, 1)
        _ReflectionSmear ("Reflection Vertical Smear", Range(0, 0.03)) = 0.008

        [Header(Light)]
        _SunGlintColor ("Sun Highlight Color", Color) = (1, 0.96, 0.86, 1)
        _SunGlintSize ("Sun Highlight Size", Range(0.01, 1)) = 0.12
        _SunGlintStrength ("Sun Highlight Strength", Range(0, 2)) = 0.3
        _ShadowColor ("Shadow Tint", Color) = (0.44, 0.64, 0.65, 1)
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.6
        [Toggle(_CAUSTICS)] _Caustics ("Caustics", Float) = 1
        _CausticsStrength ("Caustics Strength", Range(0, 1)) = 0.35
        _CausticsScale ("Caustics Scale (m)", Range(0.3, 6)) = 1.6

        [Header(Waves)]
        _WobbleHeight ("Wobble Height (m, keep <= 0.05)", Range(0, 0.05)) = 0.025
        _WobbleScale ("Wobble Scale (m)", Range(0.5, 20)) = 5
        _WobbleSpeed ("Wobble Speed", Range(0, 4)) = 1

        [Header(Edges)]
        _EdgeSoftness ("Edge Softness (m)", Range(0.01, 0.5)) = 0.08
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ _CAUSTICS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            TEXTURE2D(_NormalTex); SAMPLER(sampler_NormalTex);
            TEXTURE2D(_FoamTex);   SAMPLER(sampler_FoamTex);
            TEXTURE2D(_CayaPlanarReflectionTex); SAMPLER(sampler_CayaPlanarReflectionTex);
            float4x4 _CayaReflectionVP;
            float _CayaReflectionEnabled;
            float3 _CayaReflectionCameraPos;
            float _CayaWaterDebug;
            float _CayaCausticsOff; // set by WaterQuality (0 = on)

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor, _MidColor, _DeepColor;
                float _DepthRange, _Clarity, _RefractionStrength;
                float _FlowMaxSpeed, _FlowSpeedScale, _FlowCycleLength, _CalmDrift;
                float _NormalStrengthCalm, _NormalStrengthRapid, _SwellScale, _RippleScale;
                float _StreakDensity, _StreakLength, _StreakWidth, _StreakOpacity;
                half4 _FoamColor, _FoamShadowColor;
                float _ShoreFoamWidth, _ShoreFoamNoise, _ShoreFoamPulse, _ObstacleFoamStrength, _RingStrength;
                float _ReflectionStrength, _ReflectionDistortion, _ReflectionBreakup, _FresnelPower;
                half4 _SkyTopColor, _SkyHorizonColor;
                half4 _SunGlintColor, _GlintColor;
                float _SunGlintSize, _SunGlintStrength;
                float _GlintOpacity, _GlintThreshold, _GlintSharpness, _GlintAnisotropy, _GlintWarp, _GlintScale, _GlintLifetimeScale, _GlintCoverage;
                float _LegacyStreaks, _ReflectionSmear, _RapidsFoam, _BoilStrength, _BoilScale;
                half4 _ShadowColor;
                float _ShadowStrength, _CausticsStrength, _CausticsScale;
                float _WobbleHeight, _WobbleScale, _WobbleSpeed, _EdgeSoftness;
                float _Caustics;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;   // flow direction (across, along), unit
                float4 uv3 : TEXCOORD3;   // features: wave pulse amplitude, pulse phase, chop, glassy
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 uvs : TEXCOORD1;   // uv0.xy, uv1.xy
                float4 color : TEXCOORD2;
                float eyeDepth : TEXCOORD3;
                float fogFactor : TEXCOORD4;
                float2 flowDir : TEXCOORD5;
                float2 detail : TEXCOORD6; // chop, glassy
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                // Gentle wobble for life: tiny so it never disagrees with gameplay's GetWaterHeight
                float t = _Time.y * _WobbleSpeed;
                float2 p = positionWS.xz / _WobbleScale;
                float wobble = sin(p.x * 6.2831 + t * 1.3) * cos(p.y * 5.1 + t) * 0.6 + sin((p.x + p.y) * 3.7 - t * 0.8) * 0.4;
                positionWS.y += wobble * _WobbleHeight * lerp(0.4, 1.0, input.color.r);
                // Standing waves breathe (RiverFeature.PulseRate = 1.4; gameplay uses the same formula)
                positionWS.y += input.uv3.x * sin(_Time.y * 1.4 + input.uv3.y);

                o.positionWS = positionWS;
                o.positionCS = TransformWorldToHClip(positionWS);
                o.uvs = float4(input.uv0, input.uv1);
                o.color = input.color;
                o.eyeDepth = -TransformWorldToView(positionWS).z;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                o.flowDir = input.uv2;
                o.detail = input.uv3.zw;
                return o;
            }

            // Two-phase flow: two samples offset by half a cycle and cross-faded, so per-vertex speed
            // differences never stretch or tear the texture over time.
            struct FlowPhases { float2 offset0, offset1, jump0, jump1; float weight0, weight1; };

            // velocity is in the UV1 frame (m/s across, m/s along), so the pattern follows the baked flow direction
            FlowPhases GetFlow(float2 velocity, float cycle)
            {
                FlowPhases f;
                float t = _Time.y / cycle;
                float phase0 = frac(t), phase1 = frac(t + 0.5);
                f.weight0 = 1.0 - abs(2.0 * phase0 - 1.0);
                f.weight1 = 1.0 - f.weight0;
                f.offset0 = velocity * cycle * phase0;
                f.offset1 = velocity * cycle * phase1;
                f.jump0 = float2(0.137, 0.371) * floor(t);        // hide the reset repeating in place
                f.jump1 = float2(0.291, 0.713) * floor(t + 0.5);
                return f;
            }

            float3 FlowNormal(float2 p, float scale, FlowPhases f)
            {
                float3 a = UnpackNormal(SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, (p - f.offset0) / scale + f.jump0));
                float3 b = UnpackNormal(SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, (p - f.offset1) / scale + f.jump1));
                return a * f.weight0 + b * f.weight1;
            }

            float4 FlowFoam(float2 uv0, float2 uv1, FlowPhases f)
            {
                return SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, uv0 + f.jump0) * f.weight0
                     + SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, uv1 + f.jump1) * f.weight1;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float R = saturate(input.color.r);   // current strength
                float G = saturate(input.color.g);   // obstacle foam
                float B = saturate(input.color.b);   // obstacle proximity
                float2 flowPos = input.uvs.zw;       // meters: (across from centerline, along)
                float t = _Time.y;

                float speed = R * _FlowMaxSpeed * _FlowSpeedScale;
                float2 flowDir = normalize(input.flowDir + float2(0, 1e-4));
                FlowPhases flow = GetFlow(flowDir * speed, _FlowCycleLength);
                FlowPhases rippleFlow = GetFlow(flowDir * speed * 1.35, _FlowCycleLength * 0.7);
                float2 drift = float2(t * _CalmDrift * 0.7, t * _CalmDrift);

                // ---------- surface normal: slow swells + fine ripples, calmer in the backwater
                float chop = saturate(input.detail.x), glassy = saturate(input.detail.y);
                float3 swell = FlowNormal(flowPos + drift, _SwellScale, flow);
                float3 ripple = FlowNormal(flowPos - drift * 1.7, _RippleScale * lerp(1.0, 0.5, chop), rippleFlow); // riffles: tiny choppy waves
                float choppiness = lerp(_NormalStrengthCalm, _NormalStrengthRapid, saturate(R + G * 0.8 + chop * 0.6));
                choppiness *= lerp(1.0, 0.25, glassy);                                                        // glassy tongue at a lip
                float2 slope = (swell.xy * 0.6 + ripple.xy) * choppiness;
                float3 normalWS = normalize(float3(slope.x, 1.0, slope.y));

                // ---------- depth
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float surfaceEye = input.eyeDepth;
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float thickness = max(0.0, sceneEye - surfaceEye);           // along the view ray
                float3 cameraPos = GetCameraPositionWS();
                float3 bedWS = cameraPos + (input.positionWS - cameraPos) * (sceneEye / max(surfaceEye, 1e-4));
                float verticalDepth = max(0.0, input.positionWS.y - bedWS.y);

                // ---------- refraction (never pulls in things that are above the water)
                float2 refractUV = screenUV + normalWS.xz * _RefractionStrength * saturate(verticalDepth * 2.0);
                if (LinearEyeDepth(SampleSceneDepth(refractUV), _ZBufferParams) < surfaceEye) refractUV = screenUV;
                float3 sceneColor = SampleSceneColor(refractUV);

                float depth01 = saturate(verticalDepth / _DepthRange);
                float3 depthColor = depth01 < 0.5 ? lerp(_ShallowColor.rgb, _MidColor.rgb, depth01 * 2.0)
                                                  : lerp(_MidColor.rgb, _DeepColor.rgb, depth01 * 2.0 - 1.0);
                float visibility = exp(-thickness / _Clarity);               // how much riverbed shows through
                float3 underwater = lerp(depthColor, sceneColor * lerp(1.0, _ShallowColor.rgb * 1.25, 0.45), visibility);

                // ---------- main light
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float shadow = lerp(1.0, mainLight.shadowAttenuation, _ShadowStrength);

                #if defined(_CAUSTICS)
                if (_CayaCausticsOff < 0.5)
                {
                    float2 bedUV = bedWS.xz / _CausticsScale;
                    float c1 = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, bedUV + float2(t * 0.031, t * 0.017)).b;
                    float c2 = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, bedUV * 1.37 - float2(t * 0.023, -t * 0.029) + 0.5).b;
                    float caustics = pow(saturate(1.0 - abs(c1 - c2) * 3.5), 5.0);
                    float shallowOnly = visibility * (1.0 - saturate(verticalDepth / 1.4));
                    underwater += mainLight.color * caustics * shallowOnly * shadow * _CausticsStrength;
                }
                #endif

                // ---------- reflection
                float3 viewDir = normalize(cameraPos - input.positionWS);
                float fresnel = 0.03 + 0.97 * pow(1.0 - saturate(dot(normalWS, viewDir)), _FresnelPower);
                float distortion = _ReflectionDistortion * (1.0 + 3.0 * R + 2.0 * G);
                float3 reflectDir = reflect(-viewDir, normalWS);
                float3 skyReflection = lerp(_SkyHorizonColor.rgb, _SkyTopColor.rgb, saturate(reflectDir.y * 1.5));
                float3 probe = DecodeHDREnvironment(SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectDir, 3), unity_SpecCube0_HDR);
                float3 reflection = lerp(skyReflection, probe, 0.4);
                bool planar = _CayaReflectionEnabled > 0.5 && distance(cameraPos, _CayaReflectionCameraPos) < 0.05;
                if (planar)
                {
                    float3 fallback = reflection;
                    float4 clip = mul(_CayaReflectionVP, float4(input.positionWS, 1.0));
                    float2 reflectUV = clip.xy / clip.w * 0.5 + 0.5;
                    reflectUV.x = 1.0 - reflectUV.x; // the reflection camera image is mirrored left-right
                    reflectUV.x += normalWS.x * distortion;          // horizontal wobble from the ripples
                    reflectUV.y += normalWS.z * distortion * 0.35;
                    // Vertical brush smear: a few taps along screen Y, like reflections painted with a wet brush
                    float smear = _ReflectionSmear * (1.0 + R * 1.5);
                    reflection = 0;
                    reflection += SAMPLE_TEXTURE2D(_CayaPlanarReflectionTex, sampler_CayaPlanarReflectionTex, reflectUV + float2(0, -2.0 * smear)).rgb * 0.12;
                    reflection += SAMPLE_TEXTURE2D(_CayaPlanarReflectionTex, sampler_CayaPlanarReflectionTex, reflectUV + float2(0, -1.0 * smear)).rgb * 0.22;
                    reflection += SAMPLE_TEXTURE2D(_CayaPlanarReflectionTex, sampler_CayaPlanarReflectionTex, reflectUV).rgb * 0.32;
                    reflection += SAMPLE_TEXTURE2D(_CayaPlanarReflectionTex, sampler_CayaPlanarReflectionTex, reflectUV + float2(0, 1.0 * smear)).rgb * 0.22;
                    reflection += SAMPLE_TEXTURE2D(_CayaPlanarReflectionTex, sampler_CayaPlanarReflectionTex, reflectUV + float2(0, 2.0 * smear)).rgb * 0.12;
                    // Near the edges of the reflection image (distortion pushes UVs out) fade to the sky/probe instead of clamped streaks
                    float2 inside = min(reflectUV, 1.0 - reflectUV);
                    float edge = saturate(min(inside.x, inside.y) * 25.0);
                    reflection = lerp(fallback, reflection, edge);
                }
                // Strong current breaks reflections up into water color and foam
                float reflectWeight = _ReflectionStrength * lerp(1.0, 1.0 - _ReflectionBreakup, saturate(R * 1.2 + G));
                float3 color = lerp(underwater, reflection, saturate(fresnel * reflectWeight));

                // Shadows tint toward deep teal, never black
                color *= lerp(_ShadowColor.rgb, 1.0, shadow);

                // Soft, broad painted sun highlight, strongest in calm water
                float3 smoothNormal = normalize(float3(swell.x * choppiness * 0.6, 1.0, swell.y * choppiness * 0.6));
                float3 halfDir = normalize(mainLight.direction + viewDir);
                float sunHighlight = smoothstep(1.0 - _SunGlintSize * 0.1, 1.0, saturate(dot(smoothNormal, halfDir)));
                color += _SunGlintColor.rgb * mainLight.color * sunHighlight * _SunGlintStrength * shadow * lerp(1.0, 0.35, R);

                // ---------- sky glints on ripples (the thin curved white marks of the reference)
                // A ripple that faces the viewer reflects a higher, brighter part of the sky than flat water:
                // that difference, thresholded softly, draws the mark. Ripples are stretched along the flow,
                // domain-warped so the marks meander, and a slow patch mask makes areas light up and fade.
                float2 warp = float2(SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, flowPos / 37.0 + t * 0.004).b,
                                     SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, flowPos / 29.0 - t * 0.005 + 0.31).b) - 0.5;
                // Glints live in a flow-aligned frame (y = along the local flow), so they stretch and curve with it
                float2 flowSide = float2(flowDir.y, -flowDir.x);
                float2 alignedPos = float2(dot(flowPos, flowSide), dot(flowPos, flowDir));
                float2 glintPos = alignedPos + warp * _GlintWarp * 6.0;
                float speedVariation = 1.0 + (SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, flowPos / 45.0 + 0.7).b - 0.5) * 0.6; // +-30%
                FlowPhases glintFlow = GetFlow(float2(0, speed * speedVariation * 1.1), _FlowCycleLength * 0.8);
                float stretch = 1.0 + _GlintAnisotropy * saturate(R * 1.3);                 // round in calm water, long in the flow
                float2 glintScale = float2(_GlintScale, _GlintScale * stretch);
                float3 ga = UnpackNormal(SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, (glintPos - glintFlow.offset0) / glintScale + glintFlow.jump0));
                float3 gb = UnpackNormal(SAMPLE_TEXTURE2D(_NormalTex, sampler_NormalTex, (glintPos - glintFlow.offset1) / glintScale + glintFlow.jump1));
                float2 glintSlope = (ga.xy * glintFlow.weight0 + gb.xy * glintFlow.weight1) * lerp(0.6, 1.2, saturate(R + 0.2));
                float3 rippleNormal = normalize(float3(glintSlope.x, 1.0, glintSlope.y));
                float lift = reflect(-viewDir, rippleNormal).y - reflect(-viewDir, float3(0, 1, 0)).y;
                float softness = lerp(0.12, 0.01, _GlintSharpness);
                float glint = smoothstep(_GlintThreshold, _GlintThreshold + softness, lift);

                float life = (SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, flowPos / _GlintLifetimeScale + float2(t * 0.013, -t * 0.009)).b
                            + SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, flowPos / (_GlintLifetimeScale * 0.61) + float2(-t * 0.017, t * 0.011) + 0.5).b) * 0.5;
                float lifeCut = lerp(0.62, 0.42, _GlintCoverage) - chop * 0.16;                     // riffles sparkle
                glint *= smoothstep(lifeCut, lifeCut + 0.08, life);                                  // most of the surface: no marks
                glint *= lerp(1.0, 0.35, saturate(input.eyeDepth / 45.0));                          // more near the viewer
                glint *= lerp(0.25, 1.0, saturate(R * 1.5));                                         // a gentle glint in the backwater
                glint *= shadow;                                                                     // none in shadow
                float flecks = step(0.55, SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, flowPos * 1.7).g);
                glint *= lerp(1.0, flecks, saturate(G * 2.0));                                       // broken into flecks near rocks
                float3 glintColor = lerp(reflection * 1.25 + 0.08, _GlintColor.rgb, 0.65);
                color = lerp(color, glintColor, saturate(glint * _GlintOpacity));

                // ---------- foam
                float4 foamNoise = FlowFoam((flowPos - flow.offset0 * 0.4) / 1.8, (flowPos - flow.offset1 * 0.4) / 1.8, flow);

                // Flow streaks: longer, denser and faster in strong current, rare and short in calm water
                float streakLength = _StreakLength * lerp(0.3, 1.6, R);
                float streak = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, float2(flowPos.x / _StreakWidth, (flowPos.y - flow.offset0.y) / streakLength) + flow.jump0).r * flow.weight0
                             + SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, float2(flowPos.x / _StreakWidth, (flowPos.y - flow.offset1.y) / streakLength) + flow.jump1).r * flow.weight1;
                float r2 = saturate(R * 1.1); r2 *= r2;  // sparse thin lines in normal current, dense foam only in rapids
                float density = _StreakDensity * lerp(0.1, 1.0, r2);
                float threshold = 1.0 - density;
                float bankFade = smoothstep(0.0, 0.08, input.uvs.x) * smoothstep(1.0, 0.92, input.uvs.x);
                float streakFoam = smoothstep(threshold, threshold + 0.07, streak) * _StreakOpacity * bankFade * saturate(R * 2.5 + 0.08) * _LegacyStreaks;

                // Shoreline: broken, pulsing, thicker in strong current
                float shoreWidth = _ShoreFoamWidth * lerp(0.6, 1.8, R);
                float shore = 1.0 - saturate(verticalDepth / max(shoreWidth, 1e-3));
                float pulse = sin(t * _ShoreFoamPulse + foamNoise.b * 6.2831) * 0.15;
                float shoreFoam = smoothstep(foamNoise.g * _ShoreFoamNoise, foamNoise.g * _ShoreFoamNoise + 0.25, shore + pulse) * step(0.001, shore);

                // Obstacles: cushion + V tail baked into G, broken up by the foam cells
                float obstacleFoam = smoothstep(0.25, 0.7, G * _ObstacleFoamStrength * (0.55 + foamNoise.g * 0.9));

                // Calm water: soft rings drifting out from rocks and posts (B = proximity)
                float ringDist = (1.0 - B) * 3.0;
                float ring = pow(saturate(sin((ringDist - t * 0.6) * 6.2831 / 0.9)), 12.0) * B * saturate(1.0 - R * 4.0) * _RingStrength;

                // Rapids: soft broken whitewater patches (not lines), only in the strongest current
                float rapids = saturate((R - 0.72) * 4.0);
                float rapidsFoam = smoothstep(0.5, 0.78, foamNoise.g * (0.75 + foamNoise.b * 0.5)) * rapids * _RapidsFoam;

                // Boils: soft round upwellings of lighter water drifting with the current
                float boilA = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, (flowPos - flow.offset0 * 0.5) / _BoilScale + flow.jump0).b;
                float boilB = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, (flowPos - flow.offset1 * 0.5) / _BoilScale + flow.jump1).b;
                float boil = smoothstep(0.6, 0.85, boilA * flow.weight0 + boilB * flow.weight1) * saturate((R - 0.5) * 3.0) * _BoilStrength;
                color = lerp(color, color * 1.15 + 0.035, boil);
                color = lerp(color, color * 1.1 + 0.03, glassy * 0.55); // glassy tongues (lips and the Stage 8 fast line) read as smooth, bright water

                float foam = saturate(max(max(max(streakFoam, shoreFoam), obstacleFoam), rapidsFoam) + ring * 0.6);
                float3 foamColor = lerp(_FoamShadowColor.rgb, _FoamColor.rgb, shadow * saturate(dot(normalWS, mainLight.direction) * 0.5 + 0.5));
                color = lerp(color, foamColor, foam);

                // ---------- debug views (F1 panel)
                if (_CayaWaterDebug > 0.5 && _CayaWaterDebug < 1.5)
                    color = R < 0.5 ? lerp(float3(0.1, 0.3, 1.0), float3(0.1, 0.9, 0.2), R * 2.0) : lerp(float3(0.1, 0.9, 0.2), float3(1.0, 0.15, 0.1), R * 2.0 - 1.0);
                else if (_CayaWaterDebug > 1.5)
                    color = lerp(float3(0.05, 0.2, 0.24), float3(1, 1, 1), foam);

                color = MixFog(color, input.fogFactor);
                float alpha = saturate(thickness / _EdgeSoftness) * input.color.a; // (vertex alpha: a channel fading into a lake, Stage 12)
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
