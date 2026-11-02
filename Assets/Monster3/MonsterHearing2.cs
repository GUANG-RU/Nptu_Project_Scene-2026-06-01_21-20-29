using UnityEngine;

public class MonsterHearing2 : MonoBehaviour
{
    [Header("聽覺閾值")]
    public float patrolThreshold = 5f;
    public float alertThreshold = 2f;

    SoundMonsterAI2 ai;

    bool heardThisFrame;
    float heardIntensity;
    Vector3 heardPosition;

    void Awake()
    {
        ai = GetComponent<SoundMonsterAI2>();
    }

    public void ReceiveSound(float intensity, Vector3 sourcePos)
    {
        if (ai == null) return;

        float threshold = GetCurrentThreshold();

        if (intensity >= threshold)
        {
            heardThisFrame = true;

            // 同一幀如果聽到多個聲音，取最強的
            if (intensity > heardIntensity)
            {
                heardIntensity = intensity;
                heardPosition = sourcePos;
            }
        }
    }

    float GetCurrentThreshold()
    {
        // Patrol：比較不敏感
        // Alert / Chase / Attack：比較敏感
        if (ai.currentState == SoundMonsterAI2.State.Alert ||
            ai.currentState == SoundMonsterAI2.State.Chase ||
            ai.currentState == SoundMonsterAI2.State.Attack)
        {
            return alertThreshold;
        }

        return patrolThreshold;
    }

    public bool HasHeardSound(out float intensity, out Vector3 pos)
    {
        intensity = heardIntensity;
        pos = heardPosition;

        bool result = heardThisFrame;

        heardThisFrame = false;
        heardIntensity = 0f;

        return result;
    }
}