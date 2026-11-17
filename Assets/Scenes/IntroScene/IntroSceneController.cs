
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class IntroSceneController : MonoBehaviour
{
    [Header("劇情資料庫")]
    public StoryDatabaseSO storyDatabase;

    [Header("劇情管理器")]
    public StoryManager storyManager;

    [Header("開場劇情設定")]
    public string eventTypeKey = "Intro";
    public string stageName = "First";

    [Header("黑畫面設定")]
    public GameObject blackScreen;

    [Tooltip("劇情結束後，黑畫面維持的秒數")]
    public float blackScreenDuration = 2f;

    [Header("下一個場景")]
    public string nextSceneName = "SampleScene";


    // ==========================================
    // 初始化
    // ==========================================
    void Awake()
    {
        // 一開始先隱藏結尾黑畫面
        if (blackScreen != null)
        {
            blackScreen.SetActive(false);
        }
    }


    // ==========================================
    // 場景開始時播放劇情
    // ==========================================
    IEnumerator Start()
    {
        Time.timeScale = 1f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 等待 StoryManager 初始化
        yield return null;

        PlayIntroStory();
    }


    // ==========================================
    // 從資料庫讀取開場劇情
    // ==========================================
    void PlayIntroStory()
    {
        if (storyDatabase == null)
        {
            Debug.LogError(
                "[IntroScene] StoryDatabase 尚未指定！"
            );
            return;
        }

        if (storyManager == null)
        {
            Debug.LogError(
                "[IntroScene] StoryManager 尚未指定！"
            );
            return;
        }

        List<StoryEncounterStage> stages =
            storyDatabase.GetStagesForEvent(
                eventTypeKey
            );

        if (stages == null)
        {
            Debug.LogError(
                "[IntroScene] 找不到事件：" + eventTypeKey
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
            Debug.LogError(
                "[IntroScene] 找不到階段：" + stageName
            );
            return;
        }

        if (selectedStage.messages == null ||
            selectedStage.messages.Length == 0)
        {
            Debug.LogError(
                "[IntroScene] 沒有劇情文字！"
            );
            return;
        }

        // 播放劇情
        // 所有劇情結束後呼叫 OnIntroFinished
        storyManager.ShowStories(
            selectedStage.messages,
            selectedStage.voiceClips,
            3,
            OnIntroFinished
        );
    }


    // ==========================================
    // 劇情結束
    // ==========================================
    void OnIntroFinished()
    {
        Debug.Log("[IntroScene] 劇情播放完畢");

        StartCoroutine(BlackScreenThenLoad());
    }


    // ==========================================
    // 黑畫面停留後進入遊戲
    // ==========================================
    IEnumerator BlackScreenThenLoad()
    {
        // 顯示全黑畫面
        if (blackScreen != null)
        {
            blackScreen.SetActive(true);
        }
        else
        {
            Debug.LogWarning(
                "[IntroScene] BlackScreen 尚未指定！"
            );
        }

        // 等待指定秒數
        yield return new WaitForSecondsRealtime(
            Mathf.Max(0f, blackScreenDuration)
        );

        // 檢查場景是否存在
        if (!Application.CanStreamedLevelBeLoaded(
            nextSceneName))
        {
            Debug.LogError(
                "[IntroScene] 找不到場景：" + nextSceneName
            );
            yield break;
        }

        // 載入正式遊戲
        SceneManager.LoadScene(nextSceneName);
    }
}
