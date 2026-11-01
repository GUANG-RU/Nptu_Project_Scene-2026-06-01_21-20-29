using UnityEngine;
using TMPro;

public class SensorColorDisplay : MonoBehaviour
{
    [Header("--- 參考組件 ---")]
    [Tooltip("對應的 Photosensor (若留空會自動抓取同物件/父物件)")]
    public Photosensor photosensor;

    [Tooltip("用於顯示文字的 TextMeshPro 組件 (若留空會自動抓取)")]
    public TMP_Text colorText;

    [Header("--- 顯示位置與面朝角度 ---")]
    [Tooltip("文字相對於無人機的頭頂偏移高度")]
    public Vector3 textOffset = new Vector3(0, 1.2f, 0);

    [Tooltip("勾選後文字會自動轉向玩家主攝影機 (Billboard 效果)")]
    public bool faceCamera = true;

    [Header("--- 狀態提示字樣 (分行顯示) ---")]
    public string inactivePrefix = "[ NEED ]";
    public string activePrefix = "[ UNLOCKED ]";

    private Camera mainCamera;

    private void Start()
    {
        if (photosensor == null)
            photosensor = GetComponentInParent<Photosensor>();

        if (colorText == null)
            colorText = GetComponent<TMP_Text>();

        mainCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (photosensor == null || colorText == null) return;

        // 1. 將文字保持在無人機頭頂固定高度
        transform.position = photosensor.transform.position + textOffset;

        // 2. 轉向玩家攝影機
        if (faceCamera && mainCamera != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - mainCamera.transform.position);
        }

        // 3. 讀取顏色並透過 \n 進行雙行排版
        Color reqColor = photosensor.requiredColor;
        string colorName = GetColorName(reqColor);

        if (photosensor.isActivated)
        {
            colorText.text = $"{activePrefix}\n{colorName}";
            colorText.color = Color.green; // 激活時顯示高亮綠字
        }
        else
        {
            colorText.text = $"{inactivePrefix}\n{colorName}";
            colorText.color = GetReadableColor(reqColor); // 文字顏色同步為目標雷射顏色
        }
    }

    /// <summary>
    /// 將 RGB 數值轉換為英文顏色字串
    /// </summary>
    private string GetColorName(Color c)
    {
        float r = c.r, g = c.g, b = c.b;

        if (r > 0.7f && g < 0.3f && b < 0.3f) return "RED";
        if (r < 0.3f && g > 0.7f && b < 0.3f) return "GREEN";
        if (r < 0.3f && g < 0.3f && b > 0.7f) return "BLUE";
        if (r > 0.7f && g > 0.7f && b < 0.3f) return "YELLOW";
        if (r > 0.7f && g < 0.3f && b > 0.7f) return "PURPLE";
        if (r < 0.3f && g > 0.7f && b > 0.7f) return "CYAN";
        if (r > 0.7f && g > 0.7f && b > 0.7f) return "WHITE";

        return $"RGB({r:F1},{g:F1},{b:F1})";
    }

    private Color GetReadableColor(Color c)
    {
        c.a = 1.0f;
        if (c.r + c.g + c.b < 0.4f)
        {
            c += new Color(0.3f, 0.3f, 0.3f);
        }
        return c;
    }
}