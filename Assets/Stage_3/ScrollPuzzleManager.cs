using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 捲軸謎題管理器
///
/// 功能：
///   - 持有多道謎題定義（Inspector 設定）
///   - 玩家靠近捲軸按 F 開啟 UI
///   - 隨機抽選謎題
///   - 通關後：播爆炸特效、Disable 牆壁物件、呼叫可選事件
///
/// 需要在同一場景中有一個設置好的 ScrollPuzzleUI（Canvas 底下）
/// </summary>
public class ScrollPuzzleManager : MonoBehaviour
{
    // ── Inspector ─────────────────────────────
    [Header("謎題定義（可設 1~多個，隨機抽一）")]
    [SerializeField] private ScrollPuzzleData[] puzzles;

    [Header("UI")]
    [SerializeField] private ScrollPuzzleUI puzzleUI;

    [Header("捲軸互動")]
    [SerializeField] private float    interactRadius = 2.0f;
    [SerializeField] private KeyCode  interactKey    = KeyCode.F;
    [SerializeField] private GameObject interactPrompt;  // 可選：「按 F 查看捲軸」提示

    [Header("通關後：牆壁")]
    [SerializeField] private GameObject[] wallsToDestroy;   // disable 這些物件
    [SerializeField] private GameObject   explosionVFX;     // Particle System prefab，實例化後播放
    [SerializeField] private Transform[]  explosionPoints;  // 爆炸特效生成位置

    [Header("通關後：可選事件")]
    public UnityEngine.Events.UnityEvent onPuzzleSolved;

    // ── 私有狀態 ──────────────────────────────
    private bool puzzleOpen   = false;
    private bool puzzleSolved = false;
    private bool playerNear   = false;
    private Transform playerTransform;

    // ──────────────────────────────────────────
    void Awake()
    {
        if (interactPrompt) interactPrompt.SetActive(false);

        // 設定 Collider 觸發器（若有）
        var col = GetComponent<SphereCollider>();
        if (col) col.isTrigger = true;
    }

    void Start()
    {
        if (puzzleUI == null)
        {
            Debug.LogError("[ScrollPuzzleManager] puzzleUI 未指定！");
            return;
        }
        puzzleUI.OnPuzzleSolved += HandleSolved;
    }

    void OnDestroy()
    {
        if (puzzleUI) puzzleUI.OnPuzzleSolved -= HandleSolved;
    }

    void Update()
    {
        if (puzzleSolved || puzzleOpen) return;
        if (!playerNear) return;

        if (Input.GetKeyDown(interactKey))
            OpenPuzzle();
    }

    // ── Trigger 偵測玩家靠近 ──────────────────
    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerNear      = true;
        playerTransform = other.transform;
        if (interactPrompt && !puzzleSolved) interactPrompt.SetActive(true);
        Debug.Log("[ScrollPuzzleManager] Player entered interact zone");
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        playerNear = false;
        if (interactPrompt) interactPrompt.SetActive(false);
    }

    // ── 開啟謎題 ──────────────────────────────
    void OpenPuzzle()
    {
        if (puzzles == null || puzzles.Length == 0)
        {
            Debug.LogWarning("[ScrollPuzzleManager] 沒有設定任何謎題！");
            return;
        }

        puzzleOpen = true;
        if (interactPrompt) interactPrompt.SetActive(false);

        // 隨機選一道謎題
        int idx = Random.Range(0, puzzles.Length);
        Debug.Log($"[ScrollPuzzleManager] 開啟謎題 #{idx}");
        puzzleUI.ShowPuzzle(puzzles[idx]);
    }

    // ── 通關 ──────────────────────────────────
    void HandleSolved()
    {
        puzzleSolved = true;
        puzzleOpen   = false;
        Debug.Log("[ScrollPuzzleManager] 謎題通關！");

        StartCoroutine(SolvedSequence());
    }

    IEnumerator SolvedSequence()
    {
        yield return new WaitForSeconds(0.2f);

        // 爆炸特效
        if (explosionVFX && explosionPoints != null)
        {
            foreach (var pt in explosionPoints)
            {
                if (pt)
                {
                    var vfx = Instantiate(explosionVFX, pt.position, pt.rotation);
                    // 自動銷毀（Particle System 播完）
                    var ps = vfx.GetComponent<ParticleSystem>();
                    if (ps)
                    {
                        float dur = ps.main.duration + ps.main.startLifetime.constantMax;
                        Destroy(vfx, dur + 0.5f);
                    }
                    else Destroy(vfx, 3f);
                }
            }
        }

        yield return new WaitForSeconds(0.5f);

        // 隱藏牆壁
        if (wallsToDestroy != null)
            foreach (var w in wallsToDestroy)
                if (w) w.SetActive(false);

        // 呼叫外部事件（例如開啟走廊）
        onPuzzleSolved?.Invoke();

        // 開啟謎題房間出口的門（若 SimpleDoor 掛在此物件子層）
        var door = GetComponentInChildren<SimpleDoor>();
        if (door) door.ForceOpen();
    }

    // ── Gizmo ─────────────────────────────────
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.3f);
        Gizmos.DrawSphere(transform.position, interactRadius);
    }
}
