using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AdaptiveStoryTrigger : MonoBehaviour
{
    // 供 DialogueInputBlocker 讀取的全域對話狀態
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

    private void Start()
    {
        if (targetToLookAt == null && requireFacingTarget)
        {
            targetToLookAt = transform;
        }

        if (startMonsterAIOnComplete && targetMonsters != null)
        {
            SetAllMonstersAIState(false);
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

    private void PrepareAndStartDialogue()
    {
        if (storyManager == null) return;

        int currentCount = GetAndIncreaseEncounterCount(puzzleType);
        int stageIndex = currentCount - 1;
        int stagePriority = Mathf.Max(1, 3 - stageIndex);

        List<StoryEncounterStage> activeStages = encounterStages;
        if ((activeStages == null || activeStages.Count == 0) && storyDatabase != null)
        {
            activeStages = storyDatabase.GetStagesForEvent(puzzleType);
        }

        string[] messagesToPlay = null;
        AudioClip[] voicesToPlay = null;

        if (activeStages != null && stageIndex >= 0 && stageIndex < activeStages.Count)
        {
            StoryEncounterStage stage = activeStages[stageIndex];
            if (stage.messages != null && stage.messages.Length > 0 && !string.IsNullOrWhiteSpace(stage.messages[0]))
            {
                messagesToPlay = stage.messages;
                voicesToPlay = stage.voiceClips;
            }
        }

        if (messagesToPlay == null || messagesToPlay.Length == 0) return;

        hasTriggeredThisLocation = true;
        isDialogueActive = true;

        if (partnerTrigger != null)
        {
            partnerTrigger.hasTriggeredThisLocation = true;
        }

        // 🚨 僅在開啟 enableFreeze 時，才鎖定輸入與凍結組件
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

        // 🚨 僅在開啟 enableFreeze 時，還原定格與解鎖輸入
        if (enableFreeze)
        {
            IsDialoguePlaying = false;
            ToggleFreezeState(false);
        }

        // 只要勾選對話後啟用怪物 AI，不管有沒有開啟定格，對話完都會醒來
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

            MonoBehaviour[] monsterScripts = monster.GetComponents<MonoBehaviour>();
            foreach (var script in monsterScripts)
            {
                if (script == null) continue;
                string sName = script.GetType().Name.ToLower();

                if (sName.Contains("ai") || sName.Contains("monster") || sName.Contains("hearing") || sName.Contains("sound"))
                {
                    script.enabled = enable;
                }
            }
        }
    }

    private void ToggleFreezeState(bool freeze)
    {
        // 1. 呼叫獨立的 DialogueInputBlocker 封鎖/解除按鍵輸入
        DialogueInputBlocker inputBlocker = FindFirstObjectByType<DialogueInputBlocker>();
        if (inputBlocker != null)
        {
            inputBlocker.SetPlayerControlsLock(freeze);
        }

        // 2. 凍結 / 解鎖玩家移動控制腳本 (停用 FirstPersonController 避免警報)
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

        // 3. 凍結 / 解鎖連動怪物的物理與動畫
        if (targetMonsters != null)
        {
            foreach (var monster in targetMonsters)
            {
                if (monster == null) continue;

                SetMonsterScriptStateForSingle(monster, !freeze);

                UnityEngine.AI.NavMeshAgent agent = monster.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = freeze;
                    if (freeze)
                    {
                        agent.velocity = Vector3.zero;
                        agent.ResetPath();
                    }
                }

                Animator monsterAnim = monster.GetComponentInChildren<Animator>();
                if (monsterAnim != null)
                {
                    monsterAnim.speed = freeze ? 0f : 1f;
                }
            }
        }
    }

    private void SetMonsterScriptStateForSingle(GameObject monster, bool enable)
    {
        MonoBehaviour[] monsterScripts = monster.GetComponents<MonoBehaviour>();
        foreach (var script in monsterScripts)
        {
            if (script == null) continue;
            string sName = script.GetType().Name.ToLower();
            if (sName.Contains("ai") || sName.Contains("monster") || sName.Contains("hearing") || sName.Contains("sound"))
            {
                script.enabled = enable;
            }
        }
    }

    private int GetAndIncreaseEncounterCount(string type)
    {
        int count = 0;
        if (saveAcrossSessions)
        {
            string key = "EncounterCount_" + type;
            count = PlayerPrefs.GetInt(key, 0) + 1;
            PlayerPrefs.SetInt(key, count);
            PlayerPrefs.Save();
        }
        else
        {
            if (!s_SessionCounts.ContainsKey(type)) s_SessionCounts[type] = 0;
            s_SessionCounts[type]++;
            count = s_SessionCounts[type];
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