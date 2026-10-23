using UnityEngine;
using UnityEngine.Events;

public class Photosensor : MonoBehaviour
{
    [Header("目標解謎顏色")]
    public Color requiredColor = Color.yellow; // 在 Inspector 中設定目標顏色 (例如黃色/紫色)

    [Header("顏色判定容差")]
    [Range(0.01f, 0.3f)]
    public float colorTolerance = 0.1f;

    [Header("當前狀態")]
    public bool isActivated = false;

    [Header("觸發事件 (可選)")]
    public UnityEvent OnActivated;
    public UnityEvent OnDeactivated;

    private Color accumulatedColor = Color.black;
    private bool receivedHitThisFrame = false;

    /// <summary>
    /// 當雷射打中此感應器時，由 Raycast 邏輯每幀呼叫一次
    /// </summary>
    public void ReceiveLaser(Color laserColor)
    {
        accumulatedColor += laserColor;
        receivedHitThisFrame = true;
    }

    private void LateUpdate()
    {
        if (receivedHitThisFrame)
        {
            CheckColorMatch();
        }
        else
        {
            // 本幀沒有任何雷射打中，重置狀態
            accumulatedColor = Color.black;
            SetActivation(false);
        }

        // 重置下一幀的累加器
        receivedHitThisFrame = false;
        accumulatedColor = Color.black;
    }

    private void CheckColorMatch()
    {
        // 將 RGB 數值限制在 0~1 之間 (防止重複打上的光線數值破表)
        Color normalizedColor = new Color(
            Mathf.Clamp01(accumulatedColor.r),
            Mathf.Clamp01(accumulatedColor.g),
            Mathf.Clamp01(accumulatedColor.b),
            1f
        );

        // 比對 RGB 各通道的相差值是否在誤差範圍內
        bool rMatch = Mathf.Abs(normalizedColor.r - requiredColor.r) <= colorTolerance;
        bool gMatch = Mathf.Abs(normalizedColor.g - requiredColor.g) <= colorTolerance;
        bool bMatch = Mathf.Abs(normalizedColor.b - requiredColor.b) <= colorTolerance;

        bool matched = rMatch && gMatch && bMatch;
        SetActivation(matched);
    }

    private void SetActivation(bool state)
    {
        if (isActivated != state)
        {
            isActivated = state;
            
            if (isActivated)
            {
                Debug.Log($"[Photosensor] {gameObject.name} 正確接收混色！狀態：亮起");
                OnActivated?.Invoke();
            }
            else
            {
                Debug.Log($"[Photosensor] {gameObject.name} 顏色不符或光束中斷。狀態：熄滅");
                OnDeactivated?.Invoke();
            }
        }
    }
}