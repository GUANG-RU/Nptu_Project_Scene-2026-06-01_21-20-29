using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public enum StoryPriority
{
    Low = 0,
    High = 1
}

public class StoryManager : MonoBehaviour
{
    [Header("UI 與音效連結")]
    public GameObject storyPanel;
    public TMP_Text storyText;
    public AudioSource voiceAudioSource; // 用於播放 AI 語音的 AudioSource

    [Header("系統設定")]
    [Tooltip("是否允許配音播完後按滑鼠左鍵跳過")]
    public bool allowMouseSkip = true;
    [Tooltip("無配音檔時，或配音播完後的預設停留時間（秒）")]
    public float postVoiceHoldTime = 1.5f;

    private Coroutine storyCoroutine;
    private bool skipCurrentMessage = false;
    private bool isPlayingStory = false;
    private bool canSkip = false; // 只有配音完全播完後才會變為 true

    // 目前正在播放的遭遇階段優先級：First = 3, Second = 2, Third = 1, 無 = 0
    private int currentPriorityLevel = 0;

    public bool IsPlayingStory => isPlayingStory;
    public int CurrentPriorityLevel => currentPriorityLevel;

    void Start()
    {
        if (storyPanel != null) storyPanel.SetActive(false);
        if (voiceAudioSource == null) voiceAudioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        if (!isPlayingStory || !allowMouseSkip) return;

        // 只有當 canSkip == true (配音已完整播完) 且按下左鍵時，才允許切換到下一句
        if (canSkip && Input.GetMouseButtonDown(0))
        {
            skipCurrentMessage = true;
        }
    }

    // ==========================================
    // 【核心方法】：支援語音檔與階梯優先度 (First=3, Second=2, Third=1)
    // ==========================================
    public void ShowStories(string[] messages, AudioClip[] voiceClips, int stagePriority)
    {
        if (storyPanel == null || storyText == null || messages == null || messages.Length == 0) return;

        // 優先度判定：若新事件優先度 <= 當前播放中的優先度，則不打斷，以先遇到的為主
        if (isPlayingStory && stagePriority <= currentPriorityLevel)
        {
            Debug.Log($"[StoryManager] 當前正在播放優先度 Level {currentPriorityLevel} 的對話，無視優先度 Level {stagePriority} 的新事件。");
            return;
        }

        // 新事件優先度更高 (例如 First 打斷 Second)，停止上一段語音與字串顯示
        if (storyCoroutine != null)
        {
            StopCoroutine(storyCoroutine);
            storyCoroutine = null;
        }

        if (voiceAudioSource != null && voiceAudioSource.isPlaying)
        {
            voiceAudioSource.Stop();
        }

        currentPriorityLevel = stagePriority;
        skipCurrentMessage = false;
        canSkip = false;
        isPlayingStory = true;

        storyCoroutine = StartCoroutine(ShowStoriesRoutine(messages, voiceClips));
    }

    // ==========================================
    // 【向下相容過渡方法】：完全相容 StoryTrigger.cs 與舊版呼叫
    // ==========================================
    
    // 相容 StoryTrigger.cs (string[], float) 的呼叫
    public void ShowStories(string[] messages, float duration)
    {
        ShowStories(messages, null, 1);
    }

    // 相容舊版 (string[], float, StoryPriority) 的呼叫
    public void ShowStories(string[] messages, float duration, StoryPriority priority)
    {
        int pLevel = (priority == StoryPriority.High) ? 3 : 1;
        ShowStories(messages, null, pLevel);
    }

    // 相容舊版單句 ShowStory
    public void ShowStory(string message, float duration = 3f)
    {
        ShowStories(new string[] { message }, null, 1);
    }

    // ==========================================
    // 【打字機與語音同步 Routine】
    // ==========================================
    private IEnumerator ShowStoriesRoutine(string[] messages, AudioClip[] voiceClips)
    {
        storyPanel.SetActive(true);

        for (int index = 0; index < messages.Length; index++)
        {
            string message = messages[index];
            if (string.IsNullOrWhiteSpace(message)) continue;

            AudioClip clip = (voiceClips != null && index < voiceClips.Length) ? voiceClips[index] : null;

            skipCurrentMessage = false;
            canSkip = false; // 語音播報期間鎖定，無法快轉

            storyText.text = message;
            storyText.ForceMeshUpdate();

            int totalCharacters = storyText.textInfo.characterCount;
            storyText.maxVisibleCharacters = 0;

            // 計算逐字印出速度：若有 Audio 則完全對齊音訊總秒數，若無則使用預設率 (每字 0.05 秒)
            float audioDuration = (clip != null) ? clip.length : (totalCharacters * 0.05f);
            float delayPerChar = (totalCharacters > 0) ? (audioDuration / totalCharacters) : 0f;

            // 播放 AI 配音
            if (clip != null && voiceAudioSource != null)
            {
                voiceAudioSource.clip = clip;
                voiceAudioSource.Play();
            }

            // 打字機階段 (對齊語音長度)
            for (int i = 0; i < totalCharacters; i++)
            {
                storyText.maxVisibleCharacters = i + 1;
                if (delayPerChar > 0f) yield return new WaitForSeconds(delayPerChar);
                else yield return null;
            }

            storyText.maxVisibleCharacters = totalCharacters;

            // 等待 Audio 實體播放完畢 (確保音效無殘音)
            if (voiceAudioSource != null && clip != null)
            {
                while (voiceAudioSource.isPlaying)
                {
                    yield return null;
                }
            }

            // 配音完全播完，解鎖快轉跳過功能！
            canSkip = true;

            // 留存停留階段 (玩家此時可按左鍵手動跳下一句，或等待停留時間結束)
            float timer = 0f;
            while (!skipCurrentMessage && timer < postVoiceHoldTime)
            {
                timer += Time.deltaTime;
                yield return null;
            }
        }

        // 全對話結束，重置 UI 與狀態
        storyPanel.SetActive(false);
        skipCurrentMessage = false;
        canSkip = false;
        isPlayingStory = false;
        currentPriorityLevel = 0;
        storyCoroutine = null;
    }
}