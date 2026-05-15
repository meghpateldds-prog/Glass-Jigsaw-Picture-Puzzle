// ─────────────────────────────────────────────────────────────────────────────
// PuzzlePieceGlass.shader  —  "Shards of Memories" style
//
// VISUAL GOALS (mobile-safe, #pragma target 2.0)
//   • Frosted / translucent glass body          (_BaseAlpha, _FrostAmount)
//   • Fresnel rim glow (UV-distance approx)     (_FresnelPow, _FresnelStr)
//   • Top-left specular shine hotspot           (_ShineStr, _ShineSharpness)
//   • Ice-blue glass tint                       (_GlassColor)
//   • Subtle frost via UV wobble offset         (_FrostAmount)
//   • Crack edge darkening texture              (_CrackTex, _CrackStr)
//   • Overlap zone: brighter + more transparent (_OverlapActive, _RefWorldMin/Max)
//
// RENDER STATE
//   Transparent queue  ZWrite Off  Blend SrcAlpha OneMinusSrcAlpha  Cull Off
// ─────────────────────────────────────────────────────────────────────────────

Shader "Puzzle/PuzzlePieceGlass"
{
    Properties
    {
        // ── Sprite ────────────────────────────────────────────────────────────
        [PerRendererData] _MainTex  ("Sprite Texture",   2D)    = "white" {}
        _Color                      ("Tint",             Color) = (1,1,1,1)

        // ── Glass appearance ──────────────────────────────────────────────────
        _BaseAlpha      ("Base Alpha",          Range(0,1))   = 0.72
        _FrostAmount    ("Frost Amount",        Range(0,0.03))= 0.012
        _FresnelPow     ("Fresnel Sharpness",   Range(0.5,8)) = 2.8
        _FresnelStr     ("Fresnel Strength",    Range(0,1))   = 0.50
        _ShineStr       ("Shine Strength",      Range(0,1))   = 0.28
        _ShineSharpness ("Shine Sharpness",     Range(1,8))   = 3.5
        _GlassColor     ("Glass Tint",          Color)        = (0.68,0.88,1.0,1)

        // ── Crack texture (optional, white = no effect) ───────────────────────
        _CrackTex       ("Crack Overlay Tex",   2D)    = "white" {}
        _CrackStr       ("Crack Darkness",      Range(0,1))   = 0.55

        // ── Overlap / hover (set from C#) ─────────────────────────────────────
        _OverlapActive  ("Overlap Active",      Float)        = 0
        _RefWorldMin    ("Ref World Min",        Vector)       = (0,0,0,0)
        _RefWorldMax    ("Ref World Max",        Vector)       = (0,0,0,0)
        _BrightBoost    ("Overlap Bright Boost", Range(0,1))  = 0.35
        _AlphaDip       ("Overlap Alpha Dip",   Range(0,1))   = 0.40
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "RenderType"        = "Transparent"
            "IgnoreProjector"   = "True"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   2.0
            #include "UnityCG.cginc"

            // ── Uniforms ──────────────────────────────────────────────────────
            sampler2D _MainTex;
            float4    _MainTex_ST;
            fixed4    _Color;

            half  _BaseAlpha;
            half  _FrostAmount;
            half  _FresnelPow;
            half  _FresnelStr;
            half  _ShineStr;
            half  _ShineSharpness;
            fixed4 _GlassColor;

            sampler2D _CrackTex;
            half   _CrackStr;

            float  _OverlapActive;
            float4 _RefWorldMin;
            float4 _RefWorldMax;
            half   _BrightBoost;
            half   _AlphaDip;

            // ── Vertex I/O ────────────────────────────────────────────────────
            struct appdata_t
            {
                float4 vertex   : POSITION;
                float2 texcoord : TEXCOORD0;
                fixed4 color    : COLOR;
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float2 uv       : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                fixed4 color    : COLOR;
            };

            // ── Vertex ────────────────────────────────────────────────────────
            v2f vert(appdata_t v)
            {
                v2f o;
                o.pos      = UnityObjectToClipPos(v.vertex);
                o.uv       = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.color    = v.color * _Color;
                return o;
            }

            // ── Fragment ──────────────────────────────────────────────────────
            fixed4 frag(v2f i) : SV_Target
            {
                // 1. Frost UV wobble — two sine waves at irrational frequencies
                //    produce a low-frequency non-repeating shimmer that reads as
                //    frosted glass without any extra texture or GrabPass.
                float2 frostUV;
                frostUV.x = i.uv.x + sin(i.uv.y * 37.3 + i.uv.x * 11.7) * _FrostAmount;
                frostUV.y = i.uv.y + sin(i.uv.x * 41.1 + i.uv.y * 17.3) * _FrostAmount;

                fixed4 texFrost = tex2D(_MainTex, frostUV);
                fixed4 texSharp = tex2D(_MainTex, i.uv);

                // Keep alpha sharp (so shard silhouette is crisp) but colour is
                // the frosted sample (80% frosted, 20% sharp colour blend).
                fixed4 texCol;
                texCol.rgb = lerp(texFrost.rgb, texSharp.rgb, 0.2);
                texCol.a   = texSharp.a;           // sharp alpha = clean edges

                clip(texCol.a - 0.01);

                // 2. Tint
                fixed4 col = texCol * i.color;

                // 3. Glass colour — shift toward icy blue-white so every piece
                //    reads as a shard of translucent glass, not a flat sticker.
                col.rgb = lerp(col.rgb, col.rgb * _GlassColor.rgb, 0.35);
                col.rgb = saturate(col.rgb + 0.06);   // lift from dark wood bg

                // 4. Fresnel rim glow (2-D UV-distance approximation)
                //    edgeDist: 0 at UV centre, ~0.7 at corners
                float2 uv_c  = i.uv - 0.5;
                float  eDist = length(uv_c);
                float  fres  = pow(saturate(eDist * 1.41421), _FresnelPow);
                col.rgb += fres * _FresnelStr * fixed3(0.82, 0.93, 1.0);
                col.a   += fres * 0.10;

                // 5. Shine hotspot — diagonal top-left specular blob.
                //    UV(0,1) = top-left → brightest. UV(1,0) = bottom-right → dark.
                float shine = pow(saturate((1.0 - i.uv.x + i.uv.y) * 0.5),
                                  _ShineSharpness);
                col.rgb += shine * _ShineStr;

                // 6. Crack texture darkening.
                //    crackTex R channel: white = no crack, dark = crack edge.
                //    _CrackStr = 0 → multiplier stays 1.0, no effect.
                float crackR   = tex2D(_CrackTex, i.uv).r;
                float crackMul = 1.0 - (1.0 - crackR) * _CrackStr;
                col.rgb *= crackMul;

                // 7. Base transparency
                col.a *= _BaseAlpha;

                // 8. Overlap hover effect (UNITY_BRANCH skips for non-hovered)
                UNITY_BRANCH
                if (_OverlapActive > 0.5)
                {
                    bool inside = i.worldPos.x >= _RefWorldMin.x &&
                                  i.worldPos.x <= _RefWorldMax.x &&
                                  i.worldPos.y >= _RefWorldMin.y &&
                                  i.worldPos.y <= _RefWorldMax.y;
                    if (inside)
                    {
                        col.rgb = saturate(col.rgb + _BrightBoost);
                        col.a  *= (1.0 - _AlphaDip);
                    }
                }

                return col;
            }
            ENDCG
        }
    }

    FallBack "Sprites/Default"
}
