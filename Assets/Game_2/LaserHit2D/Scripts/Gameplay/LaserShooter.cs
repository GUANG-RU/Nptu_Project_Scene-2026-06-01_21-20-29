using System.Collections.Generic;
using UnityEngine;

namespace LaserHit2D
{
    public class LaserShooter : MonoBehaviour
    {
        [Header("隨機角度設定")]
        [SerializeField] private bool m_IsRandomAngle = false;
        [SerializeField] private float m_MinAngle = -90f;
        [SerializeField] private float m_MaxAngle = 90f;

        [Header("基礎設定")]
        [SerializeField] private GameObject m_LaserPrefab;
        [SerializeField] private int m_LaserNumber;
        public Color m_LaserColor = Color.white;

        [Header("UI 連動設定")]
        [SerializeField] private TMPro.TMP_InputField m_AngleInputField;
        private LaserReflector m_Reflector;
        private bool m_IsUpdatingUI = false;

        [Header("UI 按鈕旋轉速度")]
        [SerializeField] private float m_ButtonRotateStep = 10f;

        [Header("圖形比對器（僅玩家左邊發射器需要綁定）")]
        [SerializeField] private PatternMatcher m_PatternMatcher;

        private float m_CurrentAngle = 0f;

        private void Awake()
        {
            if (m_IsRandomAngle)
            {
                int min = Mathf.RoundToInt(m_MinAngle);
                int max = Mathf.RoundToInt(m_MaxAngle);

                // 🌟 使用 do-while 迴圈，確保開局隨機出來的角度絕對不為 0 度
                do
                {
                    m_CurrentAngle = Random.Range(min, max + 1);
                } 
                while (Mathf.Approximately(m_CurrentAngle, 0f) || m_CurrentAngle == 0);
            }
            else
            {
                m_CurrentAngle = transform.eulerAngles.z;
                if (m_CurrentAngle > 180f) m_CurrentAngle -= 360f;
            }

            NormalizeCurrentAngle();
            ApplyRotation();
        }

        void Start()
        {
            if (m_LaserPrefab != null)
            {
                GameObject laser = Instantiate(m_LaserPrefab, transform.position, Quaternion.identity);
                m_Reflector = laser.GetComponent<LaserReflector>();
                if (m_Reflector != null)
                {
                    m_Reflector.SetLaserNumber(m_LaserNumber);
                }
                
                LineRenderer lr = laser.GetComponent<LineRenderer>();
                if (lr != null)
                {
                    lr.startColor = m_LaserColor;
                    lr.endColor = m_LaserColor;
                }
            }

            UpdateAngleUI();
        }

        // 【關鍵修復 1】：當此元件/UI 被開啟時，同步顯示雷射
        private void OnEnable()
        {
            if (m_Reflector != null)
            {
                m_Reflector.gameObject.SetActive(true);
            }
        }

        // 【關鍵修復 2】：當按下 Exit 或 UI 被關閉時，同步隱藏雷射
        private void OnDisable()
        {
            if (m_Reflector != null)
            {
                m_Reflector.gameObject.SetActive(false);
            }
        }

        void Update()
        {
            if (m_Reflector != null && m_Reflector.gameObject.activeSelf)
            {
                m_Reflector.CastLaser(transform.position, transform.right);
            }

            if (transform.hasChanged)
            {
                UpdateAngleUI();
                transform.hasChanged = false;
            }
        }

        public void RotateLeft()
        {
            m_CurrentAngle += m_ButtonRotateStep;
            NormalizeCurrentAngle();
            ApplyRotation();
            UpdateAngleUI();
            OnValidateOrAngleChanged();
        }

        public void RotateRight()
        {
            m_CurrentAngle -= m_ButtonRotateStep;
            NormalizeCurrentAngle();
            ApplyRotation();
            UpdateAngleUI();
            OnValidateOrAngleChanged();
        }

        public void OnInputAngleChanged(string angleText)
        {
            if (m_IsUpdatingUI) return;

            if (float.TryParse(angleText, out float angle))
            {
                m_CurrentAngle = angle;
                NormalizeCurrentAngle();
                ApplyRotation();
                OnValidateOrAngleChanged();
            }
        }

        public void SetShooterAngle(string angleText)
        {
            OnInputAngleChanged(angleText);
        }

        private void NormalizeCurrentAngle()
        {
            if (m_CurrentAngle >= 360f || m_CurrentAngle <= -360f)
            {
                m_CurrentAngle %= 360f;
            }
        }

        private void ApplyRotation()
        {
            transform.rotation = Quaternion.Euler(0, 0, m_CurrentAngle);
        }

        private void UpdateAngleUI()
        {
            if (m_AngleInputField == null) return;

            m_IsUpdatingUI = true;
            m_AngleInputField.text = Mathf.RoundToInt(m_CurrentAngle).ToString();
            m_IsUpdatingUI = false;
        }

        public List<Vector3> GetLaserPathPoints()
        {
            List<Vector3> points = new List<Vector3>();

            if (m_Reflector != null && m_Reflector.gameObject.activeSelf)
            {
                LineRenderer lr = m_Reflector.GetComponent<LineRenderer>();
                if (lr != null)
                {
                    int count = lr.positionCount;
                    for (int i = 0; i < count; i++)
                    {
                        points.Add(lr.GetPosition(i));
                    }
                }
            }

            return points;
        }

        private void OnValidateOrAngleChanged()
        {
            if (m_PatternMatcher != null)
            {
                bool isMatch = m_PatternMatcher.CheckPatternMatch();
                if (isMatch)
                {
                    Debug.Log("【成功】左右雷射圖形完全相同！");
                }
            }
        }

        public void SetLaserColor(Color color)
        {
            m_LaserColor = color;

            if (m_Reflector != null)
            {
                LineRenderer lr = m_Reflector.GetComponent<LineRenderer>();
                if (lr != null)
                {
                    lr.startColor = color;
                    lr.endColor = color;
                }
            }
        }
    }
}