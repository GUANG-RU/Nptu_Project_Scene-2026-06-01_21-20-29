using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RandomSecretDoorManager : MonoBehaviour
{
    [System.Serializable]
    public class SecretDoorPair
    {
        public string pairName = "暗門組";
        [Tooltip("正面暗門")]
        public Transform frontDoor;
        [Tooltip("對應的背面暗門")]
        public Transform backDoor;
        
        [HideInInspector] public bool isOpen = false; // 紀錄開關狀態
    }

    [Header("4 組對應的暗門設定")]
    [SerializeField] private List<SecretDoorPair> m_DoorPairs = new List<SecretDoorPair>();

    [Header("正面暗門高度設定")]
    [SerializeField] private float m_FrontClosedY = -5.0f;
    [SerializeField] private float m_FrontOpenY = 5.0f;

    [Header("背面暗門高度設定")]
    [SerializeField] private float m_BackClosedY = 21.87798f;
    [SerializeField] private float m_BackOpenY = 26.0f;

    [Header("開啟動畫設定")]
    [SerializeField] private bool m_AnimateOpen = true;
    [SerializeField] private float m_OpenSpeed = 3.0f;

    [Header("🌟 定時加開暗門設定")]
    [Tooltip("是否開啟定時加開門機制")]
    [SerializeField] private bool m_EnableAutoOpenTimer = true;
    [Tooltip("定時加開的等待時間（秒）。300 秒 = 5 分鐘，600 秒 = 10 分鐘")]
    [SerializeField] private float m_AutoOpenDelaySeconds = 300f;

    private void Start()
    {
        // 1. 開局先確保所有暗門回到初始關閉高度
        ResetAllDoorsToClosed();

        // 2. 隨機抽出一組進行開啟
        SelectAndOpenRandomPair();

        // 3. 啟動定時加開暗門的計時器
        if (m_EnableAutoOpenTimer)
        {
            StartCoroutine(AutoOpenDoorRoutine());
        }
    }

    private void ResetAllDoorsToClosed()
    {
        foreach (var pair in m_DoorPairs)
        {
            pair.isOpen = false;

            if (pair.frontDoor != null)
            {
                Vector3 pos = pair.frontDoor.localPosition;
                pos.y = m_FrontClosedY;
                pair.frontDoor.localPosition = pos;
            }

            if (pair.backDoor != null)
            {
                Vector3 pos = pair.backDoor.localPosition;
                pos.y = m_BackClosedY;
                pair.backDoor.localPosition = pos;
            }
        }
    }

    /// <summary>
    /// 從「尚未開啟」的暗門中隨機抽出一組打開
    /// </summary>
    public void SelectAndOpenRandomPair()
    {
        // 篩選出目前還是關閉狀態的門組
        List<SecretDoorPair> closedPairs = m_DoorPairs.FindAll(p => !p.isOpen);

        if (closedPairs.Count == 0)
        {
            Debug.Log("[SecretDoorManager] 所有暗門都已開啟，無需再加開。");
            return;
        }

        // 隨機抽出一組關閉中的門
        int randomIndex = Random.Range(0, closedPairs.Count);
        SecretDoorPair chosenPair = closedPairs[randomIndex];

        if (chosenPair != null)
        {
            chosenPair.isOpen = true; // 標記為已開啟

            if (chosenPair.frontDoor != null) 
                OpenDoorToY(chosenPair.frontDoor, m_FrontOpenY);

            if (chosenPair.backDoor != null) 
                OpenDoorToY(chosenPair.backDoor, m_BackOpenY);

            Debug.Log($"[SecretDoorManager] 成功開啟暗門：{chosenPair.pairName}");
        }
    }

    /// <summary>
    /// 🌟 定時加開暗門協程
    /// </summary>
    /// <summary>
    /// 🌟 每隔設定時間 (如 300 秒) 就自動加開一扇剩餘暗門，直到全部開完為止
    /// </summary>
    private IEnumerator AutoOpenDoorRoutine()
    {
        while (true)
        {
            // 1. 每隔 5 分鐘 (300 秒) 觸發一次
            yield return new WaitForSeconds(m_AutoOpenDelaySeconds);

            // 2. 檢查是否還有未開啟的門
            List<SecretDoorPair> closedPairs = m_DoorPairs.FindAll(p => !p.isOpen);

            if (closedPairs.Count == 0)
            {
                Debug.Log("[SecretDoorManager] 所有暗門已全數開啟，自動停止計時器。");
                yield break; // 全部開完，自動跳出並結束協程
            }

            // 3. 隨機開啟一扇剩餘的暗門
            SelectAndOpenRandomPair();
        }
    }

    private void OpenDoorToY(Transform door, float targetY)
    {
        Vector3 targetPos = door.localPosition;
        targetPos.y = targetY;

        if (m_AnimateOpen)
        {
            StartCoroutine(AnimateDoor(door, targetPos));
        }
        else
        {
            door.localPosition = targetPos;
        }
    }

    private IEnumerator AnimateDoor(Transform door, Vector3 targetPos)
    {
        while (Vector3.Distance(door.localPosition, targetPos) > 0.001f)
        {
            door.localPosition = Vector3.MoveTowards(
                door.localPosition,
                targetPos,
                m_OpenSpeed * Time.deltaTime
            );
            yield return null;
        }
        door.localPosition = targetPos;
    }
}