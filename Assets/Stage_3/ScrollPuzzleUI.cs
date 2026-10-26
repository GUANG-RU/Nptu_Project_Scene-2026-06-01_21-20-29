using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 捲軸謎題 UI  V5
/// </summary>
public class ScrollPuzzleUI : MonoBehaviour
{
    [Header("UI 參考")]
    [SerializeField] private RectTransform gridContainer;
    [SerializeField] private RectTransform panelRect;     // 捲軸背景 Panel（GridContainer 會自動對齊它的中心）
    [SerializeField] private Canvas        parentCanvas;

    [Header("玩家腳本（開 UI 時停用）")]
    [SerializeField] private MonoBehaviour playerLook;
    [SerializeField] private MonoBehaviour playerMovement;

    [Header("格子外觀")]
    [SerializeField] private float cellSize      = 70f;
    [SerializeField] private float cellGap       = 4f;
    [SerializeField] private Color normalColor   = new Color(0.85f, 0.78f, 0.60f, 1f);
    [SerializeField] private Color obstacleColor = new Color(0.25f, 0.18f, 0.10f, 1f);
    [SerializeField] private Color runeColor     = new Color(0.6f,  0.3f,  0.8f,  0.9f);
    [SerializeField] private Color lineColor     = new Color(0.40f, 0.20f, 0.05f, 1f);
    [SerializeField] private Color runeHitColor  = new Color(1.00f, 0.80f, 0.10f, 1f);

    [Header("符文圖示（可留空）")]
    [SerializeField] private Sprite runeSprite;

    [Header("粒子")]
    [SerializeField] private float particleSpeed = 400f;
    [SerializeField] private Color particleColor = new Color(1f, 0.9f, 0.3f, 1f);
    [SerializeField] private float particleSize  = 14f;

    [Header("按鍵")]
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    // ── 事件 ──────────────────────────────────
    public System.Action OnPuzzleSolved;

    // ── 私有狀態 ──────────────────────────────
    private ScrollPuzzleData data;
    private int[,]           grid;
    private bool             puzzleActive = false;

    private RectTransform[,] cellRects;

    private bool    isDragging      = false;
    private int     dragCol         = -1;
    private int     dragRow         = -1;
    private Vector2 dragStartScreen;

    private List<GameObject> lineObjects   = new List<GameObject>();
    private List<GameObject> markerObjects = new List<GameObject>();
    private List<GameObject> rayObjects    = new List<GameObject>();  // 模擬光路顯示

    // ──────────────────────────────────────────
    void Awake()
    {
        gameObject.SetActive(false);
        if (parentCanvas == null)
            parentCanvas = GetComponentInParent<Canvas>();
    }

    public void ShowPuzzle(ScrollPuzzleData puzzleData)
    {
        data = puzzleData;
        gameObject.SetActive(true);
        BuildGrid();
        BuildEntryExitMarkers();
        puzzleActive = true;

        if (playerMovement) playerMovement.enabled = false;
        if (playerLook)     playerLook.enabled     = false;
        StartCoroutine(UnlockCursorNextFrame());
    }

    IEnumerator UnlockCursorNextFrame()
    {
        yield return null;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    public void HidePuzzle()
    {
        puzzleActive = false;
        gameObject.SetActive(false);
        if (playerMovement) playerMovement.enabled = true;
        if (playerLook)     playerLook.enabled     = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    // ── 建立格線 ──────────────────────────────
    void BuildGrid()
    {
        foreach (Transform t in gridContainer) Destroy(t.gameObject);
        lineObjects.Clear();
        markerObjects.Clear();
        rayObjects.Clear();

        int cols = data.columns;
        int rows = data.rows;
        grid      = new int[cols, rows];
        cellRects = new RectTransform[cols, rows];

        float totalW = cols * cellSize + (cols - 1) * cellGap;
        float totalH = rows * cellSize + (rows - 1) * cellGap;

        // 若有指定 panelRect，把 GridContainer 的父節點移過去
        if (panelRect != null && gridContainer.parent != panelRect)
            gridContainer.SetParent(panelRect, false);

        // 強制 GridContainer 置中於父節點
        gridContainer.anchorMin        = new Vector2(0.5f, 0.5f);
        gridContainer.anchorMax        = new Vector2(0.5f, 0.5f);
        gridContainer.pivot            = new Vector2(0.5f, 0.5f);
        gridContainer.anchoredPosition = Vector2.zero;
        gridContainer.sizeDelta        = new Vector2(totalW, totalH);

        Debug.Log($"[PuzzleUI] GridContainer parent={gridContainer.parent?.name} worldPos={gridContainer.position} sizeDelta={gridContainer.sizeDelta}");

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                bool isObstacle = data.IsObstacle(c, r);
                bool isRune     = data.IsRune(c, r);

                var go = new GameObject($"Cell_{c}_{r}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(gridContainer, false);

                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(cellSize, cellSize);

                // 以 GridContainer 中心為原點
                float x = (c - (cols - 1) * 0.5f) * (cellSize + cellGap);
                float y = (r - (rows - 1) * 0.5f) * (cellSize + cellGap);
                rt.anchoredPosition = new Vector2(x, y);

                go.GetComponent<Image>().color = isObstacle ? obstacleColor : normalColor;
                cellRects[c, r] = rt;
                grid[c, r]      = isObstacle ? -1 : 0;

                if (isRune && !isObstacle) MakeRuneIcon(go.transform);

                // Debug 第一格和最後一格
                if ((c == 0 && r == 0) || (c == cols-1 && r == rows-1))
                    Debug.Log($"[PuzzleUI] Cell({c},{r}) anchoredPos={rt.anchoredPosition} worldPos={rt.position}");
            }
        }
    }

    void MakeRuneIcon(Transform parent)
    {
        var go = new GameObject("Rune", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(cellSize * 0.45f, cellSize * 0.45f);
        rt.anchoredPosition = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.sprite = runeSprite;
        img.color  = runeColor;
    }

    // ── 入口(▶ 綠) / 出口(◀ 紅) 標示 ──────────
    void BuildEntryExitMarkers()
    {
        int   cols   = data.columns;
        float arrowW = cellSize * 0.45f;
        float arrowH = cellSize * 0.28f;
        float offset = cellSize * 0.5f + cellGap + arrowW * 0.5f;

        // ── 唯一入口（左側，entryRow）──
        Vector2 entry = CellCenter(0, data.entryRow);
        MakeArrow(new Vector2(entry.x - offset, entry.y),
                  arrowW, arrowH, new Color(0.15f, 0.85f, 0.2f, 1f), true);

        // ── 唯一出口（右側，exitRow）──
        Vector2 exit = CellCenter(cols - 1, data.exitRow);
        MakeArrow(new Vector2(exit.x + offset, exit.y),
                  arrowW, arrowH, new Color(0.9f, 0.2f, 0.1f, 1f), false);
    }

    /// <summary>pointRight=true → ▶（入口），false → ◀（出口）</summary>
    void MakeArrow(Vector2 anchoredPos, float w, float h, Color color, bool pointRight)
    {
        var root = new GameObject("Marker", typeof(RectTransform));
        root.transform.SetParent(gridContainer, false);
        var rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = rootRt.anchorMax = rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.sizeDelta        = new Vector2(w + h, h);   // 容納箭頭身體+頭
        rootRt.anchoredPosition = anchoredPos;
        markerObjects.Add(root);

        // ── 箭頭身體（細橫條）──
        var shaft = new GameObject("Shaft", typeof(RectTransform), typeof(Image));
        shaft.transform.SetParent(root.transform, false);
        var srt = shaft.GetComponent<RectTransform>();
        srt.anchorMin = srt.anchorMax = srt.pivot = new Vector2(0.5f, 0.5f);
        srt.sizeDelta        = new Vector2(w, h * 0.30f);
        srt.anchoredPosition = new Vector2(pointRight ? -w * 0.25f : w * 0.25f, 0f);
        shaft.GetComponent<Image>().color = color;

        // ── 箭頭頭（等腰三角形用兩條斜線拼）──
        float tipX = pointRight ? w * 0.35f : -w * 0.35f;
        MakeArrowHead(root.transform, color, new Vector2(tipX, 0f), h, pointRight);
    }

    void MakeArrowHead(Transform parent, Color color, Vector2 center, float size, bool pointRight)
    {
        // 用兩條旋轉的長條拼成 > 或 <
        float barLen  = size * 0.65f;
        float barThk  = size * 0.22f;
        float rot     = 40f;
        float offsetX = size * 0.12f * (pointRight ? -1f : 1f);

        for (int i = 0; i < 2; i++)
        {
            float sign = (i == 0) ? 1f : -1f;
            var bar = new GameObject("HeadBar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(parent, false);
            var brt = bar.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta        = new Vector2(barLen, barThk);
            brt.anchoredPosition = new Vector2(center.x + offsetX, center.y + sign * size * 0.22f);
            float angle = sign * rot * (pointRight ? 1f : -1f);
            brt.localRotation = Quaternion.Euler(0f, 0f, angle);
            bar.GetComponent<Image>().color = color;
        }
    }

    // ── 每幀輸入 ──────────────────────────────
    void Update()
    {
        if (!puzzleActive) return;

        if (Input.GetKeyDown(closeKey)) { HidePuzzle(); return; }

        if (Input.GetMouseButtonDown(0))
        {
            Vector2Int cell = GetCellUnderMouse();
            if (cell.x >= 0 && grid[cell.x, cell.y] != -1)
            {
                isDragging      = true;
                dragCol         = cell.x;
                dragRow         = cell.y;
                dragStartScreen = Input.mousePosition;
                Debug.Log($"[PuzzleUI] DragStart cell=({dragCol},{dragRow}) screen={dragStartScreen}");
            }
        }

        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            isDragging = false;
            Vector2 delta = (Vector2)Input.mousePosition - dragStartScreen;

            Debug.Log($"[PuzzleUI] DragEnd delta={delta} mag={delta.magnitude:F1} threshold={cellSize*0.25f:F1}");

            if (delta.magnitude > cellSize * 0.25f && dragCol >= 0)
            {
                float ax = Mathf.Abs(delta.x);
                float ay = Mathf.Abs(delta.y);

                if (ax > cellSize * 0.15f && ay > cellSize * 0.15f)
                {
                    // ★ 判斷規則（UI 座標，Y 向上為正）：
                    // 拖曳方向 右上(dx>0,dy>0) 或 左下(dx<0,dy<0) → /  (同號)
                    // 拖曳方向 右下(dx>0,dy<0) 或 左上(dx<0,dy>0) → \  (異號)
                    bool sameSign  = (delta.x > 0) == (delta.y > 0);
                    int  direction = sameSign ? 1 : 2;   // 同號=/ 異號=\

                    Debug.Log($"[PuzzleUI] delta=({delta.x:F1},{delta.y:F1}) sameSign={sameSign} dir={direction}({(direction==1?"/":"\\")})");

                    if (grid[dragCol, dragRow] != -1)
                    {
                        grid[dragCol, dragRow] = direction;
                        DrawCellLine(dragCol, dragRow, direction);
                        UpdateRayDisplay();
                        TrySolve();
                    }
                }
            }
            dragCol = dragRow = -1;
        }

        if (Input.GetMouseButtonDown(1))
        {
            Vector2Int cell = GetCellUnderMouse();
            if (cell.x >= 0 && grid[cell.x, cell.y] > 0)
            {
                ClearCellLine(cell.x, cell.y);
                grid[cell.x, cell.y] = 0;
                UpdateRayDisplay();
            }
        }
    }

    Vector2Int GetCellUnderMouse()
    {
        if (data == null || cellRects == null) return new Vector2Int(-1, -1);
        Camera uiCam = (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                       ? parentCanvas.worldCamera : null;

        for (int r = 0; r < data.rows; r++)
            for (int c = 0; c < data.columns; c++)
                if (RectTransformUtility.RectangleContainsScreenPoint(cellRects[c, r], Input.mousePosition, uiCam))
                    return new Vector2Int(c, r);

        return new Vector2Int(-1, -1);
    }

    void DrawCellLine(int c, int r, int dir)
    {
        ClearCellLine(c, r);
        var rt     = cellRects[c, r];
        var lineGo = new GameObject($"Line_{c}_{r}", typeof(RectTransform), typeof(Image));
        lineGo.transform.SetParent(rt, false);
        lineObjects.Add(lineGo);

        var lrt = lineGo.GetComponent<RectTransform>();
        lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(0.5f, 0.5f);
        lrt.sizeDelta        = new Vector2(cellSize * 1.3f, 5f);
        lrt.anchoredPosition = Vector2.zero;

        // dir==1 → /  → Z=+45°（左下到右上）
        // dir==2 → \  → Z=-45°（左上到右下）
        lrt.localRotation = Quaternion.Euler(0f, 0f, dir == 1 ? 45f : -45f);

        lineGo.GetComponent<Image>().color = lineColor;
    }

    void ClearCellLine(int c, int r)
    {
        string lineName = $"Line_{c}_{r}";
        var rt = cellRects[c, r];
        for (int i = rt.childCount - 1; i >= 0; i--)
        {
            var child = rt.GetChild(i);
            if (child.name == lineName)
            {
                lineObjects.Remove(child.gameObject);
                Destroy(child.gameObject);
            }
        }
    }

    // ── 模擬光路顯示 ──────────────────────────
    void UpdateRayDisplay()
    {
        foreach (var go in rayObjects) if (go) Destroy(go);
        rayObjects.Clear();

        // 只從 entryRow 出發
        var segments = GetRaySegments(data.entryRow);
        for (int i = 0; i < segments.Count - 1; i++)
            DrawRaySegment(segments[i], segments[i + 1]);
    }

    List<Vector2> GetRaySegments(int startRow)
    {
        var pts = new List<Vector2>();
        int cx = 0, cy = startRow, dx = 1, dy = 0;

        // 從左邊邊界外開始
        pts.Add(CellCenter(0, startRow) + new Vector2(-(cellSize * 0.5f + cellGap), 0f));

        int maxSteps = data.columns * data.rows * 4 + 10;
        for (int step = 0; step < maxSteps; step++)
        {
            // 離開網格
            if (cx < 0 || cx >= data.columns || cy < 0 || cy >= data.rows) break;
            // 障礙就停
            if (grid[cx, cy] == -1) break;

            pts.Add(CellCenter(cx, cy));

            int cell = grid[cx, cy];
            if (cell == 0)
            {
                // 空白格：光線直走，不反射
            }
            else if (cell == 1) { int t = dx; dx = dy;  dy = t;  }  // / 反射: (dx,dy)→(dy,dx)
            else                { int t = dx; dx = -dy; dy = -t; }  // \ 反射: (dx,dy)→(-dy,-dx)

            cx += dx; cy += dy;
        }

        // 如果光線走出右邊，加上右邊界外的點
        if (pts.Count >= 2 && cx == data.columns)
        {
            int lastRow = cy - dy;
            lastRow = Mathf.Clamp(lastRow, 0, data.rows - 1);
            pts.Add(CellCenter(data.columns - 1, lastRow) + new Vector2(cellSize * 0.5f + cellGap, 0f));
        }

        return pts;
    }

    void DrawRaySegment(Vector2 from, Vector2 to)
    {
        var go = new GameObject("Ray", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(gridContainer, false);
        rayObjects.Add(go);

        Vector2 dir    = to - from;
        float   length = dir.magnitude;
        float   angle  = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta        = new Vector2(length, 4f);
        rt.anchoredPosition = (from + to) * 0.5f;
        rt.localRotation    = Quaternion.Euler(0f, 0f, angle);

        go.GetComponent<Image>().color = new Color(1f, 0.55f, 0.1f, 0.55f);
    }

    void TrySolve()
    {
        var hitRunes = new HashSet<Vector2Int>();
        if (!SimulateRay(data.entryRow, hitRunes)) return;

        bool allHit = true;
        if (data.runes != null)
            foreach (var rv in data.runes)
                if (!hitRunes.Contains(rv)) { allHit = false; break; }

        if (allHit) StartCoroutine(PlayParticleAndSolve(data.entryRow));
    }

    /// <summary>模擬光線；成功 = 從右側 exitRow 離開，且經過全部符文</summary>
    bool SimulateRay(int startRow, HashSet<Vector2Int> hitRunes)
    {
        int cx = 0, cy = startRow, dx = 1, dy = 0;
        int maxSteps = data.columns * data.rows * 4 + 10;

        for (int step = 0; step < maxSteps; step++)
        {
            if (cx < 0 || cx >= data.columns || cy < 0 || cy >= data.rows) break;
            if (grid[cx, cy] == -1) return false;  // 障礙擋住

            if (data.IsRune(cx, cy)) hitRunes.Add(new Vector2Int(cx, cy));

            int cell = grid[cx, cy];
            if (cell == 0) { /* 空白格直走 */ }
            else if (cell == 1) { int t = dx; dx = dy;  dy = t;  }  // / : (dx,dy)→(dy,dx)
            else                { int t = dx; dx = -dy; dy = -t; }  // \ : (dx,dy)→(-dy,-dx)

            cx += dx; cy += dy;
        }
        return cx == data.columns && cy == data.exitRow;
    }

    IEnumerator PlayParticleAndSolve(int startRow)
    {
        puzzleActive = false;

        var path = BuildPath(data.entryRow);
        if (path == null || path.Count < 2) { FinishSolve(); yield break; }

        var pGo = new GameObject("Particle", typeof(RectTransform), typeof(Image));
        pGo.transform.SetParent(gridContainer, false);
        var prt = pGo.GetComponent<RectTransform>();
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(particleSize, particleSize);
        pGo.GetComponent<Image>().color = particleColor;

        StartCoroutine(FlashRunes());

        for (int i = 0; i < path.Count - 1; i++)
        {
            Vector2 from = path[i], to = path[i + 1];
            float dur = Vector2.Distance(from, to) / particleSpeed;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                prt.anchoredPosition = Vector2.Lerp(from, to, Mathf.Clamp01(t / dur));
                yield return null;
            }
        }

        Destroy(pGo);
        yield return new WaitForSeconds(0.35f);
        FinishSolve();
    }

    void FinishSolve() { HidePuzzle(); OnPuzzleSolved?.Invoke(); }

    List<Vector2> BuildPath(int startRow)
    {
        var path = new List<Vector2>();
        int cx = 0, cy = startRow, dx = 1, dy = 0;
        path.Add(CellCenter(0, startRow) + new Vector2(-(cellSize * 0.5f + cellGap), 0f));

        int maxSteps = data.columns * data.rows * 4 + 10;
        for (int step = 0; step < maxSteps; step++)
        {
            if (cx < 0 || cx >= data.columns || cy < 0 || cy >= data.rows) break;
            if (grid[cx, cy] == -1) break;
            path.Add(CellCenter(cx, cy));
            int cell = grid[cx, cy];
            if (cell == 0)      { /* 直走 */ }
            else if (cell == 1) { int t = dx; dx = dy;  dy = t;  }  // / : (dx,dy)→(dy,dx)
            else                { int t = dx; dx = -dy; dy = -t; }  // \ : (dx,dy)→(-dy,-dx)
            cx += dx; cy += dy;
        }
        if (cx == data.columns)
            path.Add(CellCenter(data.columns - 1, Mathf.Clamp(cy - dy, 0, data.rows - 1)) + new Vector2(cellSize * 0.5f + cellGap, 0f));
        return path;
    }

    Vector2 CellCenter(int c, int r)
    {
        float x = (c - (data.columns - 1) * 0.5f) * (cellSize + cellGap);
        float y = (r - (data.rows    - 1) * 0.5f) * (cellSize + cellGap);
        return new Vector2(x, y);
    }

    IEnumerator FlashRunes()
    {
        if (data.runes == null) yield break;
        for (float t = 0f; t < 1.2f; t += Time.deltaTime)
        {
            float alpha = Mathf.PingPong(t * 4f, 1f);
            foreach (var rv in data.runes)
            {
                var rt = cellRects[rv.x, rv.y];
                for (int i = 0; i < rt.childCount; i++)
                {
                    var img = rt.GetChild(i).GetComponent<Image>();
                    if (img) img.color = Color.Lerp(runeColor, runeHitColor, alpha);
                }
            }
            yield return null;
        }
    }
}