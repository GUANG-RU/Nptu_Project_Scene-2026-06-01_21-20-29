using System;
using System.Collections.Generic;
using UnityEngine;

public class StoryTrigger : MonoBehaviour
{
    [Header("Story Database 設定")]

    [Tooltip("勾選後從 StoryDatabase 讀取劇情")]
    public bool useDatabase = false;

    [Tooltip("指定原本的 StoryDatabaseSO")]
    public StoryDatabaseSO storyDatabase;

    [Tooltip("事件 ID，例如 Intro、Monster1、Room02")]
    public string eventTypeKey = "Intro";

    [Tooltip("階段名稱，例如 First、Second、Third")]
    public string stageName = "First";


    [Header("舊版劇情設定（不使用資料庫時）")]

    [TextArea(3, 6)]
    public string storyMessage;

    [TextArea(3, 6)]
    public string[] followingMessages;

    public float displayTime = 3f;


    [Header("播放設定")]

    public StoryManager storyManager;

    [Tooltip("劇情優先權：1=低，2=中，3=高")]
    [Range(1, 3)]
    public int stagePriority = 1;

    [Tooltip("是否只觸發一次")]
    public bool triggerOnce = true;


    private bool hasTriggered = false;


    void OnTriggerEnter(Collider other)
    {
        // 確認玩家進入
        if (!other.CompareTag("Player"))
        {
            return;
        }

        // 已經觸發過
        if (triggerOnce && hasTriggered)
        {
            return;
        }

        // 檢查 StoryManager
        if (storyManager == null)
        {
            Debug.LogWarning(
                "[StoryTrigger] StoryManager 尚未指定！"
            );

            return;
        }

        // 檢查目前播放中的劇情優先權
        if (storyManager.IsPlayingStory &&
            stagePriority <= storyManager.CurrentPriorityLevel)
        {
            Debug.Log(
                "[StoryTrigger] 目前有相同或更高優先權的劇情正在播放"
            );

            return;
        }


        string[] messages = null;
        AudioClip[] voiceClips = null;


        // ======================================
        // 模式 A：從 StoryDatabase 讀取
        // ======================================
        if (useDatabase)
        {
            if (storyDatabase == null)
            {
                Debug.LogWarning(
                    "[StoryTrigger] StoryDatabase 尚未指定！"
                );

                return;
            }

            // 根據 Event Type Key 找到事件
            List<StoryEncounterStage> stages =
                storyDatabase.GetStagesForEvent(
                    eventTypeKey
                );

            if (stages == null)
            {
                Debug.LogWarning(
                    "[StoryTrigger] 找不到事件：" +
                    eventTypeKey
                );

                return;
            }


            StoryEncounterStage selectedStage = null;

            // 尋找指定階段
            foreach (StoryEncounterStage stage in stages)
            {
                if (stage == null)
                {
                    continue;
                }

                if (string.Equals(
                    stage.stageName,
                    stageName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    selectedStage = stage;
                    break;
                }
            }


            if (selectedStage == null)
            {
                Debug.LogWarning(
                    "[StoryTrigger] 找不到階段：" +
                    eventTypeKey + " / " + stageName
                );

                return;
            }


            // 直接取得資料庫內的文字與語音
            messages = selectedStage.messages;
            voiceClips = selectedStage.voiceClips;
        }


        // ======================================
        // 模式 B：使用原本的文字設定
        // ======================================
        else
        {
            List<string> messageList =
                new List<string>();

            if (!string.IsNullOrWhiteSpace(storyMessage))
            {
                messageList.Add(storyMessage);
            }

            if (followingMessages != null)
            {
                foreach (string message in followingMessages)
                {
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        messageList.Add(message);
                    }
                }
            }

            messages = messageList.ToArray();
            voiceClips = null;
        }


        // ======================================
        // 檢查劇情內容
        // ======================================
        if (messages == null || messages.Length == 0)
        {
            Debug.LogWarning(
                "[StoryTrigger] 沒有可播放的劇情文字！"
            );

            return;
        }


        // ======================================
        // 開始播放
        // ======================================
        storyManager.ShowStories(
            messages,
            voiceClips,
            stagePriority,
            null
        );

        hasTriggered = true;

        Debug.Log(
            "[StoryTrigger] 開始播放劇情：" +
            eventTypeKey + " / " + stageName
        );
    }
}