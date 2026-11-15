using UnityEngine;
using UnityEngine.UI;
using LaserHit2D;

public class TabletPuzzleManager : MonoBehaviour
{
    private static int s_GlobalTabletCount = 0;

    [Header("拼圖 Prefab 設定")]
    [SerializeField] private GameObject m_PuzzlePrefab;
    [SerializeField] private Vector3 m_BaseSpawnPosition = new Vector3(1000, 0, 0);

    [Header("3D 平板 / 螢幕 Renderer 設定")]
    [Tooltip("若使用獨立 Quad (GameScreen)，請拉入 GameScreen；若使用原本 3D 模型，請拉入 DisplayArea")]
    [SerializeField] private Renderer m_TabletRenderer;

    [Tooltip("要替換貼圖的 Material 索引（若用 Quad 請填 0；若用原本 3D 模型可填 2 與 3）")]
    [SerializeField] private int[] m_MaterialIndices = new int[] { 0 };

    [SerializeField] private Material m_ClearedMaterial;

    [Header("UI 與玩家控制")]
    [SerializeField] private GameObject m_InteractPromptUI;        
    [Tooltip("若做成 Prefab 可留空，腳本會在遊戲開始時自動尋找 Tag 為 Player 的物件腳本")]
    [SerializeField] private MonoBehaviour m_PlayerMovementScript; 

    [Header("門的控制（通關時開啟）")]
    [SerializeField] private DoorController m_DoorController;

    private GameObject m_CurrentPuzzleInstance;
    private Camera m_PuzzleCamera;
    private RenderTexture m_PuzzleRT; 

    private bool m_IsPlayerInRange = false;
    private bool m_IsInteracting = false;
    private bool m_IsCleared = false;

    private void Start()
    {
        // 1. 自動尋找玩家移動腳本（解決 Prefab 丢失引用的問題）
        FindPlayerMovementScript();

        if (!m_IsCleared && m_CurrentPuzzleInstance == null && m_PuzzlePrefab != null)
        {
            SpawnPuzzleInstance();
        }
    }

    /// <summary>
    /// 自動搜尋場景中 Tag 為 "Player" 的物件並取得其移動控制腳本
    /// </summary>
    private void FindPlayerMovementScript()
    {
        if (m_PlayerMovementScript == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                // 優先抓取玩家身上的控制腳本
                MonoBehaviour[] scripts = player.GetComponents<MonoBehaviour>();
                foreach (var script in scripts)
                {
                    if (script != null && script != this && script.enabled)
                    {
                        m_PlayerMovementScript = script;
                        break;
                    }
                }
            }
        }
    }

    void Update()
    {
        // 🚨 對話定格期間，忽略所有 E / Esc 互動按鍵（全場景雷射平板同步生效）
        if (AdaptiveStoryTrigger.IsDialoguePlaying) return;

        if (m_IsCleared && !m_IsInteracting) return;

        if (m_IsInteracting)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E))
            {
                ClosePuzzle();
            }
        }
        else if (m_IsPlayerInRange && Input.GetKeyDown(KeyCode.E))
        {
            TogglePuzzleMode(true);
        }
    }

    private void SpawnPuzzleInstance()
    {
        Vector3 uniqueSpawnPos = m_BaseSpawnPosition + new Vector3(0, s_GlobalTabletCount * 100f, 0);
        s_GlobalTabletCount++;

        m_CurrentPuzzleInstance = Instantiate(m_PuzzlePrefab, uniqueSpawnPos, Quaternion.identity);
        
        WinUIManager winUI = m_CurrentPuzzleInstance.GetComponentInChildren<WinUIManager>(true);
        if (winUI != null)
        {
            winUI.SetOwnerTabletManager(this);
        }

        m_PuzzleCamera = m_CurrentPuzzleInstance.GetComponentInChildren<Camera>();
        if (m_PuzzleCamera != null)
        {
            RenderTexture baseRT = m_PuzzleCamera.targetTexture;
            if (baseRT != null)
            {
                m_PuzzleRT = new RenderTexture(baseRT);
                m_PuzzleCamera.targetTexture = m_PuzzleRT;
                ApplyRTToMaterials();
            }
        }

        Button[] buttons = m_CurrentPuzzleInstance.GetComponentsInChildren<Button>(true);
        foreach (Button btn in buttons)
        {
            if (btn.name.ToLower().Contains("exit") || btn.name.ToLower().Contains("close"))
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(ClosePuzzle);
            }
        }
    }

    private void ApplyRTToMaterials()
    {
        if (m_TabletRenderer == null || m_PuzzleRT == null) return;

        Material[] mats = m_TabletRenderer.materials;
        
        if (m_MaterialIndices != null && m_MaterialIndices.Length > 0)
        {
            foreach (int idx in m_MaterialIndices)
            {
                if (idx >= 0 && idx < mats.Length)
                {
                    mats[idx].mainTexture = m_PuzzleRT;
                }
            }
        }
        m_TabletRenderer.materials = mats;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !m_IsCleared)
        {
            m_IsPlayerInRange = true;
            if (m_InteractPromptUI != null) m_InteractPromptUI.SetActive(true);

            if (m_CurrentPuzzleInstance == null && m_PuzzlePrefab != null)
            {
                SpawnPuzzleInstance();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            m_IsPlayerInRange = false;
            if (m_InteractPromptUI != null) m_InteractPromptUI.SetActive(false);

            // 【關鍵修改】：移除原先在 OnTriggerExit 裡面自動呼叫 TogglePuzzleMode(false) 的邏輯
            // 避免因為角色邊界碰撞微移而意外中斷拼圖
        }
    }

    public void TogglePuzzleMode(bool enablePuzzle)
    {
        m_IsInteracting = enablePuzzle;

        if (m_PlayerMovementScript == null)
        {
            FindPlayerMovementScript();
        }

        if (enablePuzzle && !m_IsCleared)
        {
            MirrorRotator.s_IsGameCleared = false;
        }

        if (m_InteractPromptUI != null)
            m_InteractPromptUI.SetActive(!m_IsInteracting && m_IsPlayerInRange && !m_IsCleared);

        // 切換玩家移動腳本
        if (m_PlayerMovementScript != null)
            m_PlayerMovementScript.enabled = !m_IsInteracting;

        if (m_IsInteracting)
        {
            // === 進入 2D 拼圖模式 ===
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // === 離開 2D 拼圖模式 ===
            if (UnityEngine.EventSystems.EventSystem.current != null)
            {
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // 關鍵：不管在遊戲中還是 UI 中，都強制關閉輸入法！
        // 這樣 TMP_InputField 就能直接接收純數字輸入，且 Shift+Space 不會再引發全形問題
        Input.imeCompositionMode = IMECompositionMode.Off;

        if (m_CurrentPuzzleInstance != null)
        {
            WinUIManager winUI = m_CurrentPuzzleInstance.GetComponentInChildren<WinUIManager>(true);
            if (winUI != null)
            {
                winUI.UpdateInstructionText(enablePuzzle);
            }
        }

        if (m_PuzzleCamera != null)
        {
            if (m_IsInteracting)
            {
                m_PuzzleCamera.targetTexture = null;
                m_PuzzleCamera.depth = 100;
            }
            else
            {
                m_PuzzleCamera.targetTexture = m_PuzzleRT;
                m_PuzzleCamera.depth = -1;
            }
        }
    }

    public void OnPuzzleCleared()
    {
        m_IsCleared = true;

        if (m_InteractPromptUI != null) 
            m_InteractPromptUI.SetActive(false);

        if (m_DoorController != null)
        {
            m_DoorController.OnTabletCleared(this);
        }
    }

    public void ForceSetCleared()
    {
        if (m_IsCleared) return;
        m_IsCleared = true;

        if (m_InteractPromptUI != null)
            m_InteractPromptUI.SetActive(false);

        if (m_CurrentPuzzleInstance != null)
        {
            WinUIManager winUI = m_CurrentPuzzleInstance.GetComponentInChildren<WinUIManager>(true);
            if (winUI != null)
            {
                winUI.SetStatus(true);
            }
        }

        if (m_ClearedMaterial != null && m_TabletRenderer != null)
        {
            Material[] mats = m_TabletRenderer.materials;
            if (m_MaterialIndices != null)
            {
                foreach (int idx in m_MaterialIndices)
                {
                    if (idx >= 0 && idx < mats.Length)
                    {
                        mats[idx] = m_ClearedMaterial;
                    }
                }
            }
            m_TabletRenderer.materials = mats;
        }
    }

    public void ClosePuzzle()
    {
        if (m_IsInteracting)
        {
            TogglePuzzleMode(false);
        }
    }

    private void OnDestroy()
    {
        if (m_PuzzleRT != null)
        {
            m_PuzzleRT.Release();
            Destroy(m_PuzzleRT);
        }
    }
}