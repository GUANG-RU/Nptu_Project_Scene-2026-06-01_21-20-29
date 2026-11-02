using UnityEngine;

public class MonsterHearing3 : MonoBehaviour
{
    [Header("聽覺閾值")]
    public float patrolThreshold = 5f;
    public float alertThreshold = 2f;

    // 指向新版的 SoundMonsterAI3
    SoundMonsterAI3 ai;

    bool heardThisFrame;
    float heardIntensity;
    Vector3 heardPosition;

    void Awake()
    {
        // 確保抓取的是同一個物件上的 SoundMonsterAI3
        ai = GetComponent<SoundMonsterAI3>();
    }

    public void ReceiveSound(float intensity, Vector3 sourcePos)
    {
        if (ai == null) return;

        // 依據新版 AI 的 currentState 動態切換聽覺靈敏度
        float threshold = ai.currentState == SoundMonsterAI3.State.Alert
            ? alertThreshold
            : patrolThreshold;

        if (intensity >= threshold)
        {
            heardThisFrame = true;

            // 記錄當前幀最強的聲音來源
            if (intensity > heardIntensity)
            {
                heardIntensity = intensity;
                heardPosition = sourcePos;
            }
        }
    }

    public bool HasHeardSound(out float intensity, out Vector3 pos)
    {
        intensity = heardIntensity;
        pos = heardPosition;

        bool result = heardThisFrame;

        // 重置當前幀的狀態，等待下一幀的聲音輸入
        heardThisFrame = false;
        heardIntensity = 0f;

        return result;
    }
}