using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AdaptiveStoryTrigger : MonoBehaviour
{
    // 供 DialogueInputBlocker 與外部系統讀取的全域對話狀態
    public static bool IsDialoguePlaying { get; private set; } = false;

    [Header("1. 謎題/事件大分類")]
    [Tooltip("事件分類標籤 (例如 Monster1, Monster2, LaserPuzzle)")]
    public string puzzleType = "Monster1";

    [Header("2. 全域對話資料庫 (必拉)")]
    [Tooltip("將 Project 中的 StoryDatabase 資產拖入此處")]
    public StoryDatabaseSO storyDatabase;

    [Header("3. 存檔設定")]
    public bool saveAcrossSessions = true;

    [Header("4. 正面視線對準與定格控制")]
    public bool requireFacingTarget = true;
    public Transform targetToLookAt;
    public float maxFacingAngle = 40f;
    [Tooltip("對話期間是否凍結玩家操控與連動怪物 (取消勾選則可在對話時自由移動與操作)")]
    public bool enableFreeze = true;

    [Header("5. 怪物控制 (支援複數怪物)")]
    public List<GameObject> targetMonsters = new List<GameObject>();
    public bool startMonsterAIOnComplete = true;

    [Header("6. 成對夥伴連動 (雙面門/成對物件防重複)")]
    public AdaptiveStoryTrigger partnerTrigger;

    [Header("7. 個別階段對話 override (可留空，若留空會自動從 StoryDatabase 讀取)")]
    public List<StoryEncounterStage> encounterStages = new List<StoryEncounterStage>();

    [Header("8. 系統引用")]
    public StoryManager storyManager;

    private bool hasTriggeredThisLocation = false;
    private bool isPlayerInZone = false;
    private bool isDialogueActive = false;
    private GameObject cachedPlayerObj;

    private static Dictionary<string, int> s_SessionCounts = new Dictionary<string, int>();

    private void Awake()
    {
        if (startMonsterAIOnComplete && targetMonsters != null && targetMonsters.Count > 0)
        {
            // 只有在「還有可觸發的對話階段」時，才在開場鎖定怪物；
            // 若所有 Encounter Stages 都已播完或沒有設定任何對話，直接讓怪物自由行動
            bool hasDialogueToPlay = HasValidDialogueForNextStage();
            SetAllMonstersAIState(!hasDialogueToPlay);
        }
    }

    private void Start()
    {
        if (targetToLookAt == null && requireFacingTarget)
        {
            targetToLookAt = transform;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggeredThisLocation) return;
        if (other.CompareTag("Player") || other.gameObject.name.Contains("FPSController"))
        {
            cachedPlayerObj = other.gameObject;
            isPlayerInZone = true;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (hasTriggeredThisLocation) return;
        if (!isPlayerInZone && (other.CompareTag("Player") || other.gameObject.name.Contains("FPSController")))
        {
            cachedPlayerObj = other.gameObject;
            isPlayerInZone = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.gameObject.name.Contains("FPSController"))
        {
            isPlayerInZone = false;
        }
    }

    private void Update()
    {
        if (isPlayerInZone && !hasTriggeredThisLocation && !isDialogueActive)
        {
            if (IsPlayerFacingTarget())
            {
                PrepareAndStartDialogue();
            }
        }
    }

    private bool IsPlayerFacingTarget()
    {
        if (!requireFacingTarget) return true;
        if (targetToLookAt == null) return true;

        Camera mainCam = Camera.main;
        if (mainCam == null && cachedPlayerObj != null)
        {
            mainCam = cachedPlayerObj.GetComponentInChildren<Camera>();
        }

        if (mainCam == null) return true;

        Vector3 dirToTarget = (targetToLookAt.position - mainCam.transform.position).normalized;
        float angle = Vector3.Angle(mainCam.transform.forward, dirToTarget);

        return angle <= maxFacingAngle;
    }

    /// <summary>
    /// 檢查下一個階段是否還有有效的對話內容可播放（不增加計數器）
    /// </summary>
    private bool HasValidDialogueForNextStage()
    {
        int nextStageIndex = GetCurrentEncounterCount(puzzleType);
        List<StoryEncounterStage> activeStages = GetActiveStagesList();

        if (activeStages != null && nextStageIndex >= 0 && nextStageIndex < activeStages.Count)
        {
            StoryEncounterStage stage = activeStages[nextStageIndex];
            if (stage.messages != null && stage.messages.Length > 0 && !string.IsNullOrWhiteSpace(stage.messages[0]))
            {
                return true;
            }
        }

        return false;
    }

    private List<StoryEncounterStage> GetActiveStagesList()
    {
        if (encounterStages != null && encounterStages.Count > 0)
        {
            return encounterStages;
        }
        if (storyDatabase != null)
        {
            return storyDatabase.GetStagesForEvent(puzzleType);
        }
        return null;
    }

    private void PrepareAndStartDialogue()
    {
        if (storyManager == null) return;

        // 若所有階段皆已觸發完畢或沒有任何有效對話，標記完成並確保怪物可自由移動
        if (!HasValidDialogueForNextStage())
        {
            hasTriggeredThisLocation = true;
            if (partnerTrigger != null)
            {
                partnerTrigger.hasTriggeredThisLocation = true;
            }

            if (startMonsterAIOnComplete && targetMonsters != null)
            {
                SetAllMonstersAIState(true);
            }
            return;
        }

        int currentCount = GetAndIncreaseEncounterCount(puzzleType);
        int stageIndex = currentCount - 1;
        int stagePriority = Mathf.Max(1, 3 - stageIndex);

        List<StoryEncounterStage> activeStages = GetActiveStagesList();
        StoryEncounterStage stage = activeStages[stageIndex];

        string[] messagesToPlay = stage.messages;
        AudioClip[] voicesToPlay = stage.voiceClips;

        hasTriggeredThisLocation = true;
        isDialogueActive = true;

        if (partnerTrigger != null)
        {
            partnerTrigger.hasTriggeredThisLocation = true;
        }

        if (enableFreeze)
        {
            IsDialoguePlaying = true;
            ToggleFreezeState(true);
        }

        storyManager.ShowStories(messagesToPlay, voicesToPlay, stagePriority, OnDialogueCompleted);
    }

    private void OnDialogueCompleted()
    {
        isDialogueActive = false;

        if (enableFreeze)
        {
            IsDialoguePlaying = false;
            ToggleFreezeState(false);
        }

        if (startMonsterAIOnComplete && targetMonsters != null)
        {
            SetAllMonstersAIState(true);
            Debug.Log($"[AdaptiveStoryTrigger] 對話結束！已啟動 {targetMonsters.Count} 隻怪物的 AI。");
        }
    }

    private void SetAllMonstersAIState(bool enable)
    {
        if (targetMonsters == null) return;

        foreach (var monster in targetMonsters)
        {
            if (monster == null) continue;

            // 1. 啟用 / 停用怪物 AI 與聽覺腳本
            MonoBehaviour[] monsterScripts = monster.GetComponents<MonoBehaviour>();
            foreach (var script in monsterScripts)
            {
                if (script == null || script == this) continue;
                string sName = script.GetType().Name.ToLower();

                if (sName.Contains("ai") || sName.Contains("monster") || sName.Contains("hearing") || sName.Contains("sound"))
                {
                    script.enabled = enable;
                }
            }

            // 2. 控制 NavMeshAgent 尋路狀態
            UnityEngine.AI.NavMeshAgent agent = monster.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = !enable;
                if (!enable)
                {
                    agent.velocity = Vector3.zero;
                    agent.ResetPath();
                }
            }

            // 3. 控制怪物動畫速度
            Animator monsterAnim = monster.GetComponentInChildren<Animator>();
            if (monsterAnim != null)
            {
                monsterAnim.speed = enable ? 1f : 0f;
            }
        }
    }

    private void ToggleFreezeState(bool freeze)
    {
        DialogueInputBlocker inputBlocker = FindFirstObjectByType<DialogueInputBlocker>();
        if (inputBlocker != null)
        {
            inputBlocker.SetPlayerControlsLock(freeze);
        }

        if (cachedPlayerObj != null)
        {
            MonoBehaviour[] playerScripts = cachedPlayerObj.GetComponentsInChildren<MonoBehaviour>();
            foreach (var script in playerScripts)
            {
                if (script == null || script == this) continue;
                string sName = script.GetType().Name.ToLower();

                if (sName.Contains("firstperson") || sName.Contains("playercontroller") || sName.Contains("mouselook"))
                {
                    script.enabled = !freeze;
                }
            }
        }

        if (targetMonsters != null)
        {
            SetAllMonstersAIState(!freeze);
        }
    }

    private int GetCurrentEncounterCount(string type)
    {
        if (saveAcrossSessions)
        {
            return PlayerPrefs.GetInt("EncounterCount_" + type, 0);
        }
        return s_SessionCounts.ContainsKey(type) ? s_SessionCounts[type] : 0;
    }

    private int GetAndIncreaseEncounterCount(string type)
    {
        int count = GetCurrentEncounterCount(type) + 1;
        if (saveAcrossSessions)
        {
            PlayerPrefs.SetInt("EncounterCount_" + type, count);
            PlayerPrefs.Save();
        }
        else
        {
            s_SessionCounts[type] = count;
        }
        return count;
    }

    [ContextMenu("重置此 Puzzle 類型的遭遇次數進度")]
    public void ResetEncounterCount()
    {
        if (saveAcrossSessions)
        {
            PlayerPrefs.DeleteKey("EncounterCount_" + puzzleType);
            PlayerPrefs.Save();
        }
        s_SessionCounts.Remove(puzzleType);
        Debug.Log($"[AdaptiveStoryTrigger] 已重置 [{puzzleType}] 的進度紀錄！");
    }
}