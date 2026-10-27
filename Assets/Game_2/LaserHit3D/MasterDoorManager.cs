using UnityEngine;

public class MasterDoorManager : MonoBehaviour
{
    [Header("--- 關卡感應器需求 ---")]
    [Tooltip("開啟大門所需的所有 Photosensor (可手動拉入；若留空會自動搜尋全場景感應器)")]
    public Photosensor[] requiredSensors;

    [Header("--- 大門開關設定 ---")]
    [Tooltip("要開啓的大門 Transform (若留空則預設使用此物件自身)")]
    public Transform doorTransform;

    [Tooltip("門開啟時相對原位置的移動向量 (例如 Y 軸往上 5 米)")]
    public Vector3 openOffset = new Vector3(0, 5f, 0);

    [Tooltip("開門 / 關門平滑移動速度")]
    public float moveSpeed = 3.0f;

    [Tooltip("勾選後：一旦大門開啟就不會再關閉 (一次性永久解鎖)")]
    public bool isOneTimeUnlock = false;

    [Header("--- 當前關卡狀態 (唯讀觀察) ---")]
    public bool isAllActivated = false;

    private Vector3 closedPosition;
    private Vector3 openPosition;
    private bool isUnlockedPermanently = false;

    private void Start()
    {
        // 若 Inspector 沒有手動指定，自動搜尋場景中所有的 Photosensor
        if (requiredSensors == null || requiredSensors.Length == 0)
        {
            requiredSensors = FindObjectsByType<Photosensor>(FindObjectsSortMode.None);
        }

        if (doorTransform == null)
        {
            doorTransform = transform;
        }

        // 紀錄關閉與開啟時的世界座標
        closedPosition = doorTransform.position;
        openPosition = closedPosition + openOffset;
    }

    private void Update()
    {
        if (isUnlockedPermanently) return;

        // 每幀檢查是否「所有」感應器皆被正確顏色點亮
        bool allActive = CheckAllSensors();

        if (allActive)
        {
            if (!isAllActivated)
            {
                isAllActivated = true;
                Debug.Log("<color=green>[關卡總控] 所有 Photosensor 皆已點亮！大門開啟！</color>");
                if (isOneTimeUnlock) isUnlockedPermanently = true;
            }

            // 平滑移動至開啟位置
            doorTransform.position = Vector3.Lerp(doorTransform.position, openPosition, Time.deltaTime * moveSpeed);
        }
        else
        {
            if (isAllActivated)
            {
                isAllActivated = false;
                Debug.Log("<color=yellow>[關卡總控] 光線中斷，大門關閉！</color>");
            }

            // 平滑移動至關閉位置
            doorTransform.position = Vector3.Lerp(doorTransform.position, closedPosition, Time.deltaTime * moveSpeed);
        }
    }

    /// <summary>
    /// 遍歷陣列，確認每個 Photosensor 的 isActivated 狀態
    /// </summary>
    private bool CheckAllSensors()
    {
        if (requiredSensors == null || requiredSensors.Length == 0) return false;

        foreach (var sensor in requiredSensors)
        {
            if (sensor == null || !sensor.isActivated)
            {
                return false; // 只要有一個感應器未亮起，即回傳 false
            }
        }
        return true; // 全部皆亮起
    }
}