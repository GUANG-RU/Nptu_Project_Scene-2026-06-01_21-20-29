using UnityEngine;

public class RandomSensorGoal : MonoBehaviour
{
    [Header("--- 目標設定 ---")]
    [Tooltip("要設定目標顏色的 Photosensor (若留空會自動抓取同物件上的 Photosensor)")]
    public Photosensor targetSensor;

    [Tooltip("勾選後，遊戲一開始 (Start) 就自動隨機決定顏色")]
    public bool randomizeOnStart = true;

    [Header("--- 當前隨機結果 (唯讀觀察) ---")]
    public Color generatedColor;

    private void Start()
    {
        if (targetSensor == null)
        {
            targetSensor = GetComponent<Photosensor>();
        }

        if (randomizeOnStart)
        {
            GenerateRandomTwoColorGoal();
        }
    }

    /// <summary>
    /// 隨機挑選兩種原色進行二色加法混合 (R:1/0, G:1/0, B:1/0)
    /// </summary>
    public void GenerateRandomTwoColorGoal()
    {
        // 嚴格定義符合「恰好兩種原色混合」的三種 RGB 組合 (R, G, B)
        Color[] twoColorMixes = new Color[]
        {
            new Color(1f, 1f, 0f, 1f), // 紅 + 綠 = 黃光 (Yellow)
            new Color(1f, 0f, 1f, 1f), // 紅 + 藍 = 紫光/洋紅 (Magenta)
            new Color(0f, 1f, 1f, 1f)  // 綠 + 藍 = 青光 (Cyan)
        };

        // 隨機抽取其中一種
        int randomIndex = Random.Range(0, twoColorMixes.Length);
        generatedColor = twoColorMixes[randomIndex];

        // 套用到對應的 Photosensor
        if (targetSensor != null)
        {
            targetSensor.requiredColor = generatedColor;
            Debug.Log($"<color=cyan>[隨機關卡] 感應器 {targetSensor.name} 的目標顏色已改為：RGB({generatedColor.r},{generatedColor.g},{generatedColor.b})</color>");
        }
    }
}