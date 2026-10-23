using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 捲軸謎題 UI
///
/// 負責：
///   - 顯示格線、障礙（深色）、符文（圖示）
///   - 滑鼠拖曳輸入 / 和 \ 方向線段
///   - 粒子沿路徑發射動畫
///   - 通知 Manager 通關 / 失敗
///
/// Hierarchy 範例：
///   ScrollPuzzleCanvas (Canvas, CanvasScaler, GraphicRaycaster)
///     └─ PuzzleRoot (此腳本掛這裡)
///         ├─ Background    (Image — 捲軸背景 PNG)
///         ├─ GridContainer (空 RectTransform — 格子生成在這裡)
///         └─ ParticleRoot  (空物件 — 粒子點生成)
/// </summary>
public class ScrollPuzzleUI : MonoBehaviour
{
    // ── Inspector ─────────────────────────────
    [Header("UI 參考")]
    [SerializeField] private GameObject puzzleRoot;        // 整個謎題 UI 根節點（開關用）
    [SerializeField] private RectTransform gridContainer;  // 格子生成的容器
    [SerializeField] private Image        scrollBg;        // 捲軸背景圖

    [Header("格子外觀")]
    [SerializeField] private float cellSize       = 80f;
    [SerializeField] private float cellGap        = 4f;
    [SerializeField] private Color normalColor    = new Color(0.85f, 0.78f, 0.60f, 1f);
    [SerializeField] private Color obstacleColor  = new Color(0.25f, 0.18f, 0.10f, 1f);
    [SerializeField] private Color runeColor      = new Color(0.95f, 0.90f, 0.55f, 1f);
    [SerializeField] private Color lineColor      = new Color(0.40f, 0.20f, 0.05f, 1f);
    [SerializeField] private Color runeHitColor   = new Color(1.00f, 0.80f, 0.10f, 1f);

    [Header("符文圖示（可留空，留空用圓點代替）")]
    [SerializeField] private Sprite runeSprite;

    [Header("粒子（UI Image 做的假粒子）")]
    [SerializeField] private float particleSpeed  = 300f;  // px/s
    [SerializeField] private Color particleColor  = new Color(1f, 0.9f, 0.3f, 1f);
    [SerializeField] private float particleSize   = 14f;

    // ── 事件 ──────────────────────────────────
    public System.Action OnPuzzleSolved;
    public System.Action OnPuzzleFailed;   // 目前未使用，保留

    // ── 私有狀態 ──────────────────────────────
    private ScrollPuzzleData data;
    private int[,] grid;   // 0=空, 1=/, 2=\, -1=障礙
    private bool puzzleActive = false;

    // 格子 UI 物件
    private RectTransform[,] cellRects;
    private Image[,]         cellImages;

    // 拖曳狀態
    private bool  isDragging   = false;
    private int   dragCol      = -1;
    private int   dragRow      = -1;
    private Vector2 dragStart;

    // 線段 Image 列表（每次 Solve 前清除）
    private List<GameObject> lineObjects = new List<GameObject>();

    // ── 公開 API ──────────────────────────────
    public void ShowPuzzle(ScrollPuzzleData puzzleData)
    {
        data = puzzleData;
        BuildGrid();
        puzzleRoot.SetActive(true);
        puzzleActive = true;

        // 解鎖滑鼠
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    public void HidePuzzle()
    {
        puzzleActive = false;
        puzzleRoot.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    // ── 建立格線 ──────────────────────────────
    void BuildGrid()
    {
        // 清除舊的
        foreach (Transform t in gridContainer) Destroy(t.gameObject);
        lineObjects.Clear();

        int cols = data.columns;
        int rows = data.rows;
        grid      = new int[cols, rows];
        cellRects = new RectTransform[cols, rows];
        cellImages= new Image[cols, rows];

        float totalW = cols * cellSize + (cols - 1) * cellGap;
        float totalH = rows * cellSize + (rows - 1) * cellGap;

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                bool isObstacle = data.IsObstacle(c, r);
                bool isRune     = data.IsRune(c, r);

                // 格子底板
                var go = new GameObject($"Cell_{c}_{r}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(gridContainer, false);

                var rt = go.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(cellSize, cellSize);
                // 左下為原點，row 0 在下
                float x = c * (cellSize + cellGap) - totalW / 2f + cellSize / 2f;
                float y = r * (cellSize + cellGap) - totalH / 2f + cellSize / 2f;
                rt.anchoredPosition = new Vector2(x, y);

                var img = go.GetComponent<Image>();
                img.color = isObstacle ? obstacleColor : normalColor;

                cellRects [c, r] = rt;
                cellImages[c, r] = img;
                grid      [c, r] = isObstacle ? -1 : 0;

                // 符文圖示
                if (isRune && !isObstacle)
                {
                    var runeGo = new GameObject("Rune", typeof(RectTransform), typeof(Image));
                    runeGo.transform.SetParent(go.transform, false);
                    var rrt = runeGo.GetComponent<RectTransform>();
                    rrt.sizeDelta        = new Vector2(cellSize * 0.5f, cellSize * 0.5f);
                    rrt.anchoredPosition = Vector2.zero;
                    var rimg = runeGo.GetComponent<Image>();
                    rimg.color  = runeColor;
                    rimg.sprite = runeSprite;
                    if (runeSprite == null) rimg.color = new Color(0.6f, 0.3f, 0.8f, 0.9f);
                }

                // 格子按鈕（EventTrigger 太麻煩，用 Update + 座標換算）
            }
        }
    }

    // ── 每幀輸入 ──────────────────────────────
    void Update()
    {
        if (!puzzleActive) return;

        if (Input.GetMouseButtonDown(0))
        {
            Vector2Int cell = GetCellUnderMouse();
            if (cell.x >= 0 && grid[cell.x, cell.y] != -1)
            {
                isDragging = true;
                dragCol    = cell.x;
                dragRow    = cell.y;
                dragStart  = Input.mousePosition;
            }
        }

        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            isDragging = false;
            Vector2 delta = (Vector2)Input.mousePosition - dragStart;

            if (delta.magnitude > cellSize * 0.3f)
            {
                // 判斷方向：x>0 且 y>0 → /（右上）；x>0 且 y<0 → \（右下）
                // 只接受大致斜向的拖曳
                float ax = Mathf.Abs(delta.x);
                float ay = Mathf.Abs(delta.y);
                if (ax > cellSize * 0.2f && ay > cellSize * 0.2f)
                {
                    bool slash     = delta.y > 0;   // true = /  false = \
                    int  direction = slash ? 1 : 2;

                    if (grid[dragCol, dragRow] != -1)
                    {
                        grid[dragCol, dragRow] = direction;
                        DrawCellLine(dragCol, dragRow, direction);
                        TrySolve();
                    }
                }
            }

            dragCol = dragRow = -1;
        }

        // 右鍵清除一格
        if (Input.GetMouseButtonDown(1))
        {
            Vector2Int cell = GetCellUnderMouse();
            if (cell.x >= 0 && grid[cell.x, cell.y] > 0)
            {
                ClearCellLine(cell.x, cell.y);
                grid[cell.x, cell.y] = 0;
            }
        }
    }

    // ── 座標換算 ──────────────────────────────
    Vector2Int GetCellUnderMouse()
    {
        int cols = data.columns;
        int rows = data.rows;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                var rt = cellRects[c, r];
                Vector2 local;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rt, Input.mousePosition,
                    null,   // Screen Space Overlay → null
                    out local);
                if (Mathf.Abs(local.x) < cellSize / 2f && Mathf.Abs(local.y) < cellSize / 2f)
                    return new Vector2Int(c, r);
            }
        return new Vector2Int(-1, -1);
    }

    // ── 畫線 ──────────────────────────────────
    void DrawCellLine(int c, int r, int dir)
    {
        // 先清掉舊的
        ClearCellLine(c, r);

        var rt = cellRects[c, r];

        var lineGo = new GameObject("Line", typeof(RectTransform), typeof(Image));
        lineGo.transform.SetParent(rt, false);
        lineObjects.Add(lineGo);

        var lrt = lineGo.GetComponent<RectTransform>();
        lrt.sizeDelta        = new Vector2(cellSize * 1.35f, 6f);
        lrt.anchoredPosition = Vector2.zero;

        var limg = lineGo.GetComponent<Image>();
        limg.color = lineColor;

        // / = 45度，\ = -45度
        lrt.localRotation = Quaternion.Euler(0f, 0f, dir == 1 ? 45f : -45f);

        // 標記此格的線物件（用 tag 存在格子物件上）
        lineGo.name = $"Line_{c}_{r}";
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

    // ── 解謎判斷 ──────────────────────────────
    void TrySolve()
    {
        // 從左邊每一個 row 射入，模擬光線反射
        int cols = data.columns;
        int rows = data.rows;

        for (int startRow = 0; startRow < rows; startRow++)
        {
            var hitRunes = new HashSet<Vector2Int>();
            bool valid = SimulateRay(startRow, hitRunes);

            if (!valid) continue;

            // 檢查是否命中所有符文
            bool allHit = true;
            if (data.runes != null)
                foreach (var rv in data.runes)
                    if (!hitRunes.Contains(rv)) { allHit = false; break; }

            if (allHit)
            {
                // 通關！先播粒子再通知
                StartCoroutine(PlayParticleAndSolve(startRow));
                return;
            }
        }
    }

    /// <summary>
    /// 模擬從 row=startRow 左側射入的光線
    /// direction: (1,0)=右, (-1,0)=左, (0,1)=上, (0,-1)=下
    /// 遇到 / : (dx,dy) → (-dy,-dx)   遇到 \ : (dx,dy) → (dy,dx)
    /// </summary>
    bool SimulateRay(int startRow, HashSet<Vector2Int> hitRunes)
    {
        int cols = data.columns;
        int rows = data.rows;

        int cx = 0, cy = startRow;
        int dx = 1, dy = 0;  // 往右射入

        int maxSteps = cols * rows * 2 + 10;
        for (int step = 0; step < maxSteps; step++)
        {
            if (cx < 0 || cx >= cols || cy < 0 || cy >= rows) break;
            if (grid[cx, cy] == -1) return false;  // 撞障礙

            int cell = grid[cx, cy];
            if (cell == 0) return false;  // 空格，路徑不完整

            // 記錄符文命中
            if (data.IsRune(cx, cy)) hitRunes.Add(new Vector2Int(cx, cy));

            // 反射
            if (cell == 1)       // /
            { int tmp = dx; dx = -dy; dy = -tmp; }
            else if (cell == 2)  // \
            { int tmp = dx; dx = dy; dy = tmp; }

            cx += dx;
            cy += dy;
        }
        // 光線到達右側邊界才算合法
        return cx == cols;
    }

    // ── 粒子動畫 ──────────────────────────────
    IEnumerator PlayParticleAndSolve(int startRow)
    {
        puzzleActive = false;  // 防止繼續輸入

        // 收集路徑點
        var path = BuildPath(startRow);
        if (path == null || path.Count < 2)
        {
            OnPuzzleSolved?.Invoke();
            yield break;
        }

        // 建立粒子 Image
        var pGo = new GameObject("Particle", typeof(RectTransform), typeof(Image));
        pGo.transform.SetParent(gridContainer, false);
        var prt  = pGo.GetComponent<RectTransform>();
        prt.sizeDelta = new Vector2(particleSize, particleSize);
        var pimg = pGo.GetComponent<Image>();
        pimg.color = particleColor;

        // 讓符文閃光
        StartCoroutine(FlashRunes());

        // 沿路徑移動
        for (int i = 0; i < path.Count - 1; i++)
        {
            Vector2 from = path[i];
            Vector2 to   = path[i + 1];
            float dist   = Vector2.Distance(from, to);
            float t      = 0f;
            float dur    = dist / particleSpeed;

            while (t < dur)
            {
                t += Time.deltaTime;
                prt.anchoredPosition = Vector2.Lerp(from, to, t / dur);
                yield return null;
            }
        }

        Destroy(pGo);
        yield return new WaitForSeconds(0.3f);

        HidePuzzle();
        OnPuzzleSolved?.Invoke();
    }

    List<Vector2> BuildPath(int startRow)
    {
        int cols = data.columns;
        int rows = data.rows;
        var path = new List<Vector2>();

        int cx = 0, cy = startRow;
        int dx = 1, dy = 0;

        // 進入點（左邊界外）
        path.Add(CellCenter(cx, cy) + new Vector2(-cellSize, 0f));

        int maxSteps = cols * rows * 2 + 10;
        for (int step = 0; step < maxSteps; step++)
        {
            if (cx < 0 || cx >= cols || cy < 0 || cy >= rows) break;

            path.Add(CellCenter(cx, cy));

            int cell = grid[cx, cy];
            if (cell == 1)       { int t = dx; dx = -dy; dy = -t; }
            else if (cell == 2)  { int t = dx; dx = dy;  dy = t;  }

            cx += dx;
            cy += dy;
        }
        // 出口點
        path.Add(CellCenter(cols - 1, (cy - dy + rows) % rows) + new Vector2(cellSize, 0f));

        return path;
    }

    Vector2 CellCenter(int c, int r)
    {
        int cols = data.columns;
        int rows = data.rows;
        float totalW = cols * cellSize + (cols - 1) * cellGap;
        float totalH = rows * cellSize + (rows - 1) * cellGap;
        float x = c * (cellSize + cellGap) - totalW / 2f + cellSize / 2f;
        float y = r * (cellSize + cellGap) - totalH / 2f + cellSize / 2f;
        return new Vector2(x, y);
    }

    IEnumerator FlashRunes()
    {
        if (data.runes == null) yield break;
        float dur = 1.2f;
        float t   = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float alpha = Mathf.PingPong(t * 4f, 1f);
            foreach (var rv in data.runes)
            {
                var rt = cellRects[rv.x, rv.y];
                // 找符文子物件
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
