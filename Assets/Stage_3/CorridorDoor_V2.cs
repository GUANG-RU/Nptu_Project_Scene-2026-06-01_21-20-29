using System.Collections;
using UnityEngine;

/// <summary>
/// V2 走廊門控
///
/// 功能：
///   - 玩家靠近：顯示互動提示
///   - 按 F：門開啟動畫，通知系統對接浮動走道
///   - 玩家遠離：門自動關閉動畫，通知系統收回走道
///   - 提示物件：只有正確門顯示（微小刻痕/符文等）
/// </summary>
[RequireComponent(typeof(Collider))]
public class CorridorDoor_V2 : MonoBehaviour
{
    [Header("門動畫")]
    [SerializeField] private Animator doorAnimator;
    [SerializeField] private string   openTrigger  = "Open";
    [SerializeField] private string   closeTrigger = "Close";
    [SerializeField] private float    autoCloseDelay = 0.5f;  // 玩家離開後幾秒關門

    [Header("提示物件（正確門才顯示）")]
    [SerializeField] private GameObject hintObject;

    [Header("互動提示 UI")]
    [SerializeField] private GameObject interactPrompt;
    [SerializeField] private KeyCode    interactKey    = KeyCode.F;

    [Header("互動碰撞範圍")]
    [SerializeField] private float interactRadius = 2.0f;

    // ──────────────────────────────────────────
    public bool IsOpen { get; private set; } = false;

    private bool isCorrect;
    private System.Action onOpen;
    private System.Action onClose;

    private bool playerNear = false;
    private bool isReady    = false;
    private Coroutine autoCloseCoroutine;

    // ──────────────────────────────────────────
    public void Init(bool isCorrect, System.Action onOpen, System.Action onClose)
    {
        this.isCorrect = isCorrect;
        this.onOpen    = onOpen;
        this.onClose   = onClose;

        if (hintObject)     hintObject.SetActive(isCorrect);
        if (interactPrompt) interactPrompt.SetActive(false);

        var col = GetComponent<Collider>();
        col.isTrigger = true;

        Invoke(nameof(SetReady), 0.3f);
    }

    void SetReady() => isReady = true;

    // ──────────────────────────────────────────
    void Update()
    {
        if (Input.GetKeyDown(interactKey))
            Debug.Log($"[Door:{gameObject.name}] F pressed | playerNear={playerNear} isReady={isReady} IsOpen={IsOpen}");

        if (!playerNear || IsOpen) return;
        if (Input.GetKeyDown(interactKey)) OpenDoor();
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[Door:{gameObject.name}] TriggerEnter tag={other.tag} isReady={isReady}");
        if (!isReady || !other.CompareTag("Player")) return;
        playerNear = true;
        if (autoCloseCoroutine != null)
        {
            StopCoroutine(autoCloseCoroutine);
            autoCloseCoroutine = null;
        }
        if (interactPrompt && !IsOpen) interactPrompt.SetActive(true);
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerNear = false;
        if (interactPrompt) interactPrompt.SetActive(false);

        if (IsOpen)
            autoCloseCoroutine = StartCoroutine(AutoCloseAfterDelay());
    }

    // ──────────────────────────────────────────
    void OpenDoor()
    {
        Debug.Log($"[Door:{gameObject.name}] OpenDoor! isCorrect={isCorrect}");
        IsOpen = true;
        if (interactPrompt) interactPrompt.SetActive(false);
        if (doorAnimator)   doorAnimator.SetTrigger(openTrigger);
        onOpen?.Invoke();
    }

    IEnumerator AutoCloseAfterDelay()
    {
        yield return new WaitForSeconds(autoCloseDelay);
        CloseDoor();
    }

    void CloseDoor()
    {
        if (!IsOpen) return;
        IsOpen = false;
        if (doorAnimator) doorAnimator.SetTrigger(closeTrigger);
        onClose?.Invoke();
        autoCloseCoroutine = null;
    }

    /// <summary>系統強制關門（傳送後呼叫）</summary>
    public void ForceClose()
    {
        if (autoCloseCoroutine != null) StopCoroutine(autoCloseCoroutine);
        IsOpen = false;
        if (doorAnimator) doorAnimator.SetTrigger(closeTrigger);
        autoCloseCoroutine = null;
    }

    public void ResetDoor()
    {
        ForceClose();
        isReady = false;
        Invoke(nameof(SetReady), 0.3f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = isCorrect
            ? new Color(0.2f, 1f, 0.3f, 0.3f)
            : new Color(1f, 0.3f, 0.2f, 0.3f);
        Gizmos.DrawSphere(transform.position, interactRadius);
    }
}