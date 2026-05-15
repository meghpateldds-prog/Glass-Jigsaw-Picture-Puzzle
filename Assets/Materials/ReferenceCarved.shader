Shader "Custom/ReferenceCarved"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite Texture", 2D) = "white" {}
        [MainColor] _Color ("Tint", Color) = (1,1,1,1)
        
        [Header(Carve Settings)]
        _EngraveDepth ("Engrave Depth", Range(0, 1)) = 0.5
        _InnerShadowStr ("Inner Shadow Strength", Range(0, 2)) = 1.0
        _BevelSoftness ("Bevel Softness", Range(0.001, 0.05)) = 0.01
        _BevelStrength ("Bevel Strength", Range(0, 1)) = 0.3
        
        [Header(Image Look)]
        _Opacity ("Image Opacity", Range(0, 1)) = 0.6
        _Contrast ("Contrast Reduction", Range(0, 1)) = 0.4
        _Saturation ("Saturation", Range(0, 1)) = 0.5
        _BlendStrength ("Background Blend", Range(0, 1)) = 0.3
        
        [Header(Lighting)]
        _LightDir ("Light Direction", Vector) = (0.5, 0.5, 0, 0)
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

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

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
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _EngraveDepth;
                float _InnerShadowStr;
                float _BevelSoftness;
                float _BevelStrength;
                float _Opacity;
                float _Contrast;
                float _Saturation;
                float _BlendStrength;
                float2 _LightDir;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            // Simple contrast adjustment
            float3 Contrast(float3 color, float contrast)
            {
                return (color - 0.5) * contrast + 0.5;
            }

            // Simple saturation adjustment
            float3 Saturation(float3 color, float sat)
            {
                float luma = dot(color, float3(0.2126, 0.7152, 0.0722));
                return lerp(luma.xxx, color, sat);
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float4 mainTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                
                // --- Edge/Normal Estimation from Alpha ---
                // Sample alpha in 4 directions to create a gradient (pseudo-normal)
                float aU = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0, _BevelSoftness)).a;
                float aD = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0, -_BevelSoftness)).a;
                float aL = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(-_BevelSoftness, 0)).a;
                float aR = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(_BevelSoftness, 0)).a;
                
                float2 grad = float2(aR - aL, aU - aD);
                float edgeMask = saturate(length(grad) * 2.0);
                
                // --- Inner Shadow ---
                // Darken the inside edges by checking how much alpha is "missing" nearby
                float innerShadow = saturate((1.0 - mainTex.a) * edgeMask * _InnerShadowStr);
                
                // --- Bevel & Emboss (Recessed) ---
                // Normalize light direction and dot with gradient
                float2 lDir = normalize(_LightDir);
                float bevel = dot(grad, lDir) * _BevelStrength;
                
                // --- Color Processing ---
                float3 col = mainTex.rgb;
                
                // Apply Saturation and Contrast reductions
                col = Saturation(col, _Saturation);
                col = Contrast(col, 1.0 - _Contrast);
                
                // Blend with a "frosted/matte" base (neutral dark gray for depth)
                float3 matteBase = float3(0.2, 0.2, 0.2);
                col = lerp(col, matteBase, _BlendStrength);
                
                // --- Combine Effects ---
                // Apply recessed bevel (shadow on top-left, highlight on bottom-right for 'inward' look)
                col += bevel;
                
                // Apply inner shadow (darken the deep edges)
                col *= (1.0 - innerShadow * _EngraveDepth);
                
                // Apply ambient darkening around the very edge to 'seat' it
                float ambientEdge = saturate(edgeMask * 0.5);
                col *= (1.0 - ambientEdge * 0.2);
                
                // --- Final Opacity and Tint ---
                float finalAlpha = mainTex.a * _Opacity * input.color.a;
                float4 result = float4(col * input.color.rgb, finalAlpha);
                
                // Premultiply alpha for URP 2D
                result.rgb *= result.a;

                return result;
            }
            ENDHLSL
        }
    }
}
