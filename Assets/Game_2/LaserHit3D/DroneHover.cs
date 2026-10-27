using UnityEngine;

public class DroneHover : MonoBehaviour
{
    [Header("--- 1. 懸浮與動畫設定 ---")]
    [Tooltip("上下浮動幅度 (米)")]
    public float hoverAmplitude = 0.15f;

    [Tooltip("上下浮動頻率/速度")]
    public float hoverSpeed = 2.0f;

    [Tooltip("Y 軸自轉速度 (度/秒)")]
    public float rotateSpeed = 25.0f;

    [Header("--- 2. 動態發光 (Emission) 回饋 ---")]
    [Tooltip("無人機發光部位的 MeshRenderer (若未設定會自動取得)")]
    public Renderer targetRenderer;

    [Tooltip("未被雷射照射時的預設暗色/底色")]
    public Color inactiveColor = new Color(0.1f, 0.1f, 0.1f);

    [Tooltip("雷射命中時的 HDR 發光倍率 (讓 Glow 更亮)")]
    public float emissionIntensity = 2.5f;

    [Tooltip("發光顏色切換過渡速度")]
    public float colorSmoothSpeed = 10.0f;

    private Vector3 initialPosition;
    private Material targetMaterial;
    private Color currentEmissionColor;
    private bool isHitThisFrame = false;

    private void Start()
    {
        // 紀錄初始在地座標
        initialPosition = transform.localPosition;

        // 自動取得 Renderer 並實體化材質 (避免修改到專案原檔)
        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<Renderer>();
        }

        if (targetRenderer != null)
        {
            targetMaterial = targetRenderer.material;
            targetMaterial.EnableKeyword("_EMISSION");
        }

        currentEmissionColor = inactiveColor;
        SetEmissionColor(inactiveColor);
    }

    private void Update()
    {
        // --- A. 正弦波上下懸浮 ---
        float newY = initialPosition.y + Mathf.Sin(Time.time * hoverSpeed) * hoverAmplitude;
        transform.localPosition = new Vector3(initialPosition.x, newY, initialPosition.z);

        // --- B. 緩慢旋轉 ---
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);

        // --- C. 發光漸變邏輯 ---
        if (!isHitThisFrame)
        {
            // 若本幀未收到雷射，發光顏色平滑回歸至未激活底色
            currentEmissionColor = Color.Lerp(currentEmissionColor, inactiveColor, Time.deltaTime * colorSmoothSpeed);
            SetEmissionColor(currentEmissionColor);
        }

        // 每幀重置標記
        isHitThisFrame = false;
    }

    /// <summary>
    /// 當 Photosensor 收到雷射時被呼叫
    /// </summary>
    public void OnLaserHit(Color laserColor)
    {
        isHitThisFrame = true;
        // 計算帶有 HDR 倍率的亮光顏色
        Color targetColor = laserColor * emissionIntensity;
        currentEmissionColor = Color.Lerp(currentEmissionColor, targetColor, Time.deltaTime * colorSmoothSpeed);
        SetEmissionColor(currentEmissionColor);
    }

    private void SetEmissionColor(Color color)
    {
        if (targetMaterial != null)
        {
            // URP / Universal Render Pipeline 或 Standard Shader 相容
            targetMaterial.SetColor("_EmissionColor", color);
        }
    }
}