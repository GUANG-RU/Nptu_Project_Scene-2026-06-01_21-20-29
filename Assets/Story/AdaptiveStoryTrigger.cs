using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StoryEncounterStage
{
    [Tooltip("階段標籤 (First / Second / Third)")]
    public string stageName = "First";

    [Header("此階段播放的對話文字與對應配音")]
    [TextArea(2, 5)]
    public string[] messages;

    [Tooltip("對應上述字幕的 AI 配音檔 (數量與順序須與 Messages 一致)")]
    public AudioClip[] voiceClips;
}

public class AdaptiveStoryTrigger : MonoBehaviour
{
    [Header("1. 分類 ID")]
    [Tooltip("事件 ID：Monster1, Monster2, Monster3, LaserPuzzle, LaserPuzzle3D")]
    public string puzzleTypeID = "LaserPuzzle";

    [Header("2. 存檔設定")]
    [Tooltip("勾選後，關掉遊戲重開依然記憶 encounters 次數")]
    public bool saveAcrossSessions = true;

    [Header("3. Monster 3 特殊干擾 (選擇性)")]
    public SoundMonsterAI2 monster3Instance;
    [TextArea(2, 5)]
    public string[] monster3InterferenceMessages;
    public AudioClip[] monster3InterferenceVoices;

    [Header("4. 各階段對話清單 (Element 0 = First, Element 1 = Second, Element 2 = Third)")]
    public List<StoryEncounterStage> encounterStages = new List<StoryEncounterStage>();

    [Header("5. 系統引用")]
    public StoryManager storyManager;

    private bool hasTriggeredThisLocation = false;
    private static Dictionary<string, int> s_SessionCounts = new Dictionary<string, int>();

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggeredThisLocation || !other.CompareTag("Player") || storyManager == null) return;

        // 計算目前遇到的階段 (0 = First, 1 = Second, 2 = Third)
        int currentCount = GetAndIncreaseEncounterCount(puzzleTypeID);
        int stageIndex = currentCount - 1;

        // 計算階段優先度：First (stageIndex 0) = 優先度 3, Second = 2, Third = 1
        int stagePriority = Mathf.Max(1, 3 - stageIndex);

        // 檢查 Monster 3 是否處於 Alert/Chase/Attack 干擾狀態
        if (monster3Instance != null && 
            monster3Instance.currentState != SoundMonsterAI2.State.Patrol && 
            monster3InterferenceMessages != null && 
            monster3InterferenceMessages.Length > 0 &&
            !string.IsNullOrWhiteSpace(monster3InterferenceMessages[0]))
        {
            hasTriggeredThisLocation = true;
            // 干擾緊急對話一律視為最高優先度 (3)
            storyManager.ShowStories(monster3InterferenceMessages, monster3InterferenceVoices, 3);
            return;
        }

        hasTriggeredThisLocation = true;

        if (stageIndex >= 0 && stageIndex < encounterStages.Count)
        {
            StoryEncounterStage stage = encounterStages[stageIndex];
            if (stage.messages != null && stage.messages.Length > 0 && !string.IsNullOrWhiteSpace(stage.messages[0]))
            {
                storyManager.ShowStories(stage.messages, stage.voiceClips, stagePriority);
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