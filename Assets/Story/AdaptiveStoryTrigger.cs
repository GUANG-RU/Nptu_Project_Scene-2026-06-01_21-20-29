using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StoryEncounterStage
{
    [Tooltip("階段標籤 (僅在 Inspector 方便辨識)")]
    public string stageName = "第 1 次遇到";

    [Header("此階段播放的對話文字清單")]
    [TextArea(2, 5)]
    public string[] messages;
}

public class AdaptiveStoryTrigger : MonoBehaviour
{
    [Header("1. 分類與優先級")]
    [Tooltip("事件 ID：Monster1, Monster2, Monster3, LaserPuzzle, LaserPuzzle3D")]
    public string puzzleTypeID = "LaserPuzzle";
    [Tooltip("High (怪物/故事): 可打斷低優先\nLow (謎題教學): 遇高優先暫緩播放")]
    public StoryPriority priority = StoryPriority.Low;

    [Header("2. 存檔設定")]
    [Tooltip("勾選後，遊戲關掉重開依然會記憶 encounters 次數")]
    public bool saveAcrossSessions = true;

    [Header("3. Monster 3 特殊干擾 (直接對接現有的 SoundMonsterAI2)")]
    [Tooltip("拖入場景中的 Monster 3 (聽力型怪物)")]
    public SoundMonsterAI2 monster3Instance;

    [Tooltip("當 Monster 3 處於警戒/追擊/攻擊狀態時要播放的干擾對話 (可選)")]
    [TextArea(2, 5)]
    public string[] monster3InterferenceMessages;

    [Header("4. 各階段對話清單 (按次數順序)")]
    [Tooltip("Element 0 = 第 1 次, Element 1 = 第 2 次... 超過清單長度則自動保持安靜")]
    public List<StoryEncounterStage> encounterStages = new List<StoryEncounterStage>();

    [Header("5. 顯示與系統引用")]
    public float displayTime = 3f;
    public StoryManager storyManager;

    private bool hasTriggeredThisLocation = false;
    private static Dictionary<string, int> s_SessionCounts = new Dictionary<string, int>();

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggeredThisLocation || !other.CompareTag("Player") || storyManager == null) return;

        // 若目前正播放 High Priority，且自己是 Low Priority，先不觸發，留待玩家下次回頭重踩
        if (storyManager.IsPlayingStory && 
            storyManager.CurrentPriority == StoryPriority.High && 
            priority == StoryPriority.Low)
        {
            return;
        }

        // 檢查 Monster 3 是否處於 Alert (警戒)、Chase (追擊) 或 Attack (攻擊) 的主動干擾狀態
        if (monster3Instance != null && 
            monster3Instance.currentState != SoundMonsterAI2.State.Patrol && 
            monster3InterferenceMessages != null && 
            monster3InterferenceMessages.Length > 0 &&
            !string.IsNullOrWhiteSpace(monster3InterferenceMessages[0]))
        {
            hasTriggeredThisLocation = true;
            storyManager.ShowStories(monster3InterferenceMessages, displayTime, StoryPriority.High);
            return;
        }

        hasTriggeredThisLocation = true;

        int currentCount = GetAndIncreaseEncounterCount(puzzleTypeID);
        int stageIndex = currentCount - 1;

        if (stageIndex >= 0 && stageIndex < encounterStages.Count)
        {
            string[] selectedMessages = encounterStages[stageIndex].messages;
            if (selectedMessages != null && selectedMessages.Length > 0 && !string.IsNullOrWhiteSpace(selectedMessages[0]))
            {
                storyManager.ShowStories(selectedMessages, displayTime, priority);
            }
        }
    }

    private int GetAndIncreaseEncounterCount(string id)
    {
        int count = 0;
        if (saveAcrossSessions)
        {
            string key = "EncounterCount_" + id;
            count = PlayerPrefs.GetInt(key, 0) + 1;
            PlayerPrefs.SetInt(key, count);
            PlayerPrefs.Save();
        }
        else
        {
            if (!s_SessionCounts.ContainsKey(id)) s_SessionCounts[id] = 0;
            s_SessionCounts[id]++;
            count = s_SessionCounts[id];
        }
        return count;
    }

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