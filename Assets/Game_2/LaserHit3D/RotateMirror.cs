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

    [Header("上下俯仰 (Pitch) 限制")]
    [Tooltip("向下最大傾斜角度 (負數)")]
    public float minPitchAngle = -45.0f;
    
    [Tooltip("向上最大傾斜角度 (正數)")]
    public float maxPitchAngle = 45.0f;

    [Header("玩家腳本關聯")]
    [Tooltip("若留空，系統會自動尋找 Tag 為 Player 的物件移動腳本")]
    public MonoBehaviour playerMovementScript;

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
            SetControlMode(!isControlling);
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
                // 鏡台與玩家視角同步水平旋轉
                transform.Rotate(Vector3.up, rotAmount, Space.World);
                playerObj.transform.Rotate(Vector3.up, rotAmount, Space.World);
            }

            // --- 上下俯仰傾斜 (W/S 或 上/下鍵) ---
            float vInput = Input.GetAxis("Vertical");
            if (Mathf.Abs(vInput) > 0.01f)
            {
                // W鍵向上抬起，S鍵向下俯低
                float pitchAmount = -vInput * rotationSpeed * Time.deltaTime;
                currentPitch = Mathf.Clamp(currentPitch + pitchAmount, minPitchAngle, maxPitchAngle);

                // 更新鏡台 local 旋轉，保持 Y 軸與 Z 軸穩定
                Vector3 currentEuler = transform.localEulerAngles;
                transform.localRotation = Quaternion.Euler(currentPitch, currentEuler.y, currentEuler.z);
            }
        }
    }

    private void SetControlMode(bool state)
    {
        isControlling = state;
        if (playerMovementScript != null)
        {
            playerMovementScript.enabled = !state;
        }
    }

    // 將角度標準化為 -180 到 180 度
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