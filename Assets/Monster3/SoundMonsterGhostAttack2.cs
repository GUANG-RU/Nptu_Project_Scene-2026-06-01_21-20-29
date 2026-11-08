
using UnityEngine;

[RequireComponent(typeof(SoundMonsterAI2))]
public class SoundMonsterGhostAttack2 : MonoBehaviour
{
    [Header("玩家設定")]
    public Camera playerCamera;

    [Tooltip("玩家真正的移動腳本，例如 FirstPersonController")]
    public MonoBehaviour playerMovementScript;

    [Header("攻擊效果")]
    public float effectDuration = 2.5f;
    public float freezeDuration = 2.5f;
    public float effectCooldown = 5f;

    [Header("Debug")]
    public bool showDebugLog = true;

    private SoundMonsterAI2 monsterAI;

    private GhostVisionEffect2 ghostEffect;
    private PlayerFreezeController2 freezeController;

    private SoundMonsterAI2.State previousState;

    void Start()
    {
        monsterAI = GetComponent<SoundMonsterAI2>();

        if (monsterAI == null)
        {
            Debug.LogError(
                "[GHOST ATTACK] 找不到 SoundMonsterAI2！",
                this
            );
            enabled = false;
            return;
        }

        // 玩家攝影機
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (playerCamera != null)
        {
            ghostEffect =
                playerCamera.GetComponent<GhostVisionEffect2>();

            if (ghostEffect == null)
            {
                ghostEffect =
                    playerCamera.gameObject
                        .AddComponent<GhostVisionEffect2>();
            }
        }
        else
        {
            Debug.LogWarning(
                "[GHOST ATTACK] 找不到玩家攝影機。",
                this
            );
        }

        // 使用 SoundMonsterAI2 指定的玩家
        if (monsterAI.player == null)
        {
            Debug.LogError(
                "[GHOST ATTACK] SoundMonsterAI2 沒有指定 Player！",
                this
            );

            enabled = false;
            return;
        }

        GameObject playerObject =
            monsterAI.player.gameObject;

        // 玩家身上統一管理定身
        freezeController =
            playerObject.GetComponent<PlayerFreezeController2>();

        if (freezeController == null)
        {
            freezeController =
                playerObject.AddComponent<PlayerFreezeController2>();
        }

        // 將 Inspector 指定的玩家移動腳本交給管理器
        if (playerMovementScript != null)
        {
            freezeController.playerMovementScript =
                playerMovementScript;
        }

        if (freezeController.playerMovementScript == null)
        {
            Debug.LogError(
                "[GHOST ATTACK] 請指定 Player Movement Script！",
                this
            );
        }

        previousState = monsterAI.currentState;
    }

    void Update()
    {
        if (monsterAI == null)
            return;

        SoundMonsterAI2.State currentState =
            monsterAI.currentState;

        // 只有從非 Attack 變成 Attack 時觸發
        if (currentState == SoundMonsterAI2.State.Attack &&
            previousState != SoundMonsterAI2.State.Attack)
        {
            TriggerGhostAttack();
        }

        previousState = currentState;
    }

    void TriggerGhostAttack()
    {
        if (freezeController == null)
        {
            Debug.LogWarning(
                "[GHOST ATTACK] 找不到 PlayerFreezeController2！"
            );
            return;
        }

        // 管理器決定這次攻擊是否有效
        bool success = freezeController.TryFreeze(
            freezeDuration,
            effectCooldown
        );

        // 定身中或冷卻中：不重複攻擊
        if (!success)
        {
            if (showDebugLog)
            {
                Debug.Log(
                    "[GHOST ATTACK] 攻擊被忽略：定身中或冷卻中。"
                );
            }

            return;
        }

        // 成功攻擊才觸發視覺重影
        if (ghostEffect != null)
        {
            ghostEffect.PlayEffect(effectDuration);
        }

        if (showDebugLog)
        {
            Debug.Log(
                "[GHOST ATTACK] Bite! " +
                $"定身 {freezeDuration:F1} 秒，" +
                $"重影 {effectDuration:F1} 秒，" +
                $"冷卻 {effectCooldown:F1} 秒。"
            );
        }
    }

    [ContextMenu("Test Ghost Attack")]
    public void TestGhostAttack()
    {
        TriggerGhostAttack();
    }
}
