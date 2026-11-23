
using UnityEngine;
using UnityEngine.SceneManagement;

public class GoodEndingExitTrigger : MonoBehaviour
{
    [Header("目標場景")]
    public string targetScene = "GameClear";

    [Header("觸發設定")]
    public bool triggerOnce = true;

    [Header("Debug")]
    public bool showDebugLog = true;

    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        // 確認是否為玩家
        bool isPlayer =
            other.CompareTag("Player") ||
            other.transform.root.CompareTag("Player");

        if (!isPlayer)
        {
            return;
        }

        // 防止重複觸發
        if (triggerOnce && hasTriggered)
        {
            return;
        }

        // 檢查目標場景
        if (!Application.CanStreamedLevelBeLoaded(targetScene))
        {
            Debug.LogError(
                "[GoodEndingExitTrigger] 找不到場景：" +
                targetScene +
                "，請檢查 Build Profiles！"
            );

            return;
        }

        hasTriggered = true;

        if (showDebugLog)
        {
            Debug.Log(
                "[GoodEndingExitTrigger] 玩家走出門口，" +
                "準備進入 " + targetScene
            );
        }

        // 切換到片尾場景
        SceneManager.LoadScene(targetScene);
    }
}
