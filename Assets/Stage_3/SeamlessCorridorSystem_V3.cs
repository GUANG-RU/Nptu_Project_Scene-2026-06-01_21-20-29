using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 無縫走廊系統 V3（最終版）
///
/// 流程：
///   玩家按 F 開門
///   → 浮動走道瞬間移到門後
///   → 玩家走進去（每扇門體驗完全一樣）
///   → 走到盡頭（TurnTrigger）
///   → 視角背對門廳後瞬間傳送回 SpawnPoint
///   → 門關、走道縮回待機點
///
///   正確門：走到盡頭傳送到謎題房間入口
///   玩家遠離門（不走進去）：門自動關、走道縮回
/// </summary>
public class SeamlessCorridorSystem_V3 : MonoBehaviour
{
    [Header("走廊")]
    [SerializeField] private Transform corridorSpawn;   // 門廳起點，傳送回這裡

    [Header("門（依序拖入 5 扇）")]
    [SerializeField] private List<CorridorDoor_V2> doors = new();

    [Header("正確門索引（0~4）")]
    [SerializeField] private int correctDoorIndex = 2;

    [Header("浮動走道")]
    [SerializeField] private Transform      wrongPath;          // 錯誤走道
    [SerializeField] private TurnTrigger_V2 wrongEndTrigger;    // 錯誤走道盡頭
    [SerializeField] private Transform      correctPath;        // 正確走道
    [SerializeField] private TurnTrigger_V2 correctEndTrigger;  // 正確走道盡頭

    [Header("待機位置（夠遠，玩家看不到）")]
    [SerializeField] private Transform parkingSpot;

    [Header("走道入口偏移（門後多少距離接走道）")]
    [SerializeField] private float dockOffset = 0.05f;

    [Header("謎題房間入口")]
    [SerializeField] private Transform puzzleRoomSpawn;

    [Header("玩家")]
    [SerializeField] private Transform     player;
    [SerializeField] private MonoBehaviour playerMovement;
    [SerializeField] private MonoBehaviour playerLook;
    [SerializeField] private Transform     playerCameraTransform;

    [Header("走廊朝向（玩家面對門廳的方向，預設 +Z）")]
    [SerializeField] private Vector3 corridorForward = Vector3.forward;

    [Header("浮動走道延伸方向（門內側朝向，預設 +Z）")]
    [SerializeField] private Vector3 pathForward = Vector3.forward;

    // ──────────────────────────────────────────
    public System.Action OnWrongDoor;
    public System.Action OnCorrectDoor;

    private CorridorDoor_V2 activeDoor   = null;
    private bool            isMoving     = false;
    private bool            playerInPath = false;  // 玩家是否在浮動走道裡

    // ──────────────────────────────────────────
    void Start()
    {
        ParkPath(wrongPath);
        ParkPath(correctPath);

        for (int i = 0; i < doors.Count; i++)
        {
            int  idx     = i;
            bool correct = (i == correctDoorIndex);
            doors[i].Init(
                isCorrect: correct,
                onOpen:    () => HandleDoorOpen(idx),
                onClose:   () => HandleDoorClose(idx)
            );
        }

        if (wrongEndTrigger)   wrongEndTrigger.OnPlayerEnter   += HandleWrongEnd;
        if (correctEndTrigger) correctEndTrigger.OnPlayerEnter += HandleCorrectEnd;

    }

    void OnDestroy()
    {
        if (wrongEndTrigger)   wrongEndTrigger.OnPlayerEnter   -= HandleWrongEnd;
        if (correctEndTrigger) correctEndTrigger.OnPlayerEnter -= HandleCorrectEnd;
    }

    // ──────────────────────────────────────────
    void HandleDoorOpen(int idx)
    {
        if (activeDoor != null) return;
        activeDoor = doors[idx];

        bool correct = (idx == correctDoorIndex);
        DockPath(correct ? correctPath : wrongPath, activeDoor.transform);
    }

    void HandleDoorClose(int idx)
    {
        if (activeDoor != doors[idx]) return;
        // 延遲收回，讓玩家有時間走進去；若已在傳送序列則不處理
        if (!isMoving) StartCoroutine(DelayedReturnPaths());
        activeDoor = null;
    }

    IEnumerator DelayedReturnPaths()
    {
        yield return new WaitForSeconds(3f);  // 等 3 秒，玩家如果沒進去才收回
        if (!isMoving) ReturnPaths();
    }

    // ──────────────────────────────────────────
    void HandleWrongEnd()
    {
        if (isMoving) return;
        playerInPath = false;
        OnWrongDoor?.Invoke();
        StartCoroutine(TransportSequence(corridorSpawn));
    }

    void HandleCorrectEnd()
    {
        if (isMoving) return;
        playerInPath = false;
        OnCorrectDoor?.Invoke();
        StartCoroutine(TransportSequence(puzzleRoomSpawn));
    }

    // ──────────────────────────────────────────
    IEnumerator TransportSequence(Transform destination)
    {
        isMoving = true;
        DisablePlayer();

        // 等視角背對走廊（已在走道轉彎處）再傳送
        yield return new WaitUntil(IsLookingAway);

        TeleportTo(destination);
        ReturnPaths();
        if (activeDoor != null) { activeDoor.ForceClose(); activeDoor = null; }
        foreach (var d in doors) d.ResetDoor();

        EnablePlayer();
        isMoving = false;
    }

    // ──────────────────────────────────────────
    void DockPath(Transform path, Transform door)
    {
        if (!path || !door) return;
        // 沿著指定的走道方向偏移，接在門內側
        Vector3 dir = pathForward.normalized;
        path.position = door.position + dir * dockOffset;
        path.rotation = Quaternion.LookRotation(dir, Vector3.up);
    }

    void ParkPath(Transform path)
    {
        if (!path || !parkingSpot) return;
        path.position = parkingSpot.position;
        path.rotation = parkingSpot.rotation;
    }

    void ReturnPaths()
    {
        ParkPath(wrongPath);
        ParkPath(correctPath);
    }

    void TeleportTo(Transform target)
    {
        if (!player || !target) return;
        player.position = target.position;
        player.rotation = target.rotation;
    }

    bool IsLookingAway() =>
        !playerCameraTransform ||
        Vector3.Dot(playerCameraTransform.forward.normalized,
                    corridorForward.normalized) < 0f;

    void DisablePlayer()
    {
        if (playerMovement) playerMovement.enabled = false;
        if (playerLook)     playerLook.enabled     = false;
    }

    void EnablePlayer()
    {
        if (playerMovement) playerMovement.enabled = true;
        if (playerLook)     playerLook.enabled     = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    // ──────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        if (corridorSpawn)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(corridorSpawn.position, 0.3f);
            Gizmos.DrawRay(corridorSpawn.position, corridorForward * 3f);
        }
        if (puzzleRoomSpawn)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(puzzleRoomSpawn.position, 0.3f);
        }
        if (parkingSpot)
        {
            Gizmos.color = Color.gray;
            Gizmos.DrawSphere(parkingSpot.position, 0.5f);
        }
    }
}