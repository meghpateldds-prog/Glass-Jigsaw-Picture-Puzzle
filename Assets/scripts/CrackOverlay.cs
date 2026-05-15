// ─────────────────────────────────────────────────────────────────────────────
// CrackOverlay.cs
//
// Attach to a child GameObject of the referenceImage SpriteRenderer.
// It shows a crack/fracture texture that progressively reveals itself as
// pieces snap into place — replicating the "Shards of Memories" look where
// the reassembled image appears to have crack lines between the glass shards.
//
// SETUP
//   1. Create a child GO under the referenceImage SR, name it "CrackOverlay".
//   2. Add a SpriteRenderer to it — assign a crack line sprite (white lines
//      on transparent; or dark lines on transparent for a dark-crack look).
//   3. Add this component.
//   4. In PuzzleManager.GeneratePuzzle(), call CrackOverlay.Instance.Reset()
//      after spawning pieces.
//   5. In PuzzlePiece.DoSnap() OnComplete, call
//      CrackOverlay.Instance.OnPieceSnapped().
// ─────────────────────────────────────────────────────────────────────────────

using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class CrackOverlay : MonoBehaviour
{
    public static CrackOverlay Instance;

    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("Crack reveal settings")]
    [Tooltip("Alpha the crack overlay reaches when ALL pieces are placed")]
    [Range(0f, 1f)][SerializeField] float maxAlpha = 0.75f;

    [Tooltip("Duration of the alpha fade-in per piece snap")]
    [SerializeField] float perSnapFadeDuration = 0.30f;

    [Tooltip("Duration of the final full-reveal fade when puzzle completes")]
    [SerializeField] float completeFadeDuration = 0.55f;

    [Tooltip("Sorting order. Should sit just above the reference image (ref=0 → crack=2)")]
    [SerializeField] int crackSortOrder = 2;

    [Header("Blend mode hint")]
    [Tooltip("If true, the crack SpriteRenderer uses the additive material so white lines glow.\n" +
             "If false, use a normal Transparent material with dark crack lines.")]
    [SerializeField] bool useAdditiveCracks = false;
    [SerializeField] Material additiveMaterial;     // Sprites/Default-Additive
    [SerializeField] Material standardMaterial;     // Sprites/Default

    // ── Private ───────────────────────────────────────────────────────────────
    SpriteRenderer crackSR;
    int totalPieces;
    int snappedPieces;
    Tweener fadeTween;

    // ─────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        Instance = this;
        crackSR  = GetComponent<SpriteRenderer>();
        crackSR.sortingOrder = crackSortOrder;
        crackSR.color        = new Color(1f, 1f, 1f, 0f);   // start invisible

        // Match this overlay's local transform to the parent (referenceImage)
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale    = Vector3.one;

        if (useAdditiveCracks && additiveMaterial != null)
            crackSR.material = additiveMaterial;
        else if (!useAdditiveCracks && standardMaterial != null)
            crackSR.material = standardMaterial;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Call from PuzzleManager.GeneratePuzzle() after spawning pieces.
    public void Reset(int pieceCount)
    {
        fadeTween?.Kill();
        totalPieces  = pieceCount;
        snappedPieces = 0;
        crackSR.color = new Color(1f, 1f, 1f, 0f);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Call from PuzzlePiece.DoSnap() OnComplete.
    public void OnPieceSnapped()
    {
        snappedPieces++;
        snappedPieces = Mathf.Min(snappedPieces, totalPieces);

        // Fade to the alpha that corresponds to the fraction of pieces placed.
        // E.g. half the pieces placed → crack is at 50% of maxAlpha.
        float targetAlpha = maxAlpha * ((float)snappedPieces / Mathf.Max(totalPieces, 1));

        fadeTween?.Kill();
        fadeTween = crackSR
            .DOFade(targetAlpha, perSnapFadeDuration)
            .SetEase(Ease.OutSine);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Call from PuzzleManager.CheckPuzzleComplete() when puzzle is fully solved.
    // Fades crack to full maxAlpha.
    public void OnPuzzleComplete()
    {
        fadeTween?.Kill();
        fadeTween = crackSR
            .DOFade(maxAlpha, completeFadeDuration)
            .SetEase(Ease.OutCubic);
    }

    // ─────────────────────────────────────────────────────────────────────────
    void OnDestroy()
    {
        fadeTween?.Kill();
    }
}
