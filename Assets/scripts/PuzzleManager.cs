using System.Collections;
using System.Collections.Generic;
using TMPro;
using DG.Tweening;
using UnityEngine;

public class PuzzleManager : MonoBehaviour
{
    public static PuzzleManager instance;

    [Header("Piece Count")]
    [SerializeField] int minPieces = 7;
    [SerializeField] int maxPieces = 12;

    [Header("Reference Image")]
    [Tooltip("Fraction of safe-area short side the reference image occupies")]
    [SerializeField] float refImageFraction = 0.85f;

    [Header("Reference Image Alpha")]
    [Range(0f, 1f)] public float dimAlpha = 0.35f;
    [Range(0f, 1f)] public float crackedAlpha = 1f;

    [Header("Spawn Scatter")]
    [Tooltip("How far outside the image pieces can land (world units)")]
    [SerializeField] float scatterPadding = 0.5f;

    [Header("Explosion")]
    public float explodeDuration = 0.65f;

    [Header("Scene References")]
    public Transform puzzleArea;
    public GameObject piecePrefab;
    public GameObject hintButton;
    public SpriteRenderer referenceImage;
    public TextMeshProUGUI levelText;

    List<Sprite> levelImages = new List<Sprite>();
    List<PuzzlePiece> spawnedPieces = new List<PuzzlePiece>();
    int currentLevel = 0;
    int rows, cols;
    Shader carvedShader;
    bool isTransitioning = false;
    // Safe-area world bounds (calculated once on Start)
    public Rect safeWorldRect;

    void Awake() { instance = this; }

    void Start()
    {
        CalculateSafeWorldRect();
        if (referenceImage != null) carvedShader = referenceImage.sharedMaterial.shader;
        if (hintButton == null) hintButton = GameObject.Find("Hint");
        
        levelImages.Clear();
        Sprite[] arr = Resources.LoadAll<Sprite>("PuzzleImage");
        foreach (var s in arr) levelImages.Add(s);
        Debug.Log($"Total levels found: {levelImages.Count}");
        
        LoadLevel();
    }

    void CalculateSafeWorldRect()
    {
        Camera cam = Camera.main;
        Rect safe = Screen.safeArea;
        Vector3 bl = cam.ScreenToWorldPoint(new Vector3(safe.xMin, safe.yMin, cam.nearClipPlane));
        Vector3 tr = cam.ScreenToWorldPoint(new Vector3(safe.xMax, safe.yMax, cam.nearClipPlane));
        safeWorldRect = new Rect(bl.x, bl.y, tr.x - bl.x, tr.y - bl.y);
    }

    public void LoadLevel()
    {
        isTransitioning = false;
        if (currentLevel >= levelImages.Count)
        {
            Debug.Log($"All levels completed or no levels found. Current Level: {currentLevel}, Total Images: {levelImages.Count}");
            if (UIManager.Instance != null) UIManager.Instance.ShowWinPanel();
            return;
        }

        SetRandomGrid();
        levelText.text = "Level " + (currentLevel + 1);
        Sprite spr = levelImages[currentLevel];

        float shortSide = Mathf.Min(safeWorldRect.width, safeWorldRect.height);
        float worldSize = shortSide * refImageFraction;
        float ppu = spr.rect.width / worldSize;

        Sprite refSpr = Sprite.Create(spr.texture, spr.rect, new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
        referenceImage.sprite = refSpr;
        referenceImage.transform.localScale = Vector3.one;
        referenceImage.gameObject.layer = 2; // Ignore Raycast

        // Ensure background doesn't block pieces
        GameObject bg = GameObject.Find("Background");
        if (bg != null)
        {
            bg.layer = 2;
            bg.transform.position = new Vector3(bg.transform.position.x, bg.transform.position.y, 20f);
            foreach (var c in bg.GetComponents<Collider2D>()) c.enabled = false;
        }

        foreach (var c in referenceImage.GetComponents<Collider2D>()) c.enabled = false;

        referenceImage.transform.position = new Vector3(
            safeWorldRect.x + safeWorldRect.width * 0.5f,
            safeWorldRect.y + safeWorldRect.height * 0.5f,
            10f);

        SetRefAlpha(crackedAlpha);
        referenceImage.sortingOrder = 0;
        if (hintButton != null) hintButton.SetActive(true);
        
        // Restore the carved shader for the new level
        if (referenceImage != null && carvedShader != null)
        {
            referenceImage.material.shader = carvedShader;
        }

        // Reset reference image shader properties for the new level
        if (referenceImage.material != null)
        {
            referenceImage.material.SetFloat("_Opacity", 0.6f);
            referenceImage.material.SetFloat("_Saturation", 0.5f);
            referenceImage.material.SetFloat("_Contrast", 0.4f);
            referenceImage.material.SetFloat("_BlendStrength", 0.3f);
            referenceImage.material.SetFloat("_InnerShadowStr", 1.0f);
            referenceImage.material.SetFloat("_EngraveDepth", 0.5f);
        }

        GeneratePuzzle(spr, ppu);
    }

    void SetRandomGrid()
    {
        var configs = new List<Vector2Int>
        {
            new Vector2Int(2, 4), new Vector2Int(4, 2), // 8
            new Vector2Int(3, 3),                       // 9
            new Vector2Int(2, 5), new Vector2Int(5, 2), // 10
            new Vector2Int(2, 6), new Vector2Int(6, 2), // 12
            new Vector2Int(3, 4), new Vector2Int(4, 3), // 12
        };
        configs.RemoveAll(c => c.x * c.y < minPieces || c.x * c.y > maxPieces);
        if (configs.Count == 0) configs.Add(new Vector2Int(2, 3));
        Vector2Int chosen = configs[Random.Range(0, configs.Count)];
        rows = chosen.x; cols = chosen.y;
    }

    void GeneratePuzzle(Sprite spr, float ppu)
    {
        foreach (Transform child in puzzleArea) Destroy(child.gameObject);
        spawnedPieces.Clear();
        Vector3 imgCenter = referenceImage.transform.position;
        List<PieceData> pieces = ImageSlicer.Instance.SliceImage(spr, rows, cols, ppu, imgCenter);

        foreach (PieceData d in pieces)
        {
            GameObject obj = Instantiate(piecePrefab, puzzleArea);
            PuzzlePiece piece = obj.GetComponent<PuzzlePiece>();
            piece.SetInteractable(false);
            
            // Move pieces to front (Z = 0)
            Vector3 pos = d.correctWorldPos;
            pos.z = 0f;
            obj.transform.position = pos;
            
            // Snap target also at Z = 0
            d.correctWorldPos.z = 0f; 
            
            piece.Initialize(d);
            spawnedPieces.Add(piece);
        }
        StartCoroutine(CrackThenExplode(imgCenter));
    }

    IEnumerator CrackThenExplode(Vector3 center)
    {
        yield return new WaitForSeconds(0.55f);
        SetRefAlpha(dimAlpha);
        int n = spawnedPieces.Count;
        Vector3[] start = new Vector3[n];
        Vector3[] end = new Vector3[n];
        float xMin = safeWorldRect.xMin + scatterPadding;
        float xMax = safeWorldRect.xMax - scatterPadding;
        float yMin = safeWorldRect.yMin + scatterPadding;
        float yMax = safeWorldRect.yMax - scatterPadding;
        float hw = referenceImage.sprite.bounds.extents.x;
        float hh = referenceImage.sprite.bounds.extents.y;

        for (int i = 0; i < n; i++)
        {
            start[i] = spawnedPieces[i].transform.position;
            float tx = 0f, ty = 0f;
            int side = Random.Range(1, 4);
            switch (side)
            {
                case 0: tx = Random.Range(xMin, xMax); ty = Mathf.Min(center.y + hh + Random.Range(0.8f, 1f), yMax); break;
                case 1: tx = Mathf.Min(center.x + hw + Random.Range(0.8f, 1.6f), xMax); ty = Random.Range(yMin, center.y + hh); break;
                case 2: tx = Mathf.Max(center.x - hw - Random.Range(0.8f, 1.6f), xMin); ty = Random.Range(yMin, center.y + hh); break;
                default: tx = Random.Range(xMin, xMax); ty = Mathf.Max(center.y - hh - Random.Range(0.8f, 1.6f), yMin); break;
            }
            end[i] = new Vector3(tx, ty, 0f);
        }

        float elapsed = 0f;
        while (elapsed < explodeDuration)
        {
            elapsed += Time.deltaTime;
            float t = 1f - Mathf.Pow(1f - (elapsed / explodeDuration), 3f);
            for (int i = 0; i < n; i++) spawnedPieces[i].transform.position = Vector3.LerpUnclamped(start[i], end[i], t);
            yield return null;
        }

        for (int i = 0; i < n; i++) spawnedPieces[i].transform.position = end[i];
        foreach (var p in spawnedPieces) p.SetInteractable(true);
    }

    public void CheckPuzzleComplete()
    {
        if (isTransitioning) return;
        foreach (var p in spawnedPieces) if (!p.IsPlaced) return;
        
        isTransitioning = true;
        SetRefAlpha(1f);

        // Swap to a standard sprite shader for maximum clarity and brightness
        if (referenceImage != null)
        {
            referenceImage.material.shader = Shader.Find("Sprites/Default");
            referenceImage.color = Color.white;
            referenceImage.sortingOrder = 50; // Ensure it's on top of everything
        }

        if (hintButton != null) hintButton.SetActive(false);

        // Smoothly hide all pieces
        foreach (var p in spawnedPieces)
        {
            foreach (var sr in p.GetComponentsInChildren<SpriteRenderer>())
            {
                sr.DOFade(0f, 0.5f).SetEase(Ease.OutQuad);
            }
            // Deactivate after fading without changing scale
            DOVirtual.DelayedCall(0.5f, () => p.gameObject.SetActive(false));
        }

        Invoke(nameof(NextLevel), 2.5f);
    }

    void NextLevel() { currentLevel++; LoadLevel(); }

    void SetRefAlpha(float a)
    {
        if (referenceImage == null) return;
        Color c = referenceImage.color;
        c.a = a;
        referenceImage.color = c;
    }

    public void UseHint()
    {
        List<PuzzlePiece> unplaced = spawnedPieces.FindAll(p => !p.IsPlaced);
        Debug.Log($"Hint requested. Unplaced pieces found: {unplaced.Count}");
        if (unplaced.Count > 0)
        {
            PuzzlePiece randomPiece = unplaced[Random.Range(0, unplaced.Count)];
            randomPiece.SnapByHint();
        }
    }
}