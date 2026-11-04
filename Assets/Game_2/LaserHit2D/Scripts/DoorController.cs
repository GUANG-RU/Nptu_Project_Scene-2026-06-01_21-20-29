using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DoorController : MonoBehaviour
{
    [Header("門與平板關聯（拉入正面與背面的 TabletPuzzleManager）")]
    [SerializeField] private List<TabletPuzzleManager> m_LinkedTablets = new List<TabletPuzzleManager>();

    // ====== 【連動怪物設定】 ======
    [Header("連動怪物設定")]
    [Tooltip("門【完全升起後】要解鎖行動的怪物（可留空）")]
    [SerializeField] private HateLightMonster_AI m_TargetMonster;

    [Header("門上升設定")]
    [Tooltip("門往上升的高度")]
    [SerializeField] private float m_OpenHeight = 5.0f;

    [Tooltip("開門上升的速度")]
    [SerializeField] private float m_OpenSpeed = 3.0f;

    private Vector3 m_ClosedPosition;
    private Vector3 m_TargetPosition;
    private bool m_IsOpen = false;

    private void Start()
    {
        m_ClosedPosition = transform.localPosition;
        m_TargetPosition = m_ClosedPosition + new Vector3(0, m_OpenHeight, 0);
    }

    /// <summary>
    /// 當任一側平板通關時呼叫此函式
    /// </summary>
    public void OnTabletCleared(TabletPuzzleManager clearedTablet)
    {
        // 1. 執行開門
        OpenDoor();

        // 2. 自動將這一扇門「其他側」的平板同步設為通關
        foreach (var tablet in m_LinkedTablets)
        {
            if (tablet != null && tablet != clearedTablet)
            {
                tablet.ForceSetCleared();
            }
        }
    }

    public void OpenDoor()
    {
        if (m_IsOpen) return;
        m_IsOpen = true;
        StartCoroutine(AnimateDoorOpen());
    }

    private IEnumerator AnimateDoorOpen()
    {
        // 1. 門平滑上升動畫
        while (Vector3.Distance(transform.localPosition, m_TargetPosition) > 0.001f)
        {
            transform.localPosition = Vector3.MoveTowards(
                transform.localPosition, 
                m_TargetPosition, 
                m_OpenSpeed * Time.deltaTime
            );
            yield return null;
        }
        transform.localPosition = m_TargetPosition;

        // 2. 🚨【核心修改】：大門完全升起到底後，才呼叫怪物解鎖！
        if (m_TargetMonster != null)
        {
            m_TargetMonster.UnlockMonster();
        }
    }
}