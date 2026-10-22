using UnityEngine;

public class TrialRoomManager : MonoBehaviour
{
    public static TrialRoomManager Instance { get; private set; }

    [Header("通關觸發物件")]
    public GameObject exitPortal; // 解謎成功後顯示的傳送門
    public Animator doorAnimator; // 暗門動畫控制器（若有）

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 當 RGB 3D 光學謎題完成時由 TrialRoomPuzzleManager 呼叫
    public void CompleteTrial()
    {
        Debug.Log("🎉 [TrialRoomManager] 收到通關訊號！開啟暗門與傳送門！");

        if (exitPortal != null)
        {
            exitPortal.SetActive(true); // 啟用傳送門
        }

        if (doorAnimator != null)
        {
            doorAnimator.SetTrigger("OpenDoor"); // 播放開門動畫
        }
    }
}