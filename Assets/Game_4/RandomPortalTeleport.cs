
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RandomPortalTeleport : MonoBehaviour
{
    [Header("傳送門編號")]
    [Range(1, 2)]
    public int portalNumber = 1;

    [Header("兩個目標場景")]
    public string sceneA = "Bad Ending";
    public string sceneB = "GoodEnding";

    [Header("隨機設定")]
    [Tooltip("勾選：兩個傳送門一定通往不同場景")]
    public bool differentDestinations = true;

    [Header("Debug")]
    public bool showDebugLog = true;

    [SerializeField]
    private string currentDestination;

    // 同一個場景的兩個傳送門共用隨機結果
    private static Dictionary<int, bool> sceneChoices =
        new Dictionary<int, bool>();

    private static bool isTeleporting = false;

    // 每次開始遊戲時清除舊紀錄
    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticData()
    {
        sceneChoices.Clear();
        isTeleporting = false;

        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private static void OnSceneUnloaded(Scene scene)
    {
        sceneChoices.Remove(scene.handle);
        isTeleporting = false;
    }

    void Awake()
    {
        int sceneHandle = gameObject.scene.handle;

        bool portal1GoesToA;

        if (!sceneChoices.TryGetValue(
            sceneHandle, out portal1GoesToA))
        {
            portal1GoesToA = Random.Range(0, 2) == 0;

            sceneChoices.Add(
                sceneHandle, portal1GoesToA);
        }

        if (differentDestinations)
        {
            // Portal 1 和 Portal 2 目的地不重複
            bool goesToA = portalNumber == 1
                ? portal1GoesToA
                : !portal1GoesToA;

            currentDestination = goesToA
                ? sceneA
                : sceneB;
        }
        else
        {
            // 兩個傳送門各自隨機
            currentDestination = Random.Range(0, 2) == 0
                ? sceneA
                : sceneB;
        }

        if (showDebugLog)
        {
            Debug.Log(
                "[PORTAL] Portal " + portalNumber +
                " -> " + currentDestination);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // 只允許玩家觸發
        bool isPlayer = other.CompareTag("Player") ||
                        other.transform.root.CompareTag("Player");

        if (!isPlayer || isTeleporting)
        {
            return;
        }

        if (string.IsNullOrEmpty(currentDestination))
        {
            Debug.LogError(
                "[PORTAL] 沒有設定目標場景！");
            return;
        }

        // 確認場景已加入 Build Profiles
        if (!Application.CanStreamedLevelBeLoaded(
            currentDestination))
        {
            Debug.LogError(
                "[PORTAL] 找不到場景：" +
                currentDestination +
                "，請檢查 Build Profiles！");
            return;
        }

        isTeleporting = true;

        if (showDebugLog)
        {
            Debug.Log(
                "[PORTAL] 玩家進入 Portal " +
                portalNumber +
                "，傳送至 " + currentDestination);
        }

        SceneManager.LoadScene(currentDestination);
    }
}
