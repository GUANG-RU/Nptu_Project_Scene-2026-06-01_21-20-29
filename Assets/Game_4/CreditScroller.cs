using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CreditScroller : MonoBehaviour
{
    [Header("UI 連結")]
    public RectTransform contentRect;
    public TMP_Text      creditText;
    public GameObject    creditPanel;   // 可留空，不再 SetActive
    public CanvasGroup   canvasGroup;

    [Header("滾動設定")]
    public float scrollSpeed  = 80f;
    public float startDelay   = 0.5f;
    public float endDelay     = 2.0f;

    [Header("淡入淡出設定")]
    public float fadeInDuration  = 1.5f;
    public float fadeOutDuration = 1.5f;

    [Header("txt 檔案設定")]
    public string fileName = "credits.txt";

    private bool _isScrolling = false;
    private bool Played = false;

    void Start()
    {
        // 用 alpha=0 隱藏，不動 SetActive（避免 Panel 關掉後 Layout 無法計算）
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    public void Play()
    {
        string path = Path.Combine(Application.streamingAssetsPath, fileName);

        if (!File.Exists(path))
        {
            Debug.LogError($"[CreditScroller] 找不到 txt 檔：{path}");
            return;
        }

        creditText.text = File.ReadAllText(path, System.Text.Encoding.UTF8);

        if (canvasGroup != null) canvasGroup.alpha = 0f;

        StartCoroutine(ScrollRoutine());
    }

    private IEnumerator ScrollRoutine()
    {
        // 等待 Layout 計算完畢
        yield return null;
        yield return null;
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        yield return null;

        RectTransform viewportRect = contentRect.parent as RectTransform;
        float viewportHeight = viewportRect != null ? viewportRect.rect.height : Screen.height;
        float contentHeight  = contentRect.rect.height;

        // 從畫面底部外側開始
        contentRect.anchoredPosition = new Vector2(
            contentRect.anchoredPosition.x,
            -viewportHeight
        );

        // 淡入
        yield return StartCoroutine(Fade(0f, 1f, fadeInDuration));
        yield return new WaitForSeconds(startDelay);

        // 滾動
        _isScrolling = true;
        float targetY = contentHeight;

        while (contentRect.anchoredPosition.y < targetY)
        {
            float newY = contentRect.anchoredPosition.y + scrollSpeed * Time.deltaTime;
            contentRect.anchoredPosition = new Vector2(
                contentRect.anchoredPosition.x,
                Mathf.Min(newY, targetY)
            );
            yield return null;
        }

        _isScrolling = false;

        yield return new WaitForSeconds(endDelay);

        // 淡出
        yield return StartCoroutine(Fade(1f, 0f, fadeOutDuration));
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (canvasGroup == null) yield break;

        float elapsed = 0f;
        canvasGroup.alpha = from;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        canvasGroup.alpha = to;
    }

    void Update()
    {
        if (_isScrolling && Input.GetKeyDown(KeyCode.Escape))
        {
            StopAllCoroutines();
            StartCoroutine(SkipToEnd());
        }
    }

    private IEnumerator SkipToEnd()
    {
        _isScrolling = false;
        yield return new WaitForSeconds(endDelay);
        yield return StartCoroutine(Fade(1f, 0f, fadeOutDuration));
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!Played)
        {
            Play();
            Played = true;
        }
    }
}