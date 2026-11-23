
using System;
using System.Collections.Generic;
using UnityEngine;

public class StoryTrigger : MonoBehaviour
{
    [Header("Story Database 設定")]

    [Tooltip("勾選後從 StoryDatabase 讀取劇情")]
    public bool useDatabase = false;

    [Tooltip("指定 StoryDatabaseSO")]
    public StoryDatabaseSO storyDatabase;

    [Tooltip("事件 ID，例如 Game4、BadEnding、GoodEnding")]
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

    [Tooltip("勾選後，進入場景就自動播放劇情")]
    public bool playOnStart = false;

    [Tooltip("劇情優先權：1=低，2=中，3=高")]
    [Range(1, 3)]
    public int stagePriority = 1;

    [Tooltip("是否只觸發一次")]
    public bool triggerOnce = true;


    [Header("永久播放記錄設定")]

    [Tooltip("勾選後，即使重新載入場景也不會再次播放")]
    public bool rememberPlayedStory = false;

    [Tooltip("此劇情的唯一識別名稱")]
    public string storyID = "Intro_01";


    [Header("Debug")]

    public bool showDebugLog = true;


    private bool hasTriggered = false;


    // ======================================
    // 取得劇情儲存 Key
    // ======================================

    private string GetStorySaveKey()
    {
        return "StoryPlayed:" +
               gameObject.scene.name +
               ":" + storyID;
    }


    // ======================================
    // 進入場景自動播放
    // ======================================

    private void Start()
    {
        if (playOnStart)
        {
            PlayStory();
        }
    }


    // ======================================
    // 玩家進入觸發區域
    // ======================================

    private void OnTriggerEnter(Collider other)
    {
        // 使用自動播放模式時，不需要碰撞觸發
        if (playOnStart)
        {
            return;
        }

        // 確認玩家進入
        bool isPlayer =
            other.CompareTag("Player") ||
            other.transform.root.CompareTag("Player");

        if (!isPlayer)
        {
            return;
        }

        PlayStory();
    }


    // ======================================
    // 統一播放劇情
    // ======================================

    public void PlayStory()
    {
        // 檢查本次場景是否已經觸發
        if (triggerOnce && hasTriggered)
        {
            return;
        }

        // 檢查永久播放記錄
        if (rememberPlayedStory)
        {
            if (PlayerPrefs.GetInt(
                GetStorySaveKey(), 0) == 1)
            {
                if (showDebugLog)
                {
                    Debug.Log(
                        "[StoryTrigger] 劇情已播放過：" +
                        GetStorySaveKey()
                    );
                }

                return;
            }
        }

        // 自動尋找 StoryManager
        if (storyManager == null)
        {
            storyManager =
                FindFirstObjectByType<StoryManager>();
        }

        if (storyManager == null)
        {
            Debug.LogWarning(
                "[StoryTrigger] 找不到 StoryManager！"
            );

            return;
        }

        // 檢查目前播放中的劇情優先權
        if (storyManager.IsPlayingStory &&
            stagePriority <=
            storyManager.CurrentPriorityLevel)
        {
            if (showDebugLog)
            {
                Debug.Log(
                    "[StoryTrigger] 目前有相同或更高" +
                    "優先權的劇情正在播放"
                );
            }

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

            List<StoryEncounterStage> stages =
                storyDatabase.GetStagesForEvent(
                    eventTypeKey
                );

            if (stages == null || stages.Count == 0)
            {
                Debug.LogWarning(
                    "[StoryTrigger] 找不到事件：" +
                    eventTypeKey
                );

                return;
            }

            StoryEncounterStage selectedStage = null;

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

        bool hasValidMessage = false;

        foreach (string message in messages)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                hasValidMessage = true;
                break;
            }
        }

        if (!hasValidMessage)
        {
            Debug.LogWarning(
                "[StoryTrigger] 所有劇情文字都是空白！"
            );

            return;
        }

        // 檢查字幕 UI
        if (storyManager.storyPanel == null ||
            storyManager.storyText == null)
        {
            Debug.LogWarning(
                "[StoryTrigger] StoryManager UI 尚未設定！"
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


        // ======================================
        // 儲存永久播放記錄
        // ======================================

        if (rememberPlayedStory)
        {
            PlayerPrefs.SetInt(
                GetStorySaveKey(),
                1
            );

            PlayerPrefs.Save();

            if (showDebugLog)
            {
                Debug.Log(
                    "[StoryTrigger] 已儲存劇情播放記錄：" +
                    GetStorySaveKey()
                );
            }
        }

        if (showDebugLog)
        {
            Debug.Log(
                "[StoryTrigger] 開始播放劇情：" +
                eventTypeKey + " / " + stageName
            );
        }
    }


    // ======================================
    // 手動清除劇情播放記錄
    // ======================================

    [ContextMenu("Reset Story Record")]
    public void ResetStoryRecord()
    {
        hasTriggered = false;

        PlayerPrefs.DeleteKey(
            GetStorySaveKey()
        );

        PlayerPrefs.Save();

        Debug.Log(
            "[StoryTrigger] 已清除劇情播放記錄：" +
            GetStorySaveKey()
        );
    }
}
