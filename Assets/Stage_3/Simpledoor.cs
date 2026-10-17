using System.Collections;
using UnityEngine;

/// <summary>
/// 簡單門控 — 不需要綁 Manager
/// 按 F 開門，玩家離開後自動關門
/// 可選：單向（只能從某一側開）、開後鎖定不再關
/// </summary>
[RequireComponent(typeof(Collider))]
public class SimpleDoor : MonoBehaviour
{
    [Header("門動畫")]
    [SerializeField] private Animator doorAnimator;
    [SerializeField] private string   openTrigger    = "Open";
    [SerializeField] private string   closeTrigger   = "Close";
    [SerializeField] private float    autoCloseDelay = 1.5f;

    [Header("互動提示 UI")]
    [SerializeField] private GameObject interactPrompt;
    [SerializeField] private KeyCode    interactKey = KeyCode.F;

    [Header("選項")]
    [Tooltip("開門後不自動關（例如正確路→解謎房間，進去就不用關）")]
    [SerializeField] private bool stayOpenAfterEnter = false;

    [Tooltip("單向門：只有玩家從指定方向靠近才能開（留空 = 雙向都可以開）")]
    [SerializeField] private Transform allowedSide;   // 拖入門框附近的空物件，代表「允許開門的那一側」
    [SerializeField] private float     sideCheckDist  = 3f;

    // ──────────────────────────────────────────
    public bool IsOpen { get; private set; } = false;

    private bool      playerNear = false;
    private Coroutine autoCloseCoroutine;
    private Transform playerTransform;

    // ──────────────────────────────────────────
    void Awake()
    {
        // 專門找 BoxCollider，避免抓到門板的 MeshCollider
        var box = GetComponent<BoxCollider>();
        if (box) box.isTrigger = true;
        if (interactPrompt) interactPrompt.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(interactKey))
            Debug.Log($"[SimpleDoor:{gameObject.name}] F pressed | playerNear={playerNear} IsOpen={IsOpen}");

        if (!playerNear || IsOpen) return;
        if (Input.GetKeyDown(interactKey)) TryOpen();
    }

    void TryOpen()
    {
        if (allowedSide != null && playerTransform != null)
        {
            Vector3 toPlayer   = playerTransform.position - transform.position;
            Vector3 toAllowed  = allowedSide.position     - transform.position;
            if (Vector3.Dot(toPlayer, toAllowed) < 0f)
            {
                Debug.Log($"[SimpleDoor:{gameObject.name}] TryOpen BLOCKED by allowedSide");
                return;
            }
        }
        OpenDoor();
    }

    void OpenDoor()
    {
        IsOpen = true;
        if (interactPrompt) interactPrompt.SetActive(false);
        if (doorAnimator)
        {
            doorAnimator.ResetTrigger(closeTrigger);
            doorAnimator.SetTrigger(openTrigger);
        }
        Debug.Log($"[SimpleDoor:{gameObject.name}] OpenDoor | stayOpen={stayOpenAfterEnter}");
    }

    void CloseDoor()
    {
        if (!IsOpen) return;
        IsOpen = false;
        if (doorAnimator)
        {
            doorAnimator.ResetTrigger(openTrigger);
            doorAnimator.SetTrigger(closeTrigger);
        }
        autoCloseCoroutine = null;
        Debug.Log($"[SimpleDoor:{gameObject.name}] CloseDoor");
    }

    // ──────────────────────────────────────────
    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[SimpleDoor:{gameObject.name}] OnTriggerEnter | tag={other.tag} | isReady=always");
        if (!other.CompareTag("Player")) return;
        playerTransform = other.transform;
        playerNear = true;

        if (autoCloseCoroutine != null)
        {
            StopCoroutine(autoCloseCoroutine);
            autoCloseCoroutine = null;
        }

        if (interactPrompt && !IsOpen) interactPrompt.SetActive(true);
        Debug.Log($"[SimpleDoor:{gameObject.name}] playerNear=true");
    }

    void OnTriggerExit(Collider other)
    {
        Debug.Log($"[SimpleDoor:{gameObject.name}] OnTriggerExit | tag={other.tag} | IsOpen={IsOpen} | stayOpen={stayOpenAfterEnter}");
        if (!other.CompareTag("Player")) return;
        playerNear = false;
        if (interactPrompt) interactPrompt.SetActive(false);

        if (IsOpen && !stayOpenAfterEnter)
            autoCloseCoroutine = StartCoroutine(AutoCloseAfterDelay());
    }

    IEnumerator AutoCloseAfterDelay()
    {
        yield return new WaitForSeconds(autoCloseDelay);
        CloseDoor();
    }

    // 外部呼叫（例如謎題完成後強制開門）
    public void ForceOpen()  => OpenDoor();
    public void ForceClose() => CloseDoor();
}