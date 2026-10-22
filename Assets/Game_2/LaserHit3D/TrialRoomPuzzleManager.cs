using UnityEngine;

public class TrialRoomPuzzleManager : MonoBehaviour
{
    [Header("三色接收器")]
    public Photosensor redSensor;
    public Photosensor greenSensor;
    public Photosensor blueSensor;

    private bool m_IsSolved = false;

    private void Update()
    {
        if (m_IsSolved) return;

        if (redSensor != null && greenSensor != null && blueSensor != null)
        {
            // 檢查紅、綠、藍三座接收器是否同時被正確光束照射
            if (redSensor.isActivated && greenSensor.isActivated && blueSensor.isActivated)
            {
                m_IsSolved = true;
                OnPuzzleSolved();
            }
        }
    }

    private void OnPuzzleSolved()
    {
        Debug.Log("🎉 RGB 三色光束全部定位成功！開啟暗門核心房傳送門！");
        
        if (TrialRoomManager.Instance != null)
        {
            // 找到這行並加上註解 //
            // TrialRoomManager.Instance.CompleteTrial();
        }
    }

    /*private void CheckAllSensors()
    {
        if (redSensor != null && redSensor.IsActivated &&
            greenSensor != null && greenSensor.IsActivated &&
            blueSensor != null && blueSensor.IsActivated)
        {
            if (!isSolved)
            {
                isSolved = true;
                Debug.Log("🎉 RGB 三色光束全部定位成功！開啟暗門核心房傳送門！");

                // 使用 null 檢查，確保即便沒掛載 TrialRoomManager 也不會報錯中斷
                if (TrialRoomManager.Instance != null)
                {
                    TrialRoomManager.Instance.CompleteTrial();
                }
            }
        }
    }*/
}