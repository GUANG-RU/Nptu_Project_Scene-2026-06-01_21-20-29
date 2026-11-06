using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StoryEventConfig
{
    [Tooltip("事件標籤 ID (例如 Monster1, Monster2, LaserPuzzle)")]
    public string eventTypeKey = "Monster1";

    [Tooltip("該事件包含的各階段對話與語音 (First, Second, Third...)")]
    public List<StoryEncounterStage> encounterStages = new List<StoryEncounterStage>();
}

[CreateAssetMenu(fileName = "StoryDatabase", menuName = "Story System/Story Database")]
public class StoryDatabaseSO : ScriptableObject
{
    [Header("全遊戲對話總資料庫")]
    public List<StoryEventConfig> eventConfigs = new List<StoryEventConfig>();

    /// <summary>
    /// 根據事件 ID 尋找對應的遭遇階段清單
    /// </summary>
    public List<StoryEncounterStage> GetStagesForEvent(string key)
    {
        foreach (var config in eventConfigs)
        {
            if (config != null && config.eventTypeKey.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return config.encounterStages;
            }
        }
        return null;
    }
}