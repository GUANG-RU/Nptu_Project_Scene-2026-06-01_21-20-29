using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 劇情對話的單一階段設定
/// </summary>
[Serializable]
public class StoryEncounterStage
{
    [Tooltip("階段說明 (僅在 Inspector 方便辨識，例如：第 1 次-教學、第 3 次-抱怨)")]
    public string stageName = "對話階段";

    [Header("此階段播放的文字對話")]
    [TextArea(2, 5)]
    public string[] messages;
}

/// <summary>
/// 具備跨存檔 (PlayerPrefs) 與 Inspector 無限階段自訂功能的劇情觸發器
/// </summary>
public class AdaptiveStoryTrigger : MonoBehaviour
{
    [Header("謎題/事件分類")]
    [Tooltip("分類 ID (相同 ID 會共用遇到次數，例如：LaserPuzzle, MirrorPuzzle)")]
    public string puzzleTypeID = "LaserPuzzle";

    [Header("跨關卡/重啟存檔設定")]
    [Tooltip("勾選後，遊戲關掉重開依然會記住遇到次數 (使用 PlayerPrefs)")]
    public bool saveAcrossSessions = true;

    [Header("各次遇到的對話階段清單 (按順序排列)")]
    [Tooltip("第 1 個代表第 1 次遇到、第 2 個代表第 2 次遇到...以此類推。若次數超過清單長度則自動保持安靜。")]
    public List<StoryEncounterStage> encounterStages = new List<StoryEncounterStage>();

    [Header("顯示設定")]
    [Tooltip("每段文字顯示後自動留存的時間")]
    public float displayTime = 3f;

    [Tooltip("關卡中的 StoryManager 引用")]
    public StoryManager storyManager;

    // 防止玩家在同一地點重複踩踏觸發
    private bool hasTriggeredThisLocation = false;

    // 暫存於記憶體中的計數器 (當 saveAcrossSessions 未勾選時使用)
    private static Dictionary<string, int> s_SessionCounts = new Dictionary<string, int>();

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggeredThisLocation) return;
        if (!other.CompareTag("Player")) return;

        if (storyManager == null)
        {
            Debug.LogWarning($"[{gameObject.name}] 上的 AdaptiveStoryTrigger 尚未指定 StoryManager！");
            return;
        }

        hasTriggeredThisLocation = true;

        // 1. 取得並增加此 ID 的累計次數
        int currentCount = GetAndIncreaseEncounterCount(puzzleTypeID);

        // 2. 計算對應的 List 索引 (第 1 次對應 index 0, 第 2 次對應 index 1...)
        int stageIndex = currentCount - 1;

        // 3. 檢查是否有設定該階段的對話
        if (stageIndex >= 0 && stageIndex < encounterStages.Count)
        {
            string[] selectedMessages = encounterStages[stageIndex].messages;

            if (selectedMessages != null && selectedMessages.Length > 0 && !string.IsNullOrWhiteSpace(selectedMessages[0]))
            {
                Debug.Log($"[AdaptiveStoryTrigger] 謎題 [{puzzleTypeID}] 第 {currentCount} 次觸發，播放階段：{encounterStages[stageIndex].stageName}");
                storyManager.ShowStories(selectedMessages, displayTime);
                return;
            }
        }

        // 4. 若次數已超過設定的階段數，或該階段未填寫文字，則保持安靜
        Debug.Log($"[AdaptiveStoryTrigger] 謎題 [{puzzleTypeID}] 第 {currentCount} 次觸發，已無後續對話（保持安靜）。");
    }

    /// <summary>
    /// 讀取並增加計數器
    /// </summary>
    private int GetAndIncreaseEncounterCount(string id)
    {
        int count = 0;

        if (saveAcrossSessions)
        {
            string key = "EncounterCount_" + id;
            count = PlayerPrefs.GetInt(key, 0) + 1;
            PlayerPrefs.SetInt(key, count);
            PlayerPrefs.Save(); // 立即寫入存檔
        }
        else
        {
            if (!s_SessionCounts.ContainsKey(id))
            {
                s_SessionCounts[id] = 0;
            }
            s_SessionCounts[id]++;
            count = s_SessionCounts[id];
        }

        return count;
    }

    /// <summary>
    /// 在 Unity Editor 中右鍵此元件即可一鍵重置次數 (方便測試)
    /// </summary>
    [ContextMenu("重置此 Puzzle ID 的遇到次數")]
    public void ResetEncounterCount()
    {
        if (saveAcrossSessions)
        {
            PlayerPrefs.DeleteKey("EncounterCount_" + puzzleTypeID);
            PlayerPrefs.Save();
        }
        s_SessionCounts.Remove(puzzleTypeID);
        Debug.Log($"[AdaptiveStoryTrigger] 已重置 [{puzzleTypeID}] 的次數紀錄！");
    }
}