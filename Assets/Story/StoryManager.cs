using System;
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

    [Header("系統與快對話設定")]
    [Tooltip("是否允許滑鼠跳過/快進")]
    public bool allowMouseSkip = true;

    [Tooltip("獨立控管快進對話的滑鼠按鈕 (0 = 左鍵, 1 = 右鍵)")]
    public int skipMouseButton = 0; // 0 = 滑鼠左鍵

    [Tooltip("無配音檔時，或配音播完後的預設停留時間（秒）")]
    public float postVoiceHoldTime = 1.5f;

    private Coroutine storyCoroutine;
    private bool skipCurrentMessage = false; // 無條件直接跳過當前整句，切至下一句
    private bool isPlayingStory = false;

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

        // 按下左鍵 (skipMouseButton = 0) -> 無條件觸發「跳過當前這句」
        if (Input.GetMouseButtonDown(skipMouseButton))
        {
            skipCurrentMessage = true;
        }
    }

    // ==========================================
    // 【核心方法】：支援對話結束 Callback 回傳 (用於解鎖定格)
    // ==========================================
    public void ShowStories(string[] messages, AudioClip[] voiceClips, int stagePriority, Action onComplete = null)
    {
        if (storyPanel == null || storyText == null || messages == null || messages.Length == 0)
        {
            onComplete?.Invoke();
            return;
        }

        // 優先度判定：若新事件優先度 <= 當前播放中的優先度，則不打斷，以先遇到的為主
        if (isPlayingStory && stagePriority <= currentPriorityLevel)
        {
            Debug.Log($"[StoryManager] 當前正在播放優先度 Level {currentPriorityLevel} 的對話，無視優先度 Level {stagePriority} 的新事件。");
            return;
        }

        // 新事件優先度更高，停止上一段語音與字串顯示
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
        isPlayingStory = true;

        storyCoroutine = StartCoroutine(ShowStoriesRoutine(messages, voiceClips, onComplete));
    }

    // ==========================================
    // 【向下相容過渡方法】
    // ==========================================
    public void ShowStories(string[] messages, AudioClip[] voiceClips, int stagePriority)
    {
        ShowStories(messages, voiceClips, stagePriority, null);
    }

    public void ShowStories(string[] messages, float duration)
    {
        ShowStories(messages, null, 1, null);
    }

    public void ShowStories(string[] messages, float duration, StoryPriority priority)
    {
        int pLevel = (priority == StoryPriority.High) ? 3 : 1;
        ShowStories(messages, null, pLevel, null);
    }

    public void ShowStory(string message, float duration = 3f)
    {
        ShowStories(new string[] { message }, null, 1, null);
    }

    // ==========================================
    // 【無條件左鍵快照/跳過 Routine】
    // ==========================================
    private IEnumerator ShowStoriesRoutine(string[] messages, AudioClip[] voiceClips, Action onComplete)
    {
        storyPanel.SetActive(true);

        for (int index = 0; index < messages.Length; index++)
        {
            string message = messages[index];
            if (string.IsNullOrWhiteSpace(message)) continue;

            AudioClip clip = (voiceClips != null && index < voiceClips.Length) ? voiceClips[index] : null;

            skipCurrentMessage = false; // 每句開始前重置跳過旗標

            storyText.text = message;
            storyText.ForceMeshUpdate();

            int totalCharacters = storyText.textInfo.characterCount;
            storyText.maxVisibleCharacters = 0;

            // 計算逐字印出速度
            float audioDuration = (clip != null) ? clip.length : (totalCharacters * 0.05f);
            float delayPerChar = (totalCharacters > 0) ? (audioDuration / totalCharacters) : 0f;

            // 播放 AI 配音
            if (clip != null && voiceAudioSource != null)
            {
                voiceAudioSource.clip = clip;
                voiceAudioSource.Play();
            }

            // --- 1. 打字機階段 (若中途按左鍵，立即停止配音並跳出打字機) ---
            for (int i = 0; i < totalCharacters; i++)
            {
                if (skipCurrentMessage)
                {
                    if (voiceAudioSource != null && voiceAudioSource.isPlaying)
                    {
                        voiceAudioSource.Stop();
                    }
                    break;
                }

                storyText.maxVisibleCharacters = i + 1;
                if (delayPerChar > 0f) yield return new WaitForSeconds(delayPerChar);
                else yield return null;
            }

            // 🚨 若在打字階段按了左鍵：直接跳過等待階段，立即進入迴圈的下一句！
            if (skipCurrentMessage)
            {
                if (voiceAudioSource != null && voiceAudioSource.isPlaying)
                {
                    voiceAudioSource.Stop();
                }
                continue;
            }

            // 確保正常播放時全字顯示
            storyText.maxVisibleCharacters = totalCharacters;

            // --- 2. 配音播報等待階段 ---
            if (voiceAudioSource != null && clip != null)
            {
                while (voiceAudioSource.isPlaying && !skipCurrentMessage)
                {
                    yield return null;
                }
            }

            if (skipCurrentMessage)
            {
                if (voiceAudioSource != null && voiceAudioSource.isPlaying)
                {
                    voiceAudioSource.Stop();
                }
                continue;
            }

            // --- 3. 停留時間階段 ---
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
        isPlayingStory = false;
        currentPriorityLevel = 0;
        storyCoroutine = null;

        // 觸發 Callback，解鎖玩家與怪物定格
        onComplete?.Invoke();
    }
}