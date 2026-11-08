
using UnityEngine;

public class PlayerFreezeController2 : MonoBehaviour
{
    [Header("玩家移動控制")]
    [Tooltip("指定 FirstPersonController 或實際移動腳本")]
    public MonoBehaviour playerMovementScript;

    [Header("定身設定")]
    public float defaultFreezeDuration = 2.5f;
    public float defaultCooldown = 5f;

    [Header("狀態觀察")]
    public bool isFrozen = false;

    [SerializeField]
    private float remainingTime = 0f;

    [SerializeField]
    private float cooldownRemaining = 0f;

    private float freezeEndTime;
    private float nextAllowedFreezeTime;

    private bool movementWasEnabled;
    private bool waitingForMirrorRelease;

    void Update()
    {
        float now = Time.unscaledTime;

        cooldownRemaining = Mathf.Max(
            0f,
            nextAllowedFreezeTime - now
        );

        if (isFrozen)
        {
            remainingTime = Mathf.Max(
                0f,
                freezeEndTime - now
            );

            if (now >= freezeEndTime)
            {
                ReleaseFreeze();
            }
        }

        // 如果定身已結束但玩家正在操作鏡子，
        // 等鏡子結束後才恢復移動。
        if (waitingForMirrorRelease)
        {
            if (!IsUsingMirror())
            {
                waitingForMirrorRelease = false;
                RestoreMovement();
            }
        }
    }

    void LateUpdate()
    {
        // 定身期間維持移動腳本關閉。
        // 防止其他腳本提前將它開啟。
        if (isFrozen && playerMovementScript != null)
        {
            if (playerMovementScript.enabled)
            {
                playerMovementScript.enabled = false;
            }
        }
    }

    public bool TryFreeze(float duration, float cooldown)
    {
        if (playerMovementScript == null)
        {
            Debug.LogError(
                "[FREEZE] 尚未指定玩家移動腳本！",
                this
            );
            return false;
        }

        if (playerMovementScript == this ||
            playerMovementScript is PlayerSoundEmitter2)
        {
            Debug.LogError(
                "[FREEZE] Player Movement Script 指定錯誤！",
                this
            );
            return false;
        }

        if (duration <= 0f)
            return false;

        float now = Time.unscaledTime;

        // 已定身時不允許延長時間
        if (isFrozen)
        {
            Debug.Log(
                "[FREEZE] 玩家已定身，忽略重複攻擊。"
            );
            return false;
        }

        // 效果冷卻中
        if (now < nextAllowedFreezeTime)
        {
            Debug.Log(
                "[FREEZE] 攻擊效果冷卻中。"
            );
            return false;
        }

        // 操作鏡子期間，不覆蓋鏡子的控制狀態
        if (IsUsingMirror())
        {
            Debug.Log(
                "[FREEZE] 玩家正在操作鏡子，忽略定身。"
            );
            return false;
        }

        // 避免已被其他系統鎖住時再記錄為 false，
        // 導致之後無法恢復。
        if (!playerMovementScript.enabled)
        {
            Debug.LogWarning(
                "[FREEZE] 玩家移動腳本已經關閉。" +
                "請確認是否有其他系統正在控制玩家。",
                this
            );
            return false;
        }

        movementWasEnabled = playerMovementScript.enabled;

        isFrozen = true;
        waitingForMirrorRelease = false;

        freezeEndTime = now + duration;

        nextAllowedFreezeTime =
            now + Mathf.Max(duration, cooldown);

        remainingTime = duration;

        playerMovementScript.enabled = false;

        Debug.Log(
            $"[FREEZE] 玩家定身開始：{duration:F1} 秒"
        );

        return true;
    }

    public bool TryFreeze()
    {
        return TryFreeze(
            defaultFreezeDuration,
            defaultCooldown
        );
    }

    void ReleaseFreeze()
    {
        if (!isFrozen)
            return;

        isFrozen = false;
        remainingTime = 0f;

        // 鏡子目前正在控制玩家，
        // 不能直接把移動腳本打開。
        if (IsUsingMirror())
        {
            waitingForMirrorRelease = true;

            Debug.Log(
                "[FREEZE] 定身時間結束，等待鏡子操作結束。"
            );
            return;
        }

        RestoreMovement();
    }

    void RestoreMovement()
    {
        if (playerMovementScript == null)
            return;

        // 只有本次定身開始前原本啟用，
        // 才恢復成啟用狀態。
        playerMovementScript.enabled = movementWasEnabled;

        Debug.Log(
            "[FREEZE] 定身結束，玩家移動已恢復。"
        );
    }

    bool IsUsingMirror()
    {
        RotateMirror[] mirrors =
            FindObjectsByType<RotateMirror>(
                FindObjectsSortMode.None
            );

        foreach (RotateMirror mirror in mirrors)
        {
            if (mirror != null &&
                mirror.isActiveAndEnabled &&
                mirror.isControlling)
            {
                return true;
            }
        }

        return false;
    }

    [ContextMenu("Force Restore Player")]
    public void ForceRestorePlayer()
    {
        isFrozen = false;
        remainingTime = 0f;

        if (IsUsingMirror())
        {
            waitingForMirrorRelease = true;
        }
        else
        {
            waitingForMirrorRelease = false;
            movementWasEnabled = true;
            RestoreMovement();
        }

        Debug.Log("[FREEZE] 手動解除定身。");
    }

    [ContextMenu("Test Freeze")]
    public void TestFreeze()
    {
        TryFreeze();
    }

    void OnDisable()
    {
        if (isFrozen || waitingForMirrorRelease)
        {
            isFrozen = false;
            waitingForMirrorRelease = false;

            if (playerMovementScript != null &&
                !IsUsingMirror())
            {
                RestoreMovement();
            }
        }
    }

    void OnDestroy()
    {
        if (playerMovementScript != null &&
            !IsUsingMirror() &&
            (isFrozen || waitingForMirrorRelease))
        {
            RestoreMovement();
        }
    }
}
