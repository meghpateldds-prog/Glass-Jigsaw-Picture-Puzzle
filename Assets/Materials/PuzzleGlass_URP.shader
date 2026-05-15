Shader "Custom/PuzzleGlass_URP"
{
    Properties
    {
        _MainTex        ("Sprite Texture",      2D)      = "white" {}
        _Color          ("Tint",                Color)   = (1,1,1,1)

        // Glass controls
        _GlassAlpha     ("Glass Alpha",         Range(0,1)) = 0.82
        _FresnelPower   ("Fresnel Edge Power",  Range(0.5,8)) = 3.0
        _FresnelColor   ("Fresnel Edge Color",  Color)   = (0.85,0.95,1.0,1.0)
        _FresnelStr     ("Fresnel Strength",    Range(0,1)) = 0.55

        // Refraction / shimmer
        _RefractStr     ("Refraction Strength", Range(0,0.05)) = 0.012
        _RefractSpeed   ("Refraction Speed",    Range(0,2))    = 0.6

        // Procedural scratches
        _ScratchScale   ("Scratch Scale",       Range(1,40))   = 14.0
        _ScratchStr     ("Scratch Strength",    Range(0,1))    = 0.18

        // Chromatic aberration
        _ChromaStr      ("Chromatic Strength",  Range(0,0.02)) = 0.006
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "GlassPiece"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ── Textures ──────────────────────────────────────────────────────
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // ── Uniforms ──────────────────────────────────────────────────────
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float  _GlassAlpha;
                float  _FresnelPower;
                float4 _FresnelColor;
                float  _FresnelStr;
                float  _RefractStr;
                float  _RefractSpeed;
                float  _ScratchScale;
                float  _ScratchStr;
                float  _ChromaStr;
            CBUFFER_END

            // ── Structs ───────────────────────────────────────────────────────
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
                float2 worldXY     : TEXCOORD1;  // for procedural effects
            };

            // ── Tiny procedural helpers ───────────────────────────────────────

            // Pseudo-random hash
            float hash(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 19.19);
                return frac(p.x * p.y);
            }

            // Value noise (smooth)
            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);  // smoothstep

                float a = hash(i);
                float b = hash(i + float2(1,0));
                float c = hash(i + float2(0,1));
                float d = hash(i + float2(1,1));

                return lerp(lerp(a,b,u.x), lerp(c,d,u.x), u.y);
            }

            // Scratch pattern: thin bright lines using noise derivative
            float scratchPattern(float2 uv, float scale)
            {
                float2 p  = uv * scale;
                float  n1 = noise(p);
                float  n2 = noise(p + float2(0.01, 0.0));
                float  dn = abs(n1 - n2) * 80.0;     // amplify edge
                return saturate(1.0 - dn);            // thin bright lines
            }

            // Simple edge fresnel approximation from UV distance to 0.5 centre
            float fresnelEdge(float2 uv)
            {
                float2 d   = abs(uv - 0.5) * 2.0;   // 0 at centre, 1 at edge
                float  rim = pow(max(d.x, d.y), _FresnelPower);
                return saturate(rim);
            }

            // ── Vertex ────────────────────────────────────────────────────────
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color       = IN.color * _Color;
                OUT.worldXY     = mul(unity_ObjectToWorld, IN.positionOS).xy;
                return OUT;
            }

            // ── Fragment ──────────────────────────────────────────────────────
            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;

                // ── Refraction offset (animated wave) ─────────────────────────
                float  t       = _Time.y * _RefractSpeed;
                float2 waveUV  = uv * 3.5 + float2(t * 0.13, t * 0.07);
                float  waveN   = noise(waveUV) * 2.0 - 1.0;
                float2 refUV   = uv + waveN * _RefractStr;

                // ── Sample sprite with slight chromatic shift ─────────────────
                float2 shift   = float2(_ChromaStr, 0.0);
                float  sampleR = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, refUV + shift).r;
                float  sampleG = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, refUV        ).g;
                float  sampleB = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, refUV - shift).b;
                float  sampleA = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv           ).a;

                half4 col = half4(sampleR, sampleG, sampleB, sampleA);
                col      *= IN.color;

                // Discard fully transparent pixels (tight mesh sprites)
                clip(col.a - 0.01);

                // ── Scratches overlay ─────────────────────────────────────────
                float scratch = scratchPattern(uv, _ScratchScale);
                // Scratches only show on opaque pixels — brighten slightly
                col.rgb = lerp(col.rgb, col.rgb + 0.35, scratch * _ScratchStr * sampleA);

                // ── Fresnel edge highlight ────────────────────────────────────
                float rim = fresnelEdge(uv);
                col.rgb   = lerp(col.rgb, _FresnelColor.rgb, rim * _FresnelStr);

                // ── Final alpha: sprite alpha * glass alpha ───────────────────
                col.a = sampleA * _GlassAlpha;

                return col;
            }
            ENDHLSL
        }
    }

    // Fallback for non-URP (Built-in) — plain transparent sprite
    Fallback "Sprites/Default"
}
