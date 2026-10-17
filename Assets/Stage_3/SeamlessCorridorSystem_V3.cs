using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SeamlessCorridorSystem_V3 : MonoBehaviour
{
    [Header("走廊")]
    [SerializeField] private Transform corridorSpawn;

    [Header("門（依序拖入 5 扇）")]
    [SerializeField] private List<CorridorDoor_V2> doors = new();

    [Header("正確門索引（0~4）")]
    [SerializeField] private int correctDoorIndex = 2;

    [Header("浮動走道")]
    [SerializeField] private Transform      wrongPath;
    [SerializeField] private TurnTrigger_V2 wrongEndTrigger;
    [SerializeField] private Transform      correctPath;
    [SerializeField] private TurnTrigger_V2 correctEndTrigger;

    [Header("待機位置（夠遠，玩家看不到）")]
    [SerializeField] private Transform parkingSpot;

    [Header("走道入口偏移")]
    [SerializeField] private float dockOffset = 0.05f;

    [Header("謎題房間入口")]
    [SerializeField] private Transform puzzleRoomSpawn;

    [Header("玩家")]
    [SerializeField] private Transform     player;
    [SerializeField] private MonoBehaviour playerMovement;
    [SerializeField] private MonoBehaviour playerLook;
    [SerializeField] private Transform     playerCameraTransform;

    [Header("浮動走道延伸方向（門內側朝向）")]
    [SerializeField] private Vector3 pathForward = Vector3.forward;

    // ──────────────────────────────────────────
    public System.Action OnWrongDoor;
    public System.Action OnCorrectDoor;

    private CorridorDoor_V2 activeDoor = null;
    private Transform       activePath = null;
    private bool            isMoving   = false;

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
        Debug.Log($"[System] HandleDoorOpen idx={idx} | isMoving={isMoving} | activeDoor={activeDoor?.name ?? "null"}");
        if (isMoving) return;
        activeDoor = doors[idx];
        activePath = (idx == correctDoorIndex) ? correctPath : wrongPath;
        DockPath(activePath, activeDoor.transform);
        Debug.Log($"[System] Docked path={activePath.name} to door={activeDoor.name}");
    }

    void HandleDoorClose(int idx)
    {
        Debug.Log($"[System] HandleDoorClose idx={idx} | activeDoor={activeDoor?.name ?? "null"} | isMoving={isMoving}");
    }

    // ──────────────────────────────────────────
    void HandleWrongEnd()
    {
        if (isMoving) return;
        OnWrongDoor?.Invoke();
        StartCoroutine(TransportSequence(corridorSpawn));
    }

    void HandleCorrectEnd()
    {
        if (isMoving) return;
        OnCorrectDoor?.Invoke();
        // 正確路：走廊直接固定在這裡，不傳送、不收回
        // correctPath 已經 Docked 到正確門旁，保持不動
        // 只需要關閉其他門、鎖定系統避免重複觸發
        LockCorrectPath();
    }

    /// <summary>踩到正確路終點：鎖定走廊，所有猜門關閉並停用</summary>
    void LockCorrectPath()
    {
        isMoving = true;   // 防止重複觸發

        // 其他猜門全部重置並停用互動
        foreach (var d in doors) d.ResetDoor();

        // correctPath 留在原位不動（已 Docked 到正確門）
        // wrongPath 收回待機
        ParkPath(wrongPath);

        activePath = null;
        activeDoor = null;
        // isMoving 故意不改回 false，讓猜門系統永久鎖定
        // 玩家從此只走正確路進謎題房間

        Debug.Log("[System] LockCorrectPath: 走廊已固定，猜門停用");
    }

    // ──────────────────────────────────────────
    IEnumerator TransportSequence(Transform destination)
    {
        Debug.Log($"[System] TransportSequence START → {destination.name}");
        isMoving = true;
        DisablePlayer();
        yield return null;

        TeleportTo(destination);
        Debug.Log($"[System] TeleportTo done | player={player.position} | yaw={player.eulerAngles.y}");

        ReturnPaths();
        activePath = null;
        activeDoor = null;

        yield return new WaitForSeconds(1f);
        foreach (var d in doors) d.ResetDoor();
        Debug.Log("[System] ResetDoor all done");

        EnablePlayer();
        isMoving = false;
        Debug.Log("[System] TransportSequence END");
    }

    // ──────────────────────────────────────────
    void DockPath(Transform path, Transform door)
    {
        if (!path || !door) return;
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

        var cc = player.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;

        if (target == corridorSpawn && activePath != null)
        {
            // 相對位置：玩家在WP的局部座標 → 套到corridorSpawn
            Vector3 localPos = activePath.InverseTransformPoint(player.position);
            player.position  = corridorSpawn.TransformPoint(localPos);

            // 面向：WP → 主走廊 的方向映射
            float camYaw      = playerCameraTransform ? playerCameraTransform.eulerAngles.y : player.eulerAngles.y;
            float pathYaw     = activePath.eulerAngles.y;
            float spawnYaw    = corridorSpawn.eulerAngles.y;
            float relativeYaw = camYaw - pathYaw;
            float finalYaw    = spawnYaw + relativeYaw;

            Debug.Log($"[System] TeleportTo YAWLOG | camYaw={camYaw:F1} pathYaw={pathYaw:F1} spawnYaw={spawnYaw:F1} relYaw={relativeYaw:F1} finalYaw={finalYaw:F1}");

            player.rotation = Quaternion.Euler(0f, finalYaw, 0f);
            if (playerCameraTransform)
                playerCameraTransform.localRotation = Quaternion.Euler(
                    playerCameraTransform.localEulerAngles.x, 0f, 0f);
        }
        else
        {
            player.position = target.position;
            float yaw = target.eulerAngles.y;
            player.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (playerCameraTransform)
                playerCameraTransform.localRotation = Quaternion.identity;
            Debug.Log($"[System] TeleportTo (direct) | yaw={yaw:F1}");
        }

        if (cc) cc.enabled = true;
    }

    void DisablePlayer()
    {
        if (playerMovement) playerMovement.enabled = false;
        if (playerLook)     playerLook.enabled     = false;
    }

    void EnablePlayer()
    {
        // StandardAssets FirstPersonController：同步 MouseLook 內部的 m_CharacterTargetRot / m_CameraTargetRot
        // 否則下一幀 LookRotation() 會用舊值把傳送後的面向蓋掉
        SyncMouseLook();

        if (playerMovement) playerMovement.enabled = true;
        if (playerLook)     playerLook.enabled     = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    void SyncMouseLook()
    {
        // 支援 FirstPersonController（m_MouseLook）和 RigidbodyFirstPersonController（mouseLook）
        var flags = System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance;

        // 找 playerMovement 或 playerLook 裡的 MouseLook 欄位
        MonoBehaviour target = playerMovement ? playerMovement : playerLook;
        if (!target) return;

        var mouseLookField = target.GetType().GetField("m_MouseLook", flags)
                          ?? target.GetType().GetField("mouseLook",   flags);
        if (mouseLookField == null)
        {
            Debug.LogWarning("[System] SyncMouseLook: MouseLook field not found");
            return;
        }

        object mouseLook = mouseLookField.GetValue(target);
        if (mouseLook == null) return;

        var mlType = mouseLook.GetType();

        // m_CharacterTargetRot = 玩家 body 的目標 rotation（純 Yaw）
        var charRotField = mlType.GetField("m_CharacterTargetRot", flags);
        // m_CameraTargetRot = 相機的目標 rotation（純 Pitch，localRotation）
        var camRotField  = mlType.GetField("m_CameraTargetRot",    flags);

        if (charRotField != null)
        {
            Quaternion newCharRot = Quaternion.Euler(0f, player.eulerAngles.y, 0f);
            charRotField.SetValue(mouseLook, newCharRot);
            Debug.Log($"[System] SyncMouseLook: m_CharacterTargetRot → yaw={player.eulerAngles.y:F1}");
        }

        if (camRotField != null && playerCameraTransform)
        {
            // 保留相機的 pitch（上下角度），只清掉 yaw 讓 body 負責
            Quaternion newCamRot = Quaternion.Euler(playerCameraTransform.localEulerAngles.x, 0f, 0f);
            camRotField.SetValue(mouseLook, newCamRot);
            Debug.Log($"[System] SyncMouseLook: m_CameraTargetRot → pitch={playerCameraTransform.localEulerAngles.x:F1}");
        }

        // 把改動寫回（struct 是 value type，必須 SetValue 回去）
        mouseLookField.SetValue(target, mouseLook);
    }

    void OnDrawGizmosSelected()
    {
        if (corridorSpawn)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(corridorSpawn.position, 0.3f);
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