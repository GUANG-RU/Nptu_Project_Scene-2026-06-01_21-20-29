using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LaserHit2D
{
    public class PuzzleMenuManager : MonoBehaviour
    {
        [Header("退回的 3D 主場景名稱")]
        [SerializeField] private string m_Main3DSceneName = "SampleScene";

        [Header("UI 元素")]
        [SerializeField] private Button m_ResetButton;
        [SerializeField] private TextMeshProUGUI m_ResetButtonText;

        [Header("Reset 冷卻時間設定")]
        [SerializeField] private float m_CooldownTime = 10f;
        [SerializeField] private LaserShooter m_TargetShooter;

        // 【關鍵修復 1】：移除 static，讓每個拼圖物件擁有獨立的冷卻倒數時間
        private float m_CooldownEndTime = 0f;

        // 【關鍵修復 2】：使用獨立區域變數，不再依賴全域 static 變數
        private bool m_IsCleared = false;
        private int m_ResetCount = 0;

        private void Awake()
        {
            // 確保滑鼠解鎖並顯示，允許 UI 點擊
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Start()
        {
            if (m_ResetButton != null)
            {
                m_ResetButton.onClick.AddListener(OnResetButtonPressed);
            }
        }

        private void Update()
        {
            if (Time.time < m_CooldownEndTime)
            {
                UpdateCooldownUI();
            }
            else if (m_ResetButtonText != null && m_ResetButtonText.text != "Reset")
            {
                ResetCooldownUI();
            }
        }

        public void OnResetButtonPressed()
        {
            if (Time.time < m_CooldownEndTime || m_IsCleared) return;

            m_ResetCount++;
            ResetTargetLaser();

            m_CooldownEndTime = Time.time + m_CooldownTime;
            UpdateCooldownUI();
        }

        private void UpdateCooldownUI()
        {
            float remainingTime = m_CooldownEndTime - Time.time;

            if (remainingTime > 0f)
            {
                if (m_ResetButton != null) m_ResetButton.interactable = false;
                if (m_ResetButtonText != null)
                {
                    m_ResetButtonText.text = $"Reset ({Mathf.CeilToInt(remainingTime)}s)";
                }
            }
            else
            {
                ResetCooldownUI();
            }
        }

        private void ResetCooldownUI()
        {
            if (m_ResetButtonText != null)
            {
                m_ResetButtonText.text = "Reset";
            }

            if (m_ResetButton != null)
            {
                // 只檢查本關卡是否通關，不再停用其他關卡的 Reset 按鈕
                m_ResetButton.interactable = !m_IsCleared;
            }
        }

        public void StopCooldown()
        {
            m_CooldownEndTime = 0f;
            m_IsCleared = true; // 本關卡通關，僅停用本關卡 Reset
            ResetCooldownUI();
        }

        private void ResetTargetLaser()
        {
            if (m_TargetShooter == null) return;

            int randomAngle = 0;

            if (m_ResetCount >= 2)
            {
                // 🌟 重置 2 次以上 (含第二次)：10 的倍數 (-90, -80, ..., 80, 90)，排除 0
                do
                {
                    randomAngle = Random.Range(-9, 10) * 10;
                } 
                while (randomAngle == 0);
            }
            else if (m_ResetCount == 1)
            {
                // 🌟 按下 Reset 第 1 次：5 的倍數 (-90, -85, ..., 85, 90)，排除 0
                do
                {
                    randomAngle = Random.Range(-18, 19) * 5;
                } 
                while (randomAngle == 0);
            }
            else
            {
                // 🌟 初始未按 Reset (m_ResetCount == 0)：全範圍隨機角度，排除 0
                do
                {
                    randomAngle = Random.Range(-90, 91);
                } 
                while (randomAngle == 0);
            }

            m_TargetShooter.transform.rotation = Quaternion.Euler(0, 0, randomAngle);
        }

        // Exit 按鈕邏輯
        public void OnExitButtonPressed()
        {
            Debug.Log("【Exit】離開 2D 謎題");

            TabletPuzzleManager tabletManager = FindFirstObjectByType<TabletPuzzleManager>();
            
            if (tabletManager != null)
            {
                tabletManager.ClosePuzzle();
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                SceneManager.LoadScene(m_Main3DSceneName);
            }
        }
    }
}