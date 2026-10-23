using System;
using UnityEngine;

/// <summary>
/// 一道謎題的資料定義
/// 在 Inspector 裡設定格線大小、障礙、符文位置
/// </summary>
[Serializable]
public class ScrollPuzzleData
{
    [Header("格線大小")]
    public int columns = 4;   // 左右格數
    public int rows    = 3;   // 上下格數

    [Header("障礙格子（不能畫線）— 填入格子座標 (col, row)，左下為 (0,0)")]
    public Vector2Int[] obstacles;

    [Header("符文格子（路徑必須經過）— 填入格子座標")]
    public Vector2Int[] runes;

    // ── 執行期用 ──────────────────────────────
    /// <summary>回傳這格是不是障礙</summary>
    public bool IsObstacle(int col, int row)
    {
        if (obstacles == null) return false;
        foreach (var o in obstacles)
            if (o.x == col && o.y == row) return true;
        return false;
    }

    /// <summary>回傳這格是不是符文</summary>
    public bool IsRune(int col, int row)
    {
        if (runes == null) return false;
        foreach (var r in runes)
            if (r.x == col && r.y == row) return true;
        return false;
    }
}
