using UnityEngine;

public class RotateMirror : MonoBehaviour
{
    [Header("互動與旋轉設定")]
    [Tooltip("按下 E 鍵可互動的距離")]
    public float interactDistance = 3.0f;
    
    [Tooltip("旋轉速度 (度/秒)")]
    public float rotationSpeed = 60.0f; 
    
    [Tooltip("互動按鍵")]
    public KeyCode interactKey = KeyCode.E;

    [Header("玩家對焦視角限制")]
    [Tooltip("玩家面向鏡子的判定門檻 (0.5 代表玩家正面視野 ±60 度錐形區域內對準鏡子即可互動)")]
    [Range(0f, 1f)]
    public float playerFacingThreshold = 0.5f;

    [Header("上下俯仰 (Pitch) 限制")]
    [Tooltip("向下最大傾斜角度 (負數)")]
    public float minPitchAngle = -45.0f;
    
    [Tooltip("向上最大傾斜角度 (正數)")]
    public float maxPitchAngle = 45.0f;

    [Header("玩家與攝影機關聯")]
    [Tooltip("若留空，系統會自動尋找 Tag 為 Player 的物件移動腳本")]
    public MonoBehaviour playerMovementScript;

    [Tooltip("玩家攝影機 (若留空會自動抓取 Camera.main)")]
    public Camera playerCamera;

    [Header("目前狀態 (唯讀觀察)")]
    public bool isControlling = false;

    private GameObject playerObj;
    private float currentPitch = 0.0f;

    private void Start()
    {
        playerObj = GameObject.FindGameObjectWithTag("Player");
        
        if (playerObj != null && playerMovementScript == null)
        {
            playerMovementScript = playerObj.GetComponent<MonoBehaviour>();
        }

        // 自動抓取主攝影機
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        // 記錄初始 X 軸的角度 (轉換至 -180 ~ 180 範圍)
        Vector3 currentEuler = transform.localEulerAngles;
        currentPitch = NormalizeAngle(currentEuler.x);
    }

    private void Update()
    {
        if (playerObj == null) return;

        float distance = Vector3.Distance(transform.position, playerObj.transform.position);

        // 1. 按下 E 鍵切換操控模式
        if (distance <= interactDistance && Input.GetKeyDown(interactKey))
        {
            if (!isControlling)
            {
                // 開啟操控前，檢查玩家是否面向該鏡子物件
                if (IsPlayerFacingMirror())
                {
                    SetControlMode(true);
                }
                else
                {
                    Debug.Log("無法操控：玩家必須將視野/正面對準鏡子！");
                }
            }
            else
            {
                SetControlMode(false);
            }
        }

        // 2. 超出距離自動解除
        if (isControlling && distance > interactDistance)
        {
            SetControlMode(false);
        }

        // 3. 操控模式邏輯
        if (isControlling)
        {
            // --- 左右水平旋轉 (A/D 或 左/右鍵) ---
            float hInput = Input.GetAxis("Horizontal");
            if (Mathf.Abs(hInput) > 0.01f)
            {
                float rotAmount = hInput * rotationSpeed * Time.deltaTime;
                // 鏡台與玩家身體/視角同步水平旋轉
                transform.Rotate(Vector3.up, rotAmount, Space.World);
                playerObj.transform.Rotate(Vector3.up, rotAmount, Space.World);
            }

            // --- 上下俯仰傾斜 (W/S 或 上/下鍵) ---
            float vInput = Input.GetAxis("Vertical");
            if (Mathf.Abs(vInput) > 0.01f)
            {
                float targetPitchAmount = -vInput * rotationSpeed * Time.deltaTime;
                
                // 計算實際被套用的角度變化 (考慮到 minPitchAngle 與 maxPitchAngle 的限制)
                float previousPitch = currentPitch;
                currentPitch = Mathf.Clamp(currentPitch + targetPitchAmount, minPitchAngle, maxPitchAngle);
                float actualDeltaPitch = currentPitch - previousPitch;

                // 更新鏡台 local 旋轉
                Vector3 currentEuler = transform.localEulerAngles;
                transform.localRotation = Quaternion.Euler(currentPitch, currentEuler.y, currentEuler.z);

                // 🌟 同步旋轉玩家攝影機的 X 軸 (上下俯仰視角)
                if (playerCamera != null)
                {
                    playerCamera.transform.Rotate(Vector3.right, actualDeltaPitch, Space.Self);
                }
            }
        }
    }

    /// <summary>
    /// 檢查玩家的「正面朝向」是否正對著鏡子物件
    /// </summary>
    public bool IsPlayerFacingMirror()
    {
        if (playerObj == null) return false;

        Vector3 dirToMirror = transform.position - playerObj.transform.position;
        dirToMirror.y = 0;
        dirToMirror.Normalize();

        Vector3 playerForward = playerObj.transform.forward;
        playerForward.y = 0;
        playerForward.Normalize();

        float dot = Vector3.Dot(playerForward, dirToMirror);
        return dot >= playerFacingThreshold;
    }

    private void SetControlMode(bool state)
    {
        isControlling = state;
        if (playerMovementScript != null)
        {
            playerMovementScript.enabled = !state;
        }
    }

    private float NormalizeAngle(float angle)
    {
        while (angle > 180) angle -= 360;
        while (angle < -180) angle += 360;
        return angle;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}