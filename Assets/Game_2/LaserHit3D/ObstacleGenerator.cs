using UnityEngine;

public class ObstacleGenerator : MonoBehaviour
{
    [Header("--- 平面網格對齊設定 ---")]
    [Tooltip("單格網格寬度 (6m x 6m)")]
    public float cellSize = 6.0f;

    [Tooltip("內部擋光柱高度")]
    public float pillarHeight = 9.0f;

    [Tooltip("Photosensor 感應接收器的懸浮高度")]
    public float sensorHoverHeight = 2.0f;

    [Tooltip("地圖生成起點座標")]
    public Vector3 originPosition = Vector3.zero;

    [Header("--- 障礙柱與機關 Prefabs ---")]
    [Tooltip("遮擋雷射的柱子 Prefab (請確認帶有 Collider)")]
    public GameObject pillarPrefab; // 數字 1

    [Header("--- 多顏色感應接收器陣列 (對應數字 2, 3, 4...) ---")]
    [Tooltip("Element 0 = 數字 2 (紅光), Element 1 = 數字 3 (綠光), Element 2 = 數字 4 (黃光)")]
    public GameObject[] photosensorPrefabs;

    // =========================================================================
    // 🗺️ 2D 平面矩陣佈局 (房間內部)
    // 0 = 空地 (保留你做好的地板與房間外牆)
    // 1 = 擋光柱子 (立於網格正中心，阻擋雷射)
    // 2 = 感應器 A (例如：紅光感應器)
    // 3 = 感應器 B (例如：綠光感應器)
    // 4 = 感應器 C (例如：黃光/混色感應器)
    // =========================================================================
    private readonly int[,] mapLayout = new int[5, 5]
    {
        { 0, 0, 0, 0, 0 },
        { 0, 1, 0, 2, 0 }, // (1,1) 生成擋光柱，(1,3) 生成紅光感應器
        { 0, 0, 1, 0, 0 }, // (2,2) 生成中央擋光柱
        { 0, 3, 0, 4, 0 }, // (3,1) 生成綠光感應器，(3,3) 生成黃光感應器
        { 0, 0, 0, 0, 0 }
    };

    private Transform containerTransform;

    private void Start()
    {
        GenerateObstacles();
    }

    /// <summary>
    /// 生成內部擋光柱與 Photosensor
    /// </summary>
    [ContextMenu("Generate Obstacles")]
    public void GenerateObstacles()
    {
        ClearObstacles();

        GameObject container = new GameObject("Generated_Obstacles");
        container.transform.SetParent(this.transform);
        containerTransform = container.transform;

        int rows = mapLayout.GetLength(0);
        int cols = mapLayout.GetLength(1);

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                int tileType = mapLayout[r, c];

                // 0 為空地：不生成任何東西，完全保留現有場景
                if (tileType == 0) continue;

                // 計算該網格的正中心世界座標
                Vector3 cellCornerPos = originPosition + new Vector3(c * cellSize, 0f, r * cellSize);
                Vector3 cellCenterPos = cellCornerPos + new Vector3(cellSize / 2f, 0f, cellSize / 2f);

                if (tileType == 1)
                {
                    // 1 = 生成擋光柱子 (置於網格正中心)
                    GameObject pillarObj;
                    if (pillarPrefab != null)
                    {
                        pillarObj = Instantiate(pillarPrefab, cellCenterPos, Quaternion.identity, containerTransform);
                    }
                    else
                    {
                        // 若無 Prefab，自動生成預設 Cube 柱子 (寬2m, 高9m, 深2m)
                        pillarObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        pillarObj.transform.position = cellCenterPos + new Vector3(0f, pillarHeight / 2f, 0f);
                        pillarObj.transform.localScale = new Vector3(2.0f, pillarHeight, 2.0f);
                        pillarObj.transform.SetParent(containerTransform);
                    }
                    pillarObj.name = $"Pillar_{r}_{c}";
                }
                else if (tileType >= 2)
                {
                    // 2 以上 = 對應 photosensorPrefabs 陣列中的感應器
                    int sensorIndex = tileType - 2;

                    if (photosensorPrefabs != null && sensorIndex < photosensorPrefabs.Length && photosensorPrefabs[sensorIndex] != null)
                    {
                        Vector3 sensorPos = cellCenterPos + new Vector3(0f, sensorHoverHeight, 0f);
                        GameObject sensorObj = Instantiate(photosensorPrefabs[sensorIndex], sensorPos, Quaternion.identity, containerTransform);
                        sensorObj.name = $"Photosensor_Type{tileType}_{r}_{c}";
                    }
                    else
                    {
                        Debug.LogWarning($"[ObstacleGenerator] 位置 ({r},{c}) 設定為代號 {tileType}，但 Photosensor 陣列索引 [{sensorIndex}] 為空！");
                    }
                }
            }
        }

        Debug.Log($"[ObstacleGenerator] 內部擋光柱與感應器生成完畢！");
    }

    /// <summary>
    /// 一鍵清除生成物件
    /// </summary>
    [ContextMenu("Clear Obstacles")]
    public void ClearObstacles()
    {
        if (containerTransform != null)
        {
            DestroyImmediate(containerTransform.gameObject);
            containerTransform = null;
        }
        else
        {
            Transform existing = transform.Find("Generated_Obstacles");
            if (existing != null) DestroyImmediate(existing.gameObject);
        }
    }
}