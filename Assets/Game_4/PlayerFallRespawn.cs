using UnityEngine;

public class PlayerFallRespawn : MonoBehaviour
{
    [Header("掉落高度判定設定")]
    [Tooltip("當玩家的 Y 軸座標低於此數值時，自動傳送回初始位置")]
    public float fallThresholdY = -15f;

    [Tooltip("傳送回去時是否一併還原初始面向角度")]
    public bool resetRotation = true;

    private Vector3 m_InitialPosition;
    private Quaternion m_InitialRotation;
    private CharacterController m_CharacterController;
    private Rigidbody m_Rigidbody;

    private void Start()
    {
        // 遊戲開始時自動記錄初始座標與面向
        m_InitialPosition = transform.position;
        m_InitialRotation = transform.rotation;

        m_CharacterController = GetComponent<CharacterController>();
        m_Rigidbody = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        // 當玩家 Y 軸高度低於設定門檻時觸發傳送
        if (transform.position.y < fallThresholdY)
        {
            RespawnToInitialPosition();
        }
    }

    /// <summary>
    /// 將玩家安全傳送回初始座標
    /// </summary>
    public void RespawnToInitialPosition()
    {
        // 關鍵：FPSController 使用 CharacterController 移動，直接修改 position 會被組件覆蓋，必須先暫時關閉
        bool wasCCEnabled = false;
        if (m_CharacterController != null)
        {
            wasCCEnabled = m_CharacterController.enabled;
            m_CharacterController.enabled = false;
        }

        // 還原座標與旋轉角度
        transform.position = m_InitialPosition;
        if (resetRotation)
        {
            transform.rotation = m_InitialRotation;
        }

        // 強制同步物理引擎位置，避免穿牆或判定殘留
        Physics.SyncTransforms();

        // 若身上有 Rigidbody，一併歸零下墜速度
        if (m_Rigidbody != null && !m_Rigidbody.isKinematic)
        {
            m_Rigidbody.linearVelocity = Vector3.zero;
            m_Rigidbody.angularVelocity = Vector3.zero;
        }

        // 恢復 CharacterController 運作
        if (m_CharacterController != null)
        {
            m_CharacterController.enabled = wasCCEnabled;
        }

        Debug.Log($"[PlayerFallRespawn] 玩家掉落低於 Y={fallThresholdY}，已傳送回初始座標：{m_InitialPosition}");
    }
}