using UnityEngine;

/// <summary>
/// 獨立輸入攔截器：當劇情對話播放時，封鎖除了「滑鼠左鍵 (推進對話)」以外的所有按鍵與互動
/// </summary>
public class DialogueInputBlocker : MonoBehaviour
{
    [Header("允許在對話期間使用的按鍵 (預設僅留滑鼠左鍵)")]
    [Tooltip("對話期間唯一允許點擊的按鍵 (通常為 Mouse 0 / 左鍵)")]
    public KeyCode allowedDialogueKey = KeyCode.Mouse0;

    [Header("玩家組件手動指定 (可選，若留空會自動搜尋)")]
    public MonoBehaviour[] scriptsToDisableDuringDialogue;

    private void Update()
    {
        // 若當前沒有播放對話，不執行任何封鎖
        if (!AdaptiveStoryTrigger.IsDialoguePlaying) return;

        // 🚨 關鍵邏輯：在對話期間，如果玩家按下的不是允許的按鍵 (滑鼠左鍵)，直接清空/無視該幀的輸入
        BlockUnauthorizedInputs();
    }

    private void BlockUnauthorizedInputs()
    {
        // 1. 如果玩家按下 E, F, Space, WASD 或右鍵等非允許按鍵，可以在這裡統一強制吃掉/重置 Input
        // (若有使用舊版 Input.ResetInputAxes 可重置軸向輸入)
        if (Input.anyKeyDown && !Input.GetKeyDown(allowedDialogueKey))
        {
            Input.ResetInputAxes();
        }
    }

    /// <summary>
    /// 由 AdaptiveStoryTrigger 呼叫：切換控制組件開關
    /// </summary>
    public void SetPlayerControlsLock(bool isLocked)
    {
        // 若 Inspector 有手動指定要停用的腳本 (例如 FlashlightController, PlayerMovement, RotateMirror)
        if (scriptsToDisableDuringDialogue != null)
        {
            foreach (var script in scriptsToDisableDuringDialogue)
            {
                if (script != null)
                {
                    script.enabled = !isLocked;
                }
            }
        }
    }
}