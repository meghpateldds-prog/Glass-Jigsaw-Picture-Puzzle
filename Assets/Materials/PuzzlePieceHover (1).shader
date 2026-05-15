// Save to: Assets/Shaders/PuzzlePieceHover.shader
// Assign to a Material, then set that Material on the PuzzlePiece prefab's
// SpriteRenderer (replaces "New Material").
//
// How the hover effect works:
//   Each fragment knows its world-space position (passed through from the vertex
//   stage). When _OverlapActive == 1, we convert that world position into a UV
//   on the reference image rect (_RefWorldMin → _RefWorldMax). If the UV is
//   inside [0,1]^2 the fragment is "overlapping" the target zone, so we boost
//   brightness and reduce alpha. All other fragments are drawn unchanged.
//   Because this is a single pass with a cheap branch, it has negligible cost
//   on mobile GPUs.

Shader "Custom/PuzzlePieceHover"
{
    Properties
    {
        _MainTex       ("Sprite Texture",   2D)     = "white" {}
        _Color         ("Tint",             Color)  = (1,1,1,1)

        // ── Set at runtime by PuzzlePiece.cs ─────────────────────────────────
        // World-space corners of the reference image bounding box
        _RefWorldMin   ("Ref World Min XY", Vector) = (0,0,0,0)
        _RefWorldMax   ("Ref World Max XY", Vector) = (1,1,0,0)
        // 1 = hover active, 0 = normal rendering
        _OverlapActive ("Overlap Active",   Float)  = 0
        // Brightness boost applied to the overlapping region (0–1)
        _BrightBoost   ("Brightness Boost", Float)  = 0.35
        // Alpha multiplier reduction for the overlapping region (0 = opaque, 1 = invisible)
        _AlphaDip      ("Alpha Dip",        Float)  = 0.45
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "PuzzlePieceHover"

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ── Textures ─────────────────────────────────────────────────────
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // ── Per-material constants (URP CBuffer for SRP batcher) ─────────
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float4 _RefWorldMin;    // xy = bottom-left world corner
                float4 _RefWorldMax;    // xy = top-right  world corner
                float  _OverlapActive;
                float  _BrightBoost;
                float  _AlphaDip;
            CBUFFER_END

            // ── Vertex input / output ─────────────────────────────────────────
            struct Attributes
            {
                float4 posOS  : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;      // sprite vertex colour
            };

            struct Varyings
            {
                float4 posCS  : SV_POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
                float3 posWS  : TEXCOORD1;  // world-space position for overlap test
            };

            // ── Vertex shader ─────────────────────────────────────────────────
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.posCS = TransformObjectToHClip(IN.posOS.xyz);
                OUT.posWS = TransformObjectToWorld(IN.posOS.xyz);
                OUT.uv    = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color * _Color;
                return OUT;
            }

            // ── Fragment shader ───────────────────────────────────────────────
            half4 frag(Varyings IN) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color;

                // Discard fully transparent pixels (keeps alpha shape crisp)
                clip(col.a - 0.01);

                if (_OverlapActive > 0.5)
                {
                    // Convert this fragment's world XY to a UV on the reference
                    // image rect.  refUV == (0,0) at bottom-left, (1,1) at top-right.
                    float2 refSize = _RefWorldMax.xy - _RefWorldMin.xy;
                    float2 refUV   = (IN.posWS.xy - _RefWorldMin.xy) / refSize;

                    // Overlap = fragment lies inside the reference image bounds
                    bool inside = (refUV.x >= 0.0 && refUV.x <= 1.0 &&
                                   refUV.y >= 0.0 && refUV.y <= 1.0);

                    if (inside)
                    {
                        // Boost brightness of the overlapping region
                        col.rgb = saturate(col.rgb + _BrightBoost);
                        // Make the overlapping region semi-transparent
                        col.a  *= (1.0 - _AlphaDip);
                    }
                }

                return col;
            }
            ENDHLSL
        }
    }

    // Fallback for non-URP projects — renders as a plain sprite
    Fallback "Sprites/Default"
}
