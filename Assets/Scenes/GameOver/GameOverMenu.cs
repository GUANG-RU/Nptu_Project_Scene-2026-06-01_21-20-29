using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverManager : MonoBehaviour
{
    [Header("Bad Ending 設定")]
    public string badEndingSceneName = "Bad Ending";
    public string badEndingRetryScene = "Game_4";

    void Start()
    {
        // 顯示滑鼠
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 恢復遊戲時間
        Time.timeScale = 1f;
    }

    // ============================================
    // Retry 按鈕
    // ============================================
    public void RetryLevel()
    {
        string lastLevel =
            PlayerPrefs.GetString(
                "LastLevel",
                "Level1"
            );

        // Bad Ending 死亡時返回 Game_4
        if (lastLevel == badEndingSceneName)
        {
            SceneManager.LoadScene(
                badEndingRetryScene
            );
        }
        else
        {
            // 其他關卡維持原本的 Retry 機制
            SceneManager.LoadScene(
                lastLevel
            );
        }
    }

    // ============================================
    // 回主選單
    // ============================================
    public void BackToMainMenu()
    {
        SceneManager.LoadScene(
            "StartMenu"
        );
    }
}