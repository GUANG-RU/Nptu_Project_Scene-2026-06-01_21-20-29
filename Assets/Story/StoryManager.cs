using System.Collections;
using UnityEngine;
using TMPro;

public enum StoryPriority
{
    Low = 0,   // 謎題教學、環境思考 (可被 High 打斷，或在忙碌時暫緩)
    High = 1   // 怪物登場、危機警告、劇情高潮 (可強制打斷 Low)
}

public class StoryManager : MonoBehaviour
{
    [Header("UI 連結")]
    public GameObject storyPanel;
    public TMP_Text storyText;

    [Header("打字機與跳過設定")]
    [Tooltip("每個字出現的間隔（秒），越小越快")]
    public float typingSpeed = 0.05f;
    [Tooltip("是否允許滑鼠左鍵跳過目前這段")]
    public bool allowMouseSkip = true;

    private Coroutine storyCoroutine;
    private bool skipCurrentMessage = false;
    private bool isPlayingStory = false;
    private StoryPriority currentPriority = StoryPriority.Low;

    public bool IsPlayingStory => isPlayingStory;
    public StoryPriority CurrentPriority => currentPriority;

    void Start()
    {
        if (storyPanel != null) storyPanel.SetActive(false);
    }

    void Update()
    {
        if (!isPlayingStory || !allowMouseSkip) return;

        if (Input.GetMouseButtonDown(0))
        {
            skipCurrentMessage = true;
        }
    }

    public void ShowStory(string message, float duration)
    {
        ShowStories(new string[] { message }, duration, StoryPriority.Low);
    }

    public void ShowStories(string[] messages, float duration, StoryPriority priority = StoryPriority.Low)
    {
        if (storyPanel == null || storyText == null || messages == null || messages.Length == 0) return;

        // 若正在播放 High Priority 對話，且新輸入為 Low Priority，直接阻擋
        if (isPlayingStory && currentPriority == StoryPriority.High && priority == StoryPriority.Low)
        {
            Debug.Log("[StoryManager] 正在播放高優先級劇情，無視低優先對話。");
            return;
        }

        // 停止上一段對話 (若新訊息為 High Priority，可強制打斷正在播的 Low Priority)
        if (storyCoroutine != null)
        {
            StopCoroutine(storyCoroutine);
            storyCoroutine = null;
        }

        currentPriority = priority;
        skipCurrentMessage = false;
        isPlayingStory = true;

        storyCoroutine = StartCoroutine(ShowStoriesRoutine(messages, duration));
    }

    private IEnumerator ShowStoriesRoutine(string[] messages, float duration)
    {
        storyPanel.SetActive(true);

        foreach (string message in messages)
        {
            if (string.IsNullOrWhiteSpace(message)) continue;

            skipCurrentMessage = false;
            storyText.text = message;
            storyText.ForceMeshUpdate();

            int totalCharacters = storyText.textInfo.characterCount;
            storyText.maxVisibleCharacters = 0;

            // 逐字打字機
            for (int i = 0; i < totalCharacters; i++)
            {
                if (skipCurrentMessage) break;

                storyText.maxVisibleCharacters = i + 1;
                char letter = storyText.textInfo.characterInfo[i].character;
                float delay = typingSpeed;

                // 使用 Unicode 碼避免檔案編碼問題 (\u3002=句號, \uFF01=驚嘆號, \uFF1F=問號, \uFF0C=逗號, \u2026=省略號)
                if (letter == '\u3002' || letter == '\uFF01' || letter == '\uFF1F' || letter == '!' || letter == '?')
                {
                    delay *= 6f;
                }
                else if (letter == '\uFF0C' || letter == ',' || letter == '\u2026')
                {
                    delay *= 3f;
                }

                if (delay > 0f) yield return new WaitForSeconds(delay);
                else yield return null;
            }

            // 打完字後的留存停留
            float timer = 0f;
            while (!skipCurrentMessage && timer < duration)
            {
                timer += Time.deltaTime;
                yield return null;
            }
        }

        storyPanel.SetActive(false);
        skipCurrentMessage = false;
        isPlayingStory = false;
        storyCoroutine = null;
        currentPriority = StoryPriority.Low;
    }
}