
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
    public AudioSource voiceAudioSource;

    [Header("系統與快對話設定")]

    [Tooltip("是否允許滑鼠跳過/快進")]
    public bool allowMouseSkip = true;

    [Tooltip("0 = 左鍵, 1 = 右鍵")]
    public int skipMouseButton = 0;

    [Tooltip("配音播完後的字幕停留時間")]
    public float postVoiceHoldTime = 1.5f;

    private Coroutine storyCoroutine;

    private bool skipCurrentMessage = false;
    private bool isPlayingStory = false;

    private int currentPriorityLevel = 0;

    public bool IsPlayingStory => isPlayingStory;
    public int CurrentPriorityLevel => currentPriorityLevel;


    // ==========================================
    // 修正：使用 Awake 初始化
    // ==========================================

    private void Awake()
    {
        // 在 StoryTrigger.Start() 之前完成初始化
        if (storyPanel != null)
        {
            storyPanel.SetActive(false);
        }

        if (voiceAudioSource == null)
        {
            voiceAudioSource =
                GetComponent<AudioSource>();
        }

        if (voiceAudioSource != null)
        {
            voiceAudioSource.playOnAwake = false;
        }

        isPlayingStory = false;
        currentPriorityLevel = 0;
        skipCurrentMessage = false;
    }


    // ==========================================
    // 滑鼠跳過
    // ==========================================

    private void Update()
    {
        if (!isPlayingStory || !allowMouseSkip)
        {
            return;
        }

        if (Input.GetMouseButtonDown(skipMouseButton))
        {
            skipCurrentMessage = true;
        }
    }


    // ==========================================
    // 播放劇情
    // ==========================================

    public void ShowStories(
        string[] messages,
        AudioClip[] voiceClips,
        int stagePriority,
        Action onComplete = null)
    {
        if (storyPanel == null ||
            storyText == null ||
            messages == null ||
            messages.Length == 0)
        {
            Debug.LogWarning(
                "[StoryManager] 字幕 UI 或劇情內容未設定！"
            );

            onComplete?.Invoke();
            return;
        }

        // 優先權檢查
        if (isPlayingStory &&
            stagePriority <= currentPriorityLevel)
        {
            Debug.Log(
                "[StoryManager] 目前正在播放優先權 " +
                currentPriorityLevel +
                " 的劇情，忽略優先權 " +
                stagePriority + " 的新劇情。"
            );

            return;
        }

        // 停止上一段劇情
        if (storyCoroutine != null)
        {
            StopCoroutine(storyCoroutine);
            storyCoroutine = null;
        }

        if (voiceAudioSource != null &&
            voiceAudioSource.isPlaying)
        {
            voiceAudioSource.Stop();
        }

        currentPriorityLevel = stagePriority;
        skipCurrentMessage = false;
        isPlayingStory = true;

        // 啟動字幕與語音
        storyCoroutine = StartCoroutine(
            ShowStoriesRoutine(
                messages,
                voiceClips,
                onComplete
            )
        );
    }


    // ==========================================
    // 向下相容方法
    // ==========================================

    public void ShowStories(
        string[] messages,
        AudioClip[] voiceClips,
        int stagePriority)
    {
        ShowStories(
            messages,
            voiceClips,
            stagePriority,
            null
        );
    }

    public void ShowStories(
        string[] messages,
        float duration)
    {
        ShowStories(messages, null, 1, null);
    }

    public void ShowStories(
        string[] messages,
        float duration,
        StoryPriority priority)
    {
        int pLevel =
            priority == StoryPriority.High ? 3 : 1;

        ShowStories(messages, null, pLevel, null);
    }

    public void ShowStory(
        string message,
        float duration = 3f)
    {
        ShowStories(
            new string[] { message },
            null,
            1,
            null
        );
    }


    // ==========================================
    // 字幕與語音播放流程
    // ==========================================

    private IEnumerator ShowStoriesRoutine(
        string[] messages,
        AudioClip[] voiceClips,
        Action onComplete)
    {
        // 開始播放時顯示字幕面板
        storyPanel.SetActive(true);

        Debug.Log(
            "[StoryManager] StoryPanel 已開啟"
        );

        for (int index = 0;
             index < messages.Length;
             index++)
        {
            string message = messages[index];

            if (string.IsNullOrWhiteSpace(message))
            {
                continue;
            }

            AudioClip clip =
                voiceClips != null &&
                index < voiceClips.Length
                ? voiceClips[index]
                : null;

            skipCurrentMessage = false;

            // 設定字幕
            storyText.text = message;
            storyText.ForceMeshUpdate();

            int totalCharacters =
                storyText.textInfo.characterCount;

            storyText.maxVisibleCharacters = 0;

            // 根據語音長度計算打字速度
            float audioDuration =
                clip != null
                ? clip.length
                : totalCharacters * 0.05f;

            float delayPerChar =
                totalCharacters > 0
                ? audioDuration / totalCharacters
                : 0f;


            // ==================================
            // 播放語音
            // ==================================

            if (clip != null &&
                voiceAudioSource != null)
            {
                voiceAudioSource.Stop();
                voiceAudioSource.clip = clip;
                voiceAudioSource.Play();
            }


            // ==================================
            // 逐字顯示字幕
            // ==================================

            for (int i = 0;
                 i < totalCharacters;
                 i++)
            {
                if (skipCurrentMessage)
                {
                    if (voiceAudioSource != null &&
                        voiceAudioSource.isPlaying)
                    {
                        voiceAudioSource.Stop();
                    }

                    break;
                }

                storyText.maxVisibleCharacters = i + 1;

                if (delayPerChar > 0f)
                {
                    yield return new WaitForSeconds(
                        delayPerChar
                    );
                }
                else
                {
                    yield return null;
                }
            }


            // ==================================
            // 左鍵跳過目前句子
            // ==================================

            if (skipCurrentMessage)
            {
                if (voiceAudioSource != null &&
                    voiceAudioSource.isPlaying)
                {
                    voiceAudioSource.Stop();
                }

                continue;
            }


            // ==================================
            // 完整顯示字幕
            // ==================================

            storyText.maxVisibleCharacters =
                totalCharacters;


            // ==================================
            // 等待配音播放完畢
            // ==================================

            if (voiceAudioSource != null &&
                clip != null)
            {
                while (voiceAudioSource.isPlaying &&
                       !skipCurrentMessage)
                {
                    yield return null;
                }
            }

            if (skipCurrentMessage)
            {
                if (voiceAudioSource != null &&
                    voiceAudioSource.isPlaying)
                {
                    voiceAudioSource.Stop();
                }

                continue;
            }


            // ==================================
            // 字幕停留時間
            // ==================================

            float timer = 0f;

            while (!skipCurrentMessage &&
                   timer < postVoiceHoldTime)
            {
                timer += Time.deltaTime;
                yield return null;
            }
        }


        // ======================================
        // 全部劇情播放完成
        // ======================================

        if (voiceAudioSource != null &&
            voiceAudioSource.isPlaying)
        {
            voiceAudioSource.Stop();
        }

        storyPanel.SetActive(false);

        skipCurrentMessage = false;
        isPlayingStory = false;
        currentPriorityLevel = 0;
        storyCoroutine = null;

        Debug.Log(
            "[StoryManager] 劇情播放完畢，StoryPanel 已關閉"
        );

        // 通知其他系統劇情結束
        onComplete?.Invoke();
    }
}
