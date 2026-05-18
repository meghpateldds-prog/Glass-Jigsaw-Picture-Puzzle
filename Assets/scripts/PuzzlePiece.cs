using DG.Tweening;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(PolygonCollider2D))]
public class PuzzlePiece : MonoBehaviour
{
    public bool IsPlaced { get; private set; }

    [Header("Snap")]
    [SerializeField] float snapDistance = 0.6f;
    [SerializeField] float rotationTolerance = 22f;

    [Header("Input")]
    [SerializeField] float tapMaxMove = 0.18f;
    [SerializeField] float tapMaxTime = 0.25f;

    [Header("Rotation")]
    [SerializeField] float rotateDuration = 0.10f;

    [Header("Glow")]
    [SerializeField] Color glowColor = new Color(1f, 0.92f, 0.4f, 0.85f);
    [SerializeField] float glowDuration = 0.35f;

    [Header("Hover Highlight")]
    [SerializeField] float hoverDistanceMultiplier = 2f;
    [Range(0f, 1f)][SerializeField] float hoverBrightBoost = 0.35f;
    [Range(0f, 1f)][SerializeField] float hoverAlphaDip = 0.45f;

    [Header("Glass Look")]
    [Range(0f, 1f)][SerializeField] float glassBaseAlpha = 0.72f;
    [Range(0f, 1f)][SerializeField] float glassShineStr = 0.22f;

    Vector3 correctPos;
    int rotStep = 0;
    const int TotalSteps = 8;
    const float StepAngle = 45f;

    bool interactable = false;
    bool isDragging = false;
    bool isRotating = false;
    bool isSnapping = false;

    Vector3 mouseDownWorldPos;
    Vector3 dragStartPos;
    float mouseDownTime;

    SpriteRenderer sr;
    PolygonCollider2D col;
    SpriteRenderer glowSR;
    Material pieceMat;
    bool hoverActive = false;

    Tweener rotateTween;
    Sequence snapSeq;

    static readonly int ID_RefWorldMin = Shader.PropertyToID("_RefWorldMin");
    static readonly int ID_RefWorldMax = Shader.PropertyToID("_RefWorldMax");
    static readonly int ID_OverlapActive = Shader.PropertyToID("_OverlapActive");
    static readonly int ID_BrightBoost = Shader.PropertyToID("_BrightBoost");
    static readonly int ID_AlphaDip = Shader.PropertyToID("_AlphaDip");
    static readonly int ID_BaseAlpha = Shader.PropertyToID("_BaseAlpha");
    static readonly int ID_ShineStr = Shader.PropertyToID("_ShineStr");

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<PolygonCollider2D>();
    }

    void OnDestroy()
    {
        rotateTween?.Kill();
        snapSeq?.Kill();
    }

    public void Initialize(PieceData data)
    {
        sr.sprite = data.sprite;
        correctPos = data.correctWorldPos;
        IsPlaced = false;
        sr.sortingOrder = 5;

        pieceMat = new Material(sr.sharedMaterial);
        sr.material = pieceMat;
        pieceMat.SetFloat(ID_OverlapActive, 0f);
        pieceMat.SetFloat(ID_BrightBoost, hoverBrightBoost);
        pieceMat.SetFloat(ID_AlphaDip, hoverAlphaDip);
        pieceMat.SetFloat(ID_BaseAlpha, glassBaseAlpha);
        pieceMat.SetFloat(ID_ShineStr, glassShineStr);

        if (col != null && data.colliderPath != null && data.colliderPath.Count >= 3)
        {
            col.pathCount = 1;
            col.SetPath(0, data.colliderPath.ToArray());
            col.enabled = true;
        }

        CreateGlowOverlay(data.sprite);
        rotStep = Random.Range(1, TotalSteps);
        transform.localRotation = Quaternion.Euler(0f, 0f, rotStep * StepAngle);
    }

    void CreateGlowOverlay(Sprite spr)
    {
        var child = new GameObject("Glow");
        child.transform.SetParent(transform, false);
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;

        glowSR = child.AddComponent<SpriteRenderer>();
        glowSR.sprite = spr;
        glowSR.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
        glowSR.material = pieceMat;
        glowSR.sortingOrder = sr.sortingOrder + 1;
    }

    public void SetInteractable(bool value) { interactable = value; }

    void OnMouseDown()
    {
        if (!interactable || IsPlaced || isSnapping) return;
        mouseDownWorldPos = WorldMouse();
        dragStartPos = transform.position;
        mouseDownTime = Time.time;
        isDragging = false;
        sr.sortingOrder = 10;
        if (glowSR) glowSR.sortingOrder = 11;
    }

    void OnMouseDrag()
    {
        if (!interactable || IsPlaced || isSnapping) return;
        Vector3 cur = WorldMouse();
        float moved = Vector3.Distance(cur, mouseDownWorldPos);
        if (moved > tapMaxMove) isDragging = true;
        if (isDragging)
        {
            float z = transform.position.z;
            transform.position = new Vector3(cur.x, cur.y, z);
            UpdateHoverEffect();
        }
    }

    void OnMouseUp()
    {
        if (!interactable || IsPlaced || isSnapping) return;
        SetHoverActive(false);
        sr.sortingOrder = 5;
        if (glowSR) glowSR.sortingOrder = 6;
        float elapsed = Time.time - mouseDownTime;
        float moved = Vector3.Distance(WorldMouse(), mouseDownWorldPos);
        bool isTap = !isDragging && elapsed <= tapMaxTime && moved <= tapMaxMove;
        isDragging = false;
        if (isTap)
        {
            rotStep = (rotStep + 1) % TotalSteps;
            DoRotate(rotStep * StepAngle);
        }
        else
        {
            if (!TrySnap())
            {
                PushOffReference();
            }
        }
    }

    void DoRotate(float targetAngle)
    {
        rotateTween?.Kill();
        isRotating = true;
        Vector3 lockedWorldPos = transform.position;
        Vector3 targetLocalEuler = new Vector3(0f, 0f, targetAngle);
        rotateTween = transform
            .DOLocalRotate(targetLocalEuler, rotateDuration, RotateMode.Fast)
            .SetEase(Ease.OutCubic)
            .OnUpdate(() => transform.position = lockedWorldPos)
            .OnComplete(() =>
            {
                transform.position = lockedWorldPos;
                isRotating = false;
                TrySnap();
            });
    }

    void PushOffReference()
    {
        SpriteRenderer refSR = PuzzleManager.instance.referenceImage;
        if (refSR == null) return;

        Bounds b = refSR.bounds;


        Vector3 pos = transform.position;
        // Check only X and Y (ignore Z since they are at different depths)
        bool isOverImage = pos.x >= b.min.x && pos.x <= b.max.x &&
                           pos.y >= b.min.y && pos.y <= b.max.y;

        if (isOverImage)
        {
            Vector3 targetPos = dragStartPos;

            // Clamp within screen safe area
            Rect safe = PuzzleManager.instance.safeWorldRect;
            float padding = 0.5f;
            targetPos.x = Mathf.Clamp(targetPos.x, safe.xMin + padding, safe.xMax - padding);
            targetPos.y = Mathf.Clamp(targetPos.y, safe.yMin + padding, safe.yMax - padding);

            transform.DOMove(targetPos, 1.2f).SetEase(Ease.OutQuad);
        }
    }

    bool TrySnap()
    {
        if (IsPlaced || isRotating || isSnapping) return false;
        if (Vector2.Distance(transform.position, correctPos) > snapDistance) return false;

        float z = transform.localEulerAngles.z % 360f;
        if (z < 0f) z += 360f;
        if (!(z <= rotationTolerance || z >= 360f - rotationTolerance)) return false;

        DoSnap();
        return true;
    }

    void DoSnap()
    {
        SetHoverActive(false);
        isSnapping = true;
        IsPlaced = true;

        snapSeq?.Kill();
        snapSeq = DOTween.Sequence();

        // Slow and smooth placement: Move, Rotate, and Fade together
        float snapDur = 0.5f;
        snapSeq.Append(transform.DOMove(correctPos, snapDur).SetEase(Ease.OutQuad));
        snapSeq.Join(transform.DOLocalRotate(Vector3.zero, snapDur).SetEase(Ease.OutQuad));
        snapSeq.Join(sr.DOFade(glassBaseAlpha, snapDur).SetEase(Ease.OutQuad));

        snapSeq.OnComplete(() =>
        {
            sr.sortingOrder = 0;
            if (glowSR) glowSR.sortingOrder = 1;
            col.enabled = false;
            isSnapping = false;
            DoGlowFlash();
            PuzzleManager.instance.CheckPuzzleComplete();
        });
    }

    public void SnapByHint()
    {
        if (IsPlaced || isSnapping) return;

        SetHoverActive(false);
        isSnapping = true;
        IsPlaced = true;

        // Bring to front while moving for visibility
        sr.sortingOrder = 20;
        if (glowSR) glowSR.sortingOrder = 21;

        snapSeq?.Kill();
        snapSeq = DOTween.Sequence();

        // Slightly slower and smoother for hints
        float hintDur = 1.25f;
        snapSeq.Append(transform.DOMove(correctPos, hintDur).SetEase(Ease.OutQuad));
        snapSeq.Join(transform.DOLocalRotate(Vector3.zero, hintDur).SetEase(Ease.OutQuad));
        snapSeq.Join(sr.DOFade(glassBaseAlpha, hintDur).SetEase(Ease.OutQuad));

        snapSeq.OnComplete(() =>
        {
            sr.sortingOrder = 0;
            if (glowSR) glowSR.sortingOrder = 1;
            col.enabled = false;
            isSnapping = false;
            DoGlowFlash();
            PuzzleManager.instance.CheckPuzzleComplete();
        });
    }

    public void DoGlowFlash(float duration = -1f)
    {
        if (glowSR == null) return;
        float d = (duration > 0) ? duration : glowDuration;
        float half = d * 0.5f;
        glowSR.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
        Sequence glowSeq = DOTween.Sequence();
        glowSeq.Append(glowSR.DOFade(glowColor.a, half).SetEase(Ease.OutSine));
        glowSeq.Append(glowSR.DOFade(0f, half).SetEase(Ease.InSine));
        glowSeq.OnComplete(() => glowSR.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f));
    }

    void UpdateHoverEffect()
    {
        if (pieceMat == null || PuzzleManager.instance == null) return;
        float hoverRange = snapDistance * hoverDistanceMultiplier;
        bool near = Vector2.Distance(transform.position, correctPos) < hoverRange;
        if (near != hoverActive) SetHoverActive(near);
    }

    void SetHoverActive(bool active)
    {
        if (pieceMat == null || active == hoverActive) return;
        hoverActive = active;
        if (active)
        {
            SpriteRenderer refSR = PuzzleManager.instance.referenceImage;
            if (refSR != null)
            {
                Bounds b = refSR.bounds;
                pieceMat.SetVector(ID_RefWorldMin, new Vector4(b.min.x, b.min.y, 0f, 0f));
                pieceMat.SetVector(ID_RefWorldMax, new Vector4(b.max.x, b.max.y, 0f, 0f));
            }
            pieceMat.SetFloat(ID_OverlapActive, 1f);
        }
        else
        {
            pieceMat.SetFloat(ID_OverlapActive, 0f);
        }
    }

    Vector3 WorldMouse()
    {
        Vector3 mp = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mp.z = 0f;
        return mp;
    }
}
