Shader "HD2D/SpriteBillboard"
{
    Properties
    {
        // Main texture
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
        
        // Outline
        [Toggle(_OUTLINE_ON)] _OutlineEnabled ("Enable Outline", Float) = 1
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width", Range(0, 10)) = 1.0
        _OutlineThreshold ("Outline Threshold", Range(0, 1)) = 0.5
        
        // Emission
        [Toggle(_EMISSION)] _EmissionEnabled ("Enable Emission", Float) = 0
        _EmissionColor ("Emission Color", Color) = (0,0,0,0)
        _EmissionIntensity ("Emission Intensity", Range(0, 10)) = 1.0
        _EmissionTex ("Emission Map", 2D) = "black" {}
        
        // Lighting
        [Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows ("Receive Shadows", Float) = 1
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 1.0
        _AmbientStrength ("Ambient Strength", Range(0, 1)) = 0.5
        
        // Rim lighting
        [Toggle(_RIM_LIGHTING)] _RimLighting ("Rim Lighting", Float) = 0
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 3.0
        
        // Animation
        _FlipX ("Flip X", Float) = 0
        _FlipY ("Flip Y", Float) = 0
        _UVOffset ("UV Offset", Vector) = (0,0,1,1)
        
        // Rendering
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
        _ZWrite ("Z Write", Float) = 0
        
        // Stencil for UI masking
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
    }
    
    SubShader
    {
        Tags 
        { 
            "Queue" = "Transparent" 
            "RenderType" = "TransparentCutout"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
            "DisableBatching" = "False"
        }
        
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        
        Cull [_Cull]
        Lighting Off
        ZWrite [_ZWrite]
        Blend [_SrcBlend] [_DstBlend]
        
        Pass
        {
            Name "HD2D_SPRITE"
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            
            // Shader features
            #pragma shader_feature_local _OUTLINE_ON
            #pragma shader_feature_local _EMISSION
            #pragma shader_feature_local _RECEIVE_SHADOWS
            #pragma shader_feature_local _RIM_LIGHTING
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 viewDirWS : TEXCOORD3;
                float4 color : COLOR;
                float fogFactor : TEXCOORD4;
                #if defined(_RECEIVE_SHADOWS)
                    float4 shadowCoord : TEXCOORD5;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            // Properties
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            TEXTURE2D(_EmissionTex);
            SAMPLER(sampler_EmissionTex);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MainTex_TexelSize;
                float4 _Color;
                float _Cutoff;
                
                float4 _OutlineColor;
                float _OutlineWidth;
                float _OutlineThreshold;
                
                float4 _EmissionColor;
                float _EmissionIntensity;
                
                float _ShadowStrength;
                float _AmbientStrength;
                
                float4 _RimColor;
                float _RimPower;
                
                float _FlipX;
                float _FlipY;
                float4 _UVOffset;
            CBUFFER_END
            
            // Instance data for batching
            #ifdef UNITY_INSTANCING_ENABLED
                UNITY_INSTANCING_BUFFER_START(Props)
                    UNITY_DEFINE_INSTANCED_PROP(float4, _SpriteColor)
                    UNITY_DEFINE_INSTANCED_PROP(float4, _SpriteUVOffset)
                UNITY_INSTANCING_BUFFER_END(Props)
            #endif
            
            // Billboard calculation
            float3 CalculateBillboardPosition(float3 localPos, float3 cameraForward, float3 cameraUp)
            {
                float3 right = normalize(cross(cameraUp, cameraForward));
                float3 up = cross(cameraForward, right);
                
                float3 worldPos = localPos.x * right + localPos.y * up;
                return worldPos;
            }
            
            Varyings vert(Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                
                // Get camera vectors for billboard
                float3 cameraForward = -UNITY_MATRIX_V[2].xyz;
                float3 cameraUp = UNITY_MATRIX_V[1].xyz;
                
                // Apply billboard transformation
                float3 billboardPos = CalculateBillboardPosition(IN.positionOS.xyz, cameraForward, cameraUp);
                float3 worldPos = TransformObjectToWorld(float3(0,0,0)) + billboardPos;
                
                OUT.positionWS = worldPos;
                OUT.positionCS = TransformWorldToHClip(worldPos);
                
                // Calculate normal (always facing camera)
                OUT.normalWS = -cameraForward;
                
                // UV coordinates with flipping and offset
                float2 uv = IN.uv;
                
                #ifdef UNITY_INSTANCING_ENABLED
                    float4 uvOffset = UNITY_ACCESS_INSTANCED_PROP(Props, _SpriteUVOffset);
                #else
                    float4 uvOffset = _UVOffset;
                #endif
                
                // Apply UV offset and scale for sprite atlas
                uv = uv * uvOffset.zw + uvOffset.xy;
                
                // Apply flipping
                if (_FlipX > 0.5) uv.x = 1.0 - uv.x;
                if (_FlipY > 0.5) uv.y = 1.0 - uv.y;
                
                OUT.uv = TRANSFORM_TEX(uv, _MainTex);
                
                // View direction
                OUT.viewDirWS = GetCameraPositionWS() - OUT.positionWS;
                
                // Vertex color
                #ifdef UNITY_INSTANCING_ENABLED
                    OUT.color = IN.color * UNITY_ACCESS_INSTANCED_PROP(Props, _SpriteColor);
                #else
                    OUT.color = IN.color * _Color;
                #endif
                
                // Fog
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);
                
                // Shadow coordinates
                #if defined(_RECEIVE_SHADOWS)
                    OUT.shadowCoord = TransformWorldToShadowCoord(OUT.positionWS);
                #endif
                
                return OUT;
            }
            
            // Outline detection using texture sampling
            float GetOutlineAlpha(float2 uv, float threshold)
            {
                float2 texelSize = _MainTex_TexelSize.xy * _OutlineWidth;
                
                float alpha = 0;
                
                // Sample surrounding pixels
                for (int x = -1; x <= 1; x++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        if (x == 0 && y == 0) continue;
                        
                        float2 offset = float2(x, y) * texelSize;
                        float4 sample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + offset);
                        alpha = max(alpha, sample.a);
                    }
                }
                
                return alpha > threshold ? 1.0 : 0.0;
            }
            
            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                
                // Sample main texture
                float4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                
                // Apply vertex color tint
                texColor *= IN.color;
                
                // Alpha cutoff for transparency
                clip(texColor.a - _Cutoff);
                
                // Initialize final color
                float3 finalColor = texColor.rgb;
                float finalAlpha = texColor.a;
                
                // Outline effect
                #if defined(_OUTLINE_ON)
                    float outlineAlpha = GetOutlineAlpha(IN.uv, _OutlineThreshold);
                    float centerAlpha = texColor.a;
                    
                    // If we're on the edge (outline but no center)
                    if (outlineAlpha > 0.5 && centerAlpha < _Cutoff)
                    {
                        finalColor = _OutlineColor.rgb;
                        finalAlpha = _OutlineColor.a * outlineAlpha;
                    }
                #endif
                
                // Lighting calculation
                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction);
                float3 normalWS = normalize(IN.normalWS);
                
                // Basic diffuse lighting
                float NdotL = saturate(dot(normalWS, lightDir));
                float3 diffuse = mainLight.color * NdotL;
                
                // Ambient lighting
                float3 ambient = _AmbientStrength * unity_AmbientSky.rgb;
                
                // Shadow attenuation
                float shadowAttenuation = 1.0;
                #if defined(_RECEIVE_SHADOWS)
                    shadowAttenuation = MainLightRealtimeShadow(IN.shadowCoord);
                    shadowAttenuation = lerp(1.0, shadowAttenuation, _ShadowStrength);
                #endif
                
                // Combine lighting
                float3 lighting = (diffuse * shadowAttenuation + ambient);
                finalColor *= lighting;
                
                // Rim lighting
                #if defined(_RIM_LIGHTING)
                    float3 viewDir = normalize(IN.viewDirWS);
                    float rim = 1.0 - saturate(dot(viewDir, normalWS));
                    rim = pow(rim, _RimPower);
                    finalColor += _RimColor.rgb * rim * _RimColor.a;
                #endif
                
                // Emission
                #if defined(_EMISSION)
                    float3 emission = SAMPLE_TEXTURE2D(_EmissionTex, sampler_EmissionTex, IN.uv).rgb;
                    emission *= _EmissionColor.rgb * _EmissionIntensity;
                    finalColor += emission;
                #endif
                
                // Apply fog
                finalColor = MixFog(finalColor, IN.fogFactor);
                
                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
        
        // Shadow caster pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off
            
            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShadowCasterPass.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            float4 _MainTex_ST;
            float _Cutoff;
            float _FlipX;
            float _FlipY;
            float4 _UVOffset;
            
            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                // Billboard transformation for shadow
                float3 cameraForward = -UNITY_MATRIX_V[2].xyz;
                float3 cameraUp = UNITY_MATRIX_V[1].xyz;
                float3 right = normalize(cross(cameraUp, cameraForward));
                float3 up = cross(cameraForward, right);
                
                float3 billboardPos = input.positionOS.x * right + input.positionOS.y * up;
                float3 worldPos = TransformObjectToWorld(float3(0,0,0)) + billboardPos;
                
                output.positionCS = TransformWorldToHClip(worldPos);
                
                // Apply shadow bias
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                
                // UV with flipping
                float2 uv = input.texcoord;
                uv = uv * _UVOffset.zw + _UVOffset.xy;
                if (_FlipX > 0.5) uv.x = 1.0 - uv.x;
                if (_FlipY > 0.5) uv.y = 1.0 - uv.y;
                output.uv = TRANSFORM_TEX(uv, _MainTex);
                
                return output;
            }
            
            half4 ShadowPassFragment(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                float4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                clip(texColor.a - _Cutoff);
                
                return 0;
            }
            ENDHLSL
        }
        
        // Depth only pass for depth texture
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            
            ZWrite On
            ColorMask 0
            Cull Off
            
            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 position : POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            float _Cutoff;
            
            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                output.positionCS = TransformObjectToHClip(input.position.xyz);
                output.uv = TRANSFORM_TEX(input.texcoord, _MainTex);
                
                return output;
            }
            
            half4 DepthOnlyFragment(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                
                float alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                clip(alpha - _Cutoff);
                
                return 0;
            }
            ENDHLSL
        }
    }
    
    FallBack "Sprites/Default"
    CustomEditor "HD2DSpriteBillboardShaderGUI"
}