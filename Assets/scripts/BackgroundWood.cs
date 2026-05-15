// ─────────────────────────────────────────────────────────────────────────────
// BackgroundWood.cs
//
// Attach to a full-screen background SpriteRenderer (set to the lowest sorting
// order, e.g. -10). It sizes the sprite to fill the safe area and applies a
// tiling UV offset so the wood texture tiles naturally across any screen size.
//
// SETUP
//   1. Create a GameObject "WoodBackground" in the scene.
//   2. Add a SpriteRenderer. Assign a wood texture sprite.
//   3. Set sorting layer to background, order -10.
//   4. Add this component.
//   5. Optionally assign a Material that has tiling support (or use default
//      Sprites/Default — tiling is handled via transform scale here).
//
// The script scales the SpriteRenderer transform so the sprite exactly fills
// the Camera's safe-area world rect, then repeats the texture by using
// material tiling (mainTextureScale) when a tiling-capable material is used,
// OR by scaling the sprite and setting drawMode = Tiled on the SpriteRenderer.
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class BackgroundWood : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("Wood look")]
    [Tooltip("Dark rich brown — the table colour visible at the edges")]
    [SerializeField] Color woodTint = new Color(0.14f, 0.14f, 0.15f, 1f);

    [Tooltip("How many times the wood texture tiles across the screen width")]
    [SerializeField] float tileFactor = 3.5f;

    [Header("Vignette overlay (optional)")]
    [Tooltip("If assigned, this SpriteRenderer is sized to screen and tinted to darken edges")]
    [SerializeField] SpriteRenderer vignetteOverlay;
    [SerializeField] Color vignetteTint = new Color(0f, 0f, 0f, 0.42f);

    // ── Private ───────────────────────────────────────────────────────────────
    SpriteRenderer sr;

    // ─────────────────────────────────────────────────────────────────────────
    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        sr.color = woodTint;

        // Enable tiling mode so the sprite repeats rather than stretches.
        sr.drawMode   = SpriteDrawMode.Tiled;
        sr.tileMode   = SpriteTileMode.Continuous;

        FitToScreen();

        if (vignetteOverlay != null)
        {
            vignetteOverlay.color = vignetteTint;
            FitSRToScreen(vignetteOverlay);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    void FitToScreen()
    {
        Camera cam = Camera.main;
        float height = cam.orthographicSize * 2f;
        float width  = height * cam.aspect;

        // With drawMode=Tiled, size sets the world-space bounding box of the
        // renderer, and Unity repeats the sprite within it.
        sr.size = new Vector2(width, height);
        transform.position = new Vector3(0f, 0f, 1f);   // just behind everything

        // Tiling: we want the texture to repeat tileFactor times across the width.
        // With SpriteTileMode.Continuous the tiling is automatic from the size
        // vs. the sprite's native size in world units.
        // Force a consistent world-unit sprite size via pixels-per-unit if needed:
        // sr.sprite.pixelsPerUnit is read-only at runtime, so instead we just let
        // the Tiled draw mode handle repetition — no further code required.
        // (If the sprite PPU is set to 100 and the screen is 10 world units wide,
        // a sprite that is 10/tileFactor world units wide will tile tileFactor times.)

        // Optional: set material mainTextureScale as a backup for non-Tiled materials
        if (sr.sharedMaterial != null && sr.drawMode != SpriteDrawMode.Tiled)
        {
            sr.sharedMaterial.mainTextureScale = new Vector2(tileFactor, tileFactor * height / width);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    void FitSRToScreen(SpriteRenderer target)
    {
        Camera cam = Camera.main;
        float height = cam.orthographicSize * 2f;
        float width  = height * cam.aspect;
        target.drawMode = SpriteDrawMode.Sliced;
        target.size     = new Vector2(width, height);
        target.transform.position = Vector3.zero;
    }
}
