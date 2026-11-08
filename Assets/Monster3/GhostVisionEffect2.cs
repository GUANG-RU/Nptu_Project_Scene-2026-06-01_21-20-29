
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Camera))]
public class GhostVisionEffect2 : MonoBehaviour
{
    [Header("重影設定")]
    public float effectDuration = 2.5f;

    [Range(0f, 1f)]
    public float ghostAlpha = 0.55f;

    [Range(0f, 50f)]
    public float ghostOffset = 22f;

    [Header("重影晃動")]
    public bool enableGhostMovement = true;
    public float movementAmount = 5f;
    public float movementSpeed = 8f;

    [Header("淡出設定")]
    public float fadeOutTime = 0.8f;

    [Header("畫質設定")]
    [Range(0.25f, 1f)]
    public float resolutionScale = 0.75f;

    private Camera mainCam;
    private Camera ghostCam;

    private RenderTexture ghostTexture;

    private Canvas ghostCanvas;
    private RawImage ghostImage1;
    private RawImage ghostImage2;

    private float remainingTime;
    private bool isActive = false;

    void Awake()
    {
        mainCam = GetComponent<Camera>();

        CreateGhostCamera();
        CreateGhostUI();
    }

    void CreateGhostCamera()
    {
        GameObject camObj =
            new GameObject("GhostCamera2");

        camObj.transform.SetParent(
            transform, false
        );

        ghostCam = camObj.AddComponent<Camera>();

        ghostCam.CopyFrom(mainCam);

        ghostCam.transform.localPosition = Vector3.zero;
        ghostCam.transform.localRotation = Quaternion.identity;

        AudioListener listener =
            camObj.GetComponent<AudioListener>();

        if (listener != null)
        {
            Destroy(listener);
        }

        CreateRenderTexture();

        ghostCam.enabled = false;
    }

    void CreateRenderTexture()
    {
        int width = Mathf.Max(
            1,
            Mathf.RoundToInt(
                Screen.width * resolutionScale
            )
        );

        int height = Mathf.Max(
            1,
            Mathf.RoundToInt(
                Screen.height * resolutionScale
            )
        );

        ghostTexture = new RenderTexture(
            width, height, 16
        );

        ghostTexture.name = "GhostTexture2";
        ghostTexture.Create();

        ghostCam.targetTexture = ghostTexture;
    }

    void CreateGhostUI()
    {
        GameObject canvasObj =
            new GameObject("GhostVisionCanvas2");

        ghostCanvas =
            canvasObj.AddComponent<Canvas>();

        ghostCanvas.renderMode =
            RenderMode.ScreenSpaceOverlay;

        ghostCanvas.sortingOrder = 500;

        CreateGhostImage(
            "GhostImageLeft2",
            out ghostImage1
        );

        CreateGhostImage(
            "GhostImageRight2",
            out ghostImage2
        );

        ghostCanvas.enabled = false;
    }

    void CreateGhostImage(
        string imageName,
        out RawImage image
    )
    {
        GameObject obj = new GameObject(imageName);

        obj.transform.SetParent(
            ghostCanvas.transform, false
        );

        image = obj.AddComponent<RawImage>();

        image.texture = ghostTexture;
        image.raycastTarget = false;

        RectTransform rect =
            image.rectTransform;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;

        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        image.color = Color.clear;
    }

    public void PlayEffect()
    {
        PlayEffect(effectDuration);
    }

    public void PlayEffect(float duration)
    {
        if (duration <= 0f)
            return;

        if (ghostCam == null ||
            ghostCanvas == null)
            return;

        remainingTime = duration;
        isActive = true;

        ghostCam.enabled = true;
        ghostCanvas.enabled = true;

        Debug.Log("[GHOST] Strong double vision started");
    }

    [ContextMenu("Test Double Vision")]
    public void TestEffect()
    {
        PlayEffect();
    }

    void LateUpdate()
    {
        if (!isActive)
            return;

        remainingTime -= Time.deltaTime;

        if (remainingTime <= 0f)
        {
            StopEffect();
            return;
        }

        // 配合主攝影機的 FOV
        if (mainCam != null && ghostCam != null)
        {
            ghostCam.fieldOfView = mainCam.fieldOfView;
        }

        float alphaMultiplier = 1f;

        if (fadeOutTime > 0f &&
            remainingTime < fadeOutTime)
        {
            alphaMultiplier =
                Mathf.Clamp01(remainingTime / fadeOutTime);
        }

        float alpha =
            ghostAlpha * alphaMultiplier;

        float offset =
            ghostOffset * alphaMultiplier;

        float movementX = 0f;
        float movementY = 0f;

        if (enableGhostMovement)
        {
            movementX =
                Mathf.Sin(Time.time * movementSpeed) *
                movementAmount;

            movementY =
                Mathf.Cos(Time.time * movementSpeed * 0.7f) *
                movementAmount;
        }

        movementX *= alphaMultiplier;
        movementY *= alphaMultiplier;

        // 第一層：偏紅的重影
        ghostImage1.color = new Color(
            1f,
            0.6f,
            0.6f,
            alpha
        );

        ghostImage1.rectTransform.anchoredPosition =
            new Vector2(
                -offset + movementX,
                movementY
            );

        // 第二層：偏藍的重影
        ghostImage2.color = new Color(
            0.5f,
            0.75f,
            1f,
            alpha * 0.8f
        );

        ghostImage2.rectTransform.anchoredPosition =
            new Vector2(
                offset - movementX,
                -movementY
            );
    }

    public void StopEffect()
    {
        if (!isActive)
            return;

        isActive = false;
        remainingTime = 0f;

        if (ghostCam != null)
            ghostCam.enabled = false;

        if (ghostCanvas != null)
            ghostCanvas.enabled = false;

        Debug.Log("[GHOST] Double vision ended");
    }

    void OnDisable()
    {
        StopEffect();
    }

    void OnDestroy()
    {
        if (ghostCam != null)
        {
            ghostCam.targetTexture = null;
            Destroy(ghostCam.gameObject);
        }

        if (ghostCanvas != null)
        {
            Destroy(ghostCanvas.gameObject);
        }

        if (ghostTexture != null)
        {
            ghostTexture.Release();
            Destroy(ghostTexture);
        }
    }
}
