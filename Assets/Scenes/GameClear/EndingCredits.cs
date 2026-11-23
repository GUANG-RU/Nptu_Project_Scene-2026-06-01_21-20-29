
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class EndingCredits : MonoBehaviour
{
    [Header("UI 設定")]
    public GameObject creditsPanel;
    public TMP_Text creditsText;

    [Header("片尾播放設定")]

    [Tooltip("進入場景後是否自動播放片尾")]
    public bool playOnStart = true;

    [Tooltip("文字每秒向上移動的距離")]
    public float scrollSpeed = 70f;

    [Tooltip("開始捲動前等待時間")]
    public float startDelay = 1f;

    [Tooltip("文字完全離開畫面後等待時間")]
    public float endDelay = 2f;

    [Tooltip("文字與畫面左右邊界的距離")]
    public float horizontalPadding = 100f;

    [Tooltip("文字進入及離開畫面時的額外距離")]
    public float verticalPadding = 40f;

    [Header("跳過設定")]
    public bool allowSkip = true;
    public KeyCode skipKey = KeyCode.Escape;

    [Header("場景切換設定")]
    public bool returnToMainMenu = true;
    public string mainMenuSceneName = "StartMenu";

    [Header("遊戲暫停設定")]
    public bool pauseGameDuringCredits = true;

    [Header("製作人員名單")]
    [TextArea(15, 30)]
    public string creditsContent =
@"<size=72><b>LIGHTWAVE RESEARCH INSTITUTE</b></size>

<size=36>A GAME BY</size>
[Team Name]



<size=36>GAME DIRECTOR</size>
[Member Name]



<size=36>GAME PROGRAMMING</size>
[Member Name]



<size=36>MONSTER AI PROGRAMMING</size>
[Member Name]



<size=36>LEVEL DESIGN</size>
[Member Name]



<size=36>ENVIRONMENT & LIGHTING</size>
[Member Name]



<size=36>STORY & NARRATIVE</size>
[Member Name]



<size=36>UI / UX DESIGN</size>
[Member Name]



<size=36>SOUND DESIGN</size>
[Member Name]



<size=36>TESTING & DEBUGGING</size>
[Member Name]



<size=36>SPECIAL THANKS</size>
Our Teachers
Our Friends
Everyone Who Supported Us



<size=55><b>THANK YOU FOR PLAYING</b></size>";

    private RectTransform panelRect;
    private RectTransform textRect;

    private Coroutine creditsCoroutine;

    private bool isPlaying = false;
    private bool hasPausedGame = false;

    private float previousTimeScale = 1f;

    void Awake()
    {
        if (creditsPanel != null)
        {
            creditsPanel.SetActive(false);
        }
    }

    void Start()
    {
        if (playOnStart)
        {
            ShowCredits();
        }
    }

    void Update()
    {
        if (!isPlaying)
        {
            return;
        }

        if (allowSkip && Input.GetKeyDown(skipKey))
        {
            FinishCredits();
        }
    }

    // ==================================
    // 開始播放片尾
    // ==================================

    public void ShowCredits()
    {
        if (isPlaying)
        {
            return;
        }

        if (creditsPanel == null ||
            creditsText == null)
        {
            Debug.LogError(
                "[EndingCredits] UI 尚未指定！"
            );
            return;
        }

        creditsPanel.SetActive(true);

        panelRect =
            creditsPanel.GetComponent<RectTransform>();

        textRect =
            creditsText.rectTransform;

        if (panelRect == null || textRect == null)
        {
            Debug.LogError(
                "[EndingCredits] 找不到 RectTransform！"
            );
            return;
        }

        // 暫停背景遊戲，片尾仍正常播放
        if (pauseGameDuringCredits)
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            hasPausedGame = true;
        }

        isPlaying = true;

        // 顯示製作名單
        creditsText.text = creditsContent;

        creditsText.alignment =
            TextAlignmentOptions.Center;

        creditsText.color = Color.white;

        creditsText.richText = true;

        creditsText.overflowMode =
            TextOverflowModes.Overflow;

        // 固定文字的中心錨點
        textRect.anchorMin =
            new Vector2(0.5f, 0.5f);

        textRect.anchorMax =
            new Vector2(0.5f, 0.5f);

        textRect.pivot =
            new Vector2(0.5f, 0.5f);

        Canvas.ForceUpdateCanvases();

        float panelWidth = panelRect.rect.width;

        float textWidth = Mathf.Max(
            100f,
            panelWidth - horizontalPadding * 2f
        );

        textRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            textWidth
        );

        // 根據名單長度自動計算文字高度
        float textHeight =
            creditsText.GetPreferredValues(
                creditsContent,
                textWidth,
                Mathf.Infinity
            ).y;

        textHeight = Mathf.Max(1f, textHeight);

        textRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            textHeight + 20f
        );

        creditsText.ForceMeshUpdate();

        creditsCoroutine =
            StartCoroutine(PlayCreditsRoutine());
    }

    // ==================================
    // 電影片尾捲動
    // ==================================

    IEnumerator PlayCreditsRoutine()
    {
        float panelHeight = panelRect.rect.height;
        float textHeight = textRect.rect.height;

        // 從畫面下方開始
        float startY =
            -(panelHeight + textHeight) * 0.5f
            - verticalPadding;

        // 文字完全離開畫面上方
        float endY =
            (panelHeight + textHeight) * 0.5f
            + verticalPadding;

        textRect.anchoredPosition =
            new Vector2(0f, startY);

        if (startDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(
                startDelay
            );
        }

        float currentY = startY;

        while (currentY < endY)
        {
            currentY +=
                Mathf.Max(1f, scrollSpeed) *
                Time.unscaledDeltaTime;

            currentY = Mathf.Min(currentY, endY);

            textRect.anchoredPosition =
                new Vector2(0f, currentY);

            yield return null;
        }

        if (endDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(
                endDelay
            );
        }

        FinishCredits();
    }

    // ==================================
    // 結束片尾
    // ==================================

    public void FinishCredits()
    {
        if (!isPlaying)
        {
            return;
        }

        isPlaying = false;

        if (creditsCoroutine != null)
        {
            StopCoroutine(creditsCoroutine);
            creditsCoroutine = null;
        }

        RestoreGameTime();

        if (returnToMainMenu)
        {
            if (Application.CanStreamedLevelBeLoaded(
                mainMenuSceneName))
            {
                // 確保主選單恢復正常時間
                Time.timeScale = 1f;

                Cursor.lockState =
                    CursorLockMode.None;

                Cursor.visible = true;

                SceneManager.LoadScene(
                    mainMenuSceneName
                );
            }
            else
            {
                Debug.LogError(
                    "[EndingCredits] 找不到場景：" +
                    mainMenuSceneName
                );
            }
        }
        else
        {
            // 不切換場景，停留在黑畫面
            creditsText.text = "";
        }
    }

    // ==================================
    // 恢復遊戲時間
    // ==================================

    void RestoreGameTime()
    {
        if (hasPausedGame)
        {
            Time.timeScale = previousTimeScale;
            hasPausedGame = false;
        }
    }

    void OnDisable()
    {
        RestoreGameTime();
    }
}
