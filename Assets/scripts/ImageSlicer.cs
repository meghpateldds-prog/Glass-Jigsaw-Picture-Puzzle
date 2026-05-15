using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PieceData
{
    public Sprite sprite;
    public Vector3 correctWorldPos;
    public List<Vector2> colliderPath;
}

public class ImageSlicer : MonoBehaviour
{
    public static ImageSlicer Instance;
    void Awake() { Instance = this; }

    // Minimum piece area as a fraction of average — pieces below this are merged
    const float MergeThreshold = 0.60f;

    public List<PieceData> SliceImage(Sprite original, int rows, int cols, float ppu, Vector3 imgWorldCenter)
    {
        int offX = Mathf.RoundToInt(original.rect.x);
        int offY = Mathf.RoundToInt(original.rect.y);
        int W = Mathf.RoundToInt(original.rect.width);
        int H = Mathf.RoundToInt(original.rect.height);
        int texW = original.texture.width;

        Vector2 imgWorldSize = new Vector2(W / ppu, H / ppu);
        Color32[] src = original.texture.GetPixels32();

        var seeds = BuildSeeds(W, H, rows, cols);
        var region = VoronoiAssign(W, H, seeds);

        var groups = new List<List<Vector2Int>>(seeds.Count);
        for (int i = 0; i < seeds.Count; i++) groups.Add(new List<Vector2Int>());
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                // Only include non-transparent pixels in the puzzle pieces
                if (src[(offY + y) * texW + (offX + x)].a > 10)
                {
                    groups[region[y * W + x]].Add(new Vector2Int(x, y));
                }
            }
        }

        groups = MergeSmallGroups(groups, seeds, W, H);
        var results = new List<PieceData>();

        foreach (var pixels in groups)
        {
            if (pixels.Count == 0) continue;

            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;
            foreach (var p in pixels)
            {
                if (p.x < minX) minX = p.x; if (p.y < minY) minY = p.y;
                if (p.x > maxX) maxX = p.x; if (p.y > maxY) maxY = p.y;
            }
            int tW = maxX - minX + 1;
            int tH = maxY - minY + 1;

            var texData = new Color32[tW * tH];
            foreach (var p in pixels)
                texData[(p.y - minY) * tW + (p.x - minX)] = src[(offY + p.y) * texW + (offX + p.x)];

            var tex = new Texture2D(tW, tH, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.SetPixels32(texData);
            tex.Apply();

            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tW, tH), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.Tight);

            float wx = imgWorldCenter.x + ((minX + tW * 0.5f) / W - 0.5f) * imgWorldSize.x;
            float wy = imgWorldCenter.y + ((minY + tH * 0.5f) / H - 0.5f) * imgWorldSize.y;

            results.Add(new PieceData
            {
                sprite = sprite,
                correctWorldPos = new Vector3(wx, wy, imgWorldCenter.z),
                colliderPath = BuildColliderPath(sprite, pixels, minX, minY, tW, tH, ppu)
            });
        }
        return results;
    }

    List<Vector2Int> BuildSeeds(int W, int H, int rows, int cols)
    {
        var seeds = new List<Vector2Int>(rows * cols);
        float cw = (float)W / cols;
        float ch = (float)H / rows;

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                int sx = Mathf.Clamp(Mathf.RoundToInt((c + 0.5f) * cw + Random.Range(-cw * 0.1f, cw * 0.1f)), 0, W - 1);
                int sy = Mathf.Clamp(Mathf.RoundToInt((r + 0.5f) * ch + Random.Range(-ch * 0.1f, ch * 0.1f)), 0, H - 1);
                seeds.Add(new Vector2Int(sx, sy));
            }
        return seeds;
    }

    int[] VoronoiAssign(int W, int H, List<Vector2Int> seeds)
    {
        int[] region = new int[W * H];
        for (int i = 0; i < region.Length; i++) region[i] = -1;
        var queue = new Queue<int>(seeds.Count * 4);

        for (int s = 0; s < seeds.Count; s++)
        {
            int idx = seeds[s].y * W + seeds[s].x;
            if (region[idx] != -1) continue;
            region[idx] = s;
            queue.Enqueue(idx);
        }

        int[] dx4 = { 1, -1, 0, 0 };
        int[] dy4 = { 0, 0, 1, -1 };

        while (queue.Count > 0)
        {
            int cur = queue.Dequeue();
            int cx = cur % W; int cy = cur / W;
            int owner = region[cur];
            for (int d = 0; d < 4; d++)
            {
                int nx = cx + dx4[d]; int ny = cy + dy4[d];
                if (nx < 0 || nx >= W || ny < 0 || ny >= H) continue;
                int nIdx = ny * W + nx;
                if (region[nIdx] != -1) continue;
                region[nIdx] = owner;
                queue.Enqueue(nIdx);
            }
        }
        return region;
    }

    List<List<Vector2Int>> MergeSmallGroups(List<List<Vector2Int>> groups, List<Vector2Int> seeds, int W, int H)
    {
        float avgArea = 0f;
        foreach (var g in groups) if (g.Count > 0) avgArea += g.Count;
        int nonEmptyCount = 0;
        foreach (var g in groups) if (g.Count > 0) nonEmptyCount++;
        
        avgArea /= Mathf.Max(nonEmptyCount, 1);
        float minArea = avgArea * 0.8f; // Target 80% of average

        bool anyMerge = true;
        while (anyMerge)
        {
            anyMerge = false;
            for (int i = 0; i < groups.Count; i++)
            {
                if (groups[i].Count == 0) continue;
                
                // Hard minimum of 100 pixels OR below the 80% threshold
                if (groups[i].Count >= 100 && groups[i].Count >= minArea) continue;
                int nearest = -1; float bestD = float.MaxValue;
                Vector2Int si = (i < seeds.Count) ? seeds[i] : groups[i][0];
                for (int j = 0; j < groups.Count; j++)
                {
                    if (j == i || groups[j].Count == 0) continue;
                    Vector2Int sj = (j < seeds.Count) ? seeds[j] : groups[j][0];
                    float d = Vector2Int.Distance(si, sj);
                    if (d < bestD) { bestD = d; nearest = j; }
                }
                if (nearest < 0) continue;
                groups[nearest].AddRange(groups[i]); groups[i].Clear();
                anyMerge = true;
                int nonEmpty = 0; float total = 0f;
                foreach (var g in groups) if (g.Count > 0) { total += g.Count; nonEmpty++; }
                avgArea = nonEmpty > 0 ? total / nonEmpty : 1f;
                minArea = avgArea * MergeThreshold;
            }
        }
        groups.RemoveAll(g => g.Count == 0);
        return groups;
    }

    List<Vector2> BuildColliderPath(Sprite sprite, List<Vector2Int> pixels, int minX, int minY, int tW, int tH, float ppu)
    {
        if (sprite.GetPhysicsShapeCount() > 0)
        {
            var shapePath = new List<Vector2>();
            sprite.GetPhysicsShape(0, shapePath);
            if (shapePath.Count >= 6) return shapePath;
        }

        Texture2D tex = sprite.texture;
        int tw = tex.width; int th = tex.height;
        Color32[] pix = tex.GetPixels32();
        bool[] opaque = new bool[tw * th];
        for (int i = 0; i < pix.Length; i++) opaque[i] = pix[i].a > 10;

        var border = new List<Vector2Int>(256);
        for (int y = 0; y < th; y++)
        {
            for (int x = 0; x < tw; x++)
            {
                if (!opaque[y * tw + x]) continue;
                bool isBorder = x == 0 || x == tw - 1 || y == 0 || y == th - 1 || !opaque[y * tw + (x - 1)] || !opaque[y * tw + (x + 1)] || !opaque[(y - 1) * tw + x] || !opaque[(y + 1) * tw + x];
                if (isBorder) border.Add(new Vector2Int(x, y));
            }
        }
        if (border.Count < 3) return FallbackBoundsPath(sprite, ppu);

        float cx = 0f, cy = 0f;
        foreach (var b in border) { cx += b.x; cy += b.y; }
        cx /= border.Count; cy /= border.Count;
        border.Sort((a, b2) => Mathf.Atan2(a.y - cy, a.x - cx).CompareTo(Mathf.Atan2(b2.y - cy, b2.x - cx)));

        const int MaxVerts = 64;
        int stride = Mathf.Max(1, border.Count / MaxVerts);
        float halfTW = tw * 0.5f; float halfTH = th * 0.5f;
        var path = new List<Vector2>();
        for (int i = 0; i < border.Count; i += stride)
        {
            path.Add(new Vector2((border[i].x - halfTW + 0.5f) / ppu, (border[i].y - halfTH + 0.5f) / ppu));
        }
        return path;
    }

    List<Vector2> FallbackBoundsPath(Sprite sprite, float ppu)
    {
        float hw = sprite.rect.width * 0.5f / ppu;
        float hh = sprite.rect.height * 0.5f / ppu;
        return new List<Vector2> { new Vector2(-hw, -hh), new Vector2(hw, -hh), new Vector2(hw, hh), new Vector2(-hw, hh) };
    }
}