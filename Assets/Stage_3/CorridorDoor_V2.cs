using System.Collections;
using UnityEngine;

/// <summary>
/// V2 走廊門控
/// </summary>
[RequireComponent(typeof(Collider))]
public class CorridorDoor_V2 : MonoBehaviour
{
    [Header("門動畫")]
    [SerializeField] private Animator doorAnimator;
    [SerializeField] private string   openTrigger  = "Open";
    [SerializeField] private string   closeTrigger = "Close";
    [SerializeField] private float    autoCloseDelay = 0.5f;

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
    private Transform playerTransform;

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

    void SetReady()
    {
        isReady = true;
        // 若玩家已在範圍內（傳送後停在門旁），補觸發 playerNear
        if (playerTransform != null && !IsOpen)
        {
            float dist = Vector3.Distance(transform.position, playerTransform.position);
            if (dist <= interactRadius)
            {
                playerNear = true;
                if (interactPrompt) interactPrompt.SetActive(true);
                Debug.Log($"[Door:{gameObject.name}] SetReady: player already near, restored playerNear");
            }
        }
    }

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
        if (!isReady || !other.CompareTag("Player")) return;
        playerNear = true;
        playerTransform = other.transform;
        if (autoCloseCoroutine != null)
        {
            StopCoroutine(autoCloseCoroutine);
            autoCloseCoroutine = null;
        }
        if (interactPrompt && !IsOpen) interactPrompt.SetActive(true);
        Debug.Log($"[Door:{gameObject.name}] OnTriggerEnter | playerNear=true");
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerNear = false;
        if (interactPrompt) interactPrompt.SetActive(false);
        Debug.Log($"[Door:{gameObject.name}] OnTriggerExit | IsOpen={IsOpen}");

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

        // 關門後如果玩家還在觸發範圍內，恢復 playerNear
        if (playerTransform != null)
        {
            float dist = Vector3.Distance(transform.position, playerTransform.position);
            if (dist <= interactRadius + 0.5f)
            {
                playerNear = true;
                if (interactPrompt) interactPrompt.SetActive(true);
            }
        }
    }

    /// <summary>系統強制關門（傳送後呼叫）- 不觸發 onClose callback、不播動畫</summary>
    public void ForceClose()
    {
        if (autoCloseCoroutine != null)
        {
            StopCoroutine(autoCloseCoroutine);
            autoCloseCoroutine = null;
        }
        IsOpen = false;
        // 重置 Animator 到 Idle 狀態，不播放關門動畫
        if (doorAnimator)
        {
            doorAnimator.ResetTrigger(openTrigger);
            doorAnimator.ResetTrigger(closeTrigger);
            doorAnimator.Play("Idle", 0, 0f);
        }
        // 不呼叫 onClose — 系統內部重置，不需要通知
    }

    public void ResetDoor()
    {
        ForceClose();
        playerNear = false;
        if (interactPrompt) interactPrompt.SetActive(false);
        isReady = false;
        CancelInvoke(nameof(SetReady));
        Invoke(nameof(SetReady), 1.0f);
        Debug.Log($"[Door:{gameObject.name}] ResetDoor called, isReady=false for 1s");
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = isCorrect
            ? new Color(0.2f, 1f, 0.3f, 0.3f)
            : new Color(1f, 0.3f, 0.2f, 0.3f);
        Gizmos.DrawSphere(transform.position, interactRadius);
    }
}