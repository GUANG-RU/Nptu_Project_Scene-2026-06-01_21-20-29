using UnityEngine;
using System.Collections;

public class TimedDoor : MonoBehaviour
{
    [Header("機關設定")]
    [Tooltip("門開啟維持的秒數")]
    public float openDuration = 30f; 
    
    [Tooltip("開啟時要移動的距離 (例如 Y軸往上 5公尺)")]
    public Vector3 openOffset = new Vector3(0, 5, 0);

    private Vector3 closedPosition;
    private Coroutine timerCoroutine;

    private void Start()
    {
        // 紀錄遊戲開始時牆壁的初始位置
        closedPosition = transform.position;
    }

    /// <summary>
    /// 給 Photosensor 觸發用的公開函式
    /// </summary>
    public void OpenDoorTemporarily()
    {
        // 如果已經在倒數中，就重新計時
        if (timerCoroutine != null) 
        {
            StopCoroutine(timerCoroutine);
        }
        timerCoroutine = StartCoroutine(DoorRoutine());
    }

    private IEnumerator DoorRoutine()
    {
        // 1. 開門 (瞬間移動位置)
        transform.position = closedPosition + openOffset;
        Debug.Log($"[限時機關] {gameObject.name} 已開啟，倒數 {openDuration} 秒...");

        // 2. 等待設定的秒數
        yield return new WaitForSeconds(openDuration);

        // 3. 時間到，關門 (移回原位)
        transform.position = closedPosition;
        Debug.Log($"[限時機關] {gameObject.name} 倒數結束，已自動關閉！");
    }
}