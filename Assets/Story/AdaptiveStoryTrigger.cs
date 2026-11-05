using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StoryEncounterStage
{
    [Tooltip("階段標籤 (First / Second / Third)")]
    public string stageName = "First";

    [Header("此階段播放的對話文字與對應配音")]
    [TextArea(2, 5)]
    public string[] messages;

    [Tooltip("對應上述字幕的 AI 配音檔 (數量與順序須與 Messages 一致)")]
    public AudioClip[] voiceClips;
}

public class AdaptiveStoryTrigger : MonoBehaviour
{
    [Header("1. 謎題/事件大分類")]
    [Tooltip("事件分類名稱 (例如 LaserPuzzle 或 EnterGame2)")]
    public string puzzleType = "LaserPuzzle";

    [Header("2. 存檔設定")]
    [Tooltip("勾選後，關掉遊戲重開依然記憶進度次數")]
    public bool saveAcrossSessions = true;

    [Header("3. 正面視線對準與定格目標")]
    [Tooltip("勾選時玩家必須看向目標才觸發；取消勾選時踩入 Trigger 就直接觸發")]
    public bool requireFacingTarget = true;

    [Tooltip("玩家必須正面看向的目標（若 requireFacingTarget 為 true 且此欄留空，Start 時會自動設為此物件自身）")]
    public Transform targetToLookAt;

    [Tooltip("視線觸發的最大夾角（預設 40 度以內）")]
    public float maxFacingAngle = 40f;

    [Header("4. 怪物控制 (支援複數怪物)")]
    [Tooltip("要連動定格/啟動的怪物清單 (例如 Creep2、Monster2...可拉入多隻)")]
    public List<GameObject> targetMonsters = new List<GameObject>(); // 🚨 改為清單列表

    [Tooltip("勾選後：遊戲一開始所有列表中怪物的 AI 會先完全停用，【直到對話全部結束】才正式啟動 AI！")]
    public bool startMonsterAIOnComplete = true;

    [Header("5. 成對夥伴連動 (雙面門/成對物件防重複)")]
    [Tooltip("將成對物件 (如雙面門的另一面平板) 拖入此欄位，觸發時會同步鎖定夥伴物件")]
    public AdaptiveStoryTrigger partnerTrigger;

    [Header("6. 各階段對話清單 (Element 0 = First, Element 1 = Second, Element 2 = Third)")]
    public List<StoryEncounterStage> encounterStages = new List<StoryEncounterStage>();

    [Header("7. 系統引用")]
    public StoryManager storyManager;

    // 內部狀態控管
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

        // 🚨 開場預先將列表中【所有怪物】的 AI 腳本關閉
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

        string[] messagesToPlay = null;
        AudioClip[] voicesToPlay = null;

        if (stageIndex >= 0 && stageIndex < encounterStages.Count)
        {
            StoryEncounterStage stage = encounterStages[stageIndex];
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

        // 對話開始：定格玩家與清空所有怪物的動態位移
        ToggleFreezeState(true);

        storyManager.ShowStories(messagesToPlay, voicesToPlay, stagePriority, OnDialogueCompleted);
    }

    private void OnDialogueCompleted()
    {
        isDialogueActive = false;

        // 解鎖玩家
        ToggleFreezeState(false);

        // 🚨 對話完全結束後正式啟動【所有怪物】的 AI 邏輯
        if (startMonsterAIOnComplete && targetMonsters != null)
        {
            SetAllMonstersAIState(true);
            Debug.Log($"[AdaptiveStoryTrigger] 對話結束！已正式啟動列表中共 {targetMonsters.Count} 隻怪物的 AI 邏輯。");
        }
    }

    /// <summary>
    /// 批次設定列表中所有怪物的 AI 邏輯腳本狀態
    /// </summary>
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

    /// <summary>
    /// 雙向定格/解鎖邏輯
    /// </summary>
    private void ToggleFreezeState(bool freeze)
    {
        // A. 玩家動作定格 / 解鎖
        if (cachedPlayerObj != null)
        {
            CharacterController cc = cachedPlayerObj.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = !freeze;

            MonoBehaviour[] playerScripts = cachedPlayerObj.GetComponents<MonoBehaviour>();
            foreach (var script in playerScripts)
            {
                if (script == null || script == this) continue;
                string sName = script.GetType().Name.ToLower();
                if (sName.Contains("firstperson") || sName.Contains("playercontroller") || sName.Contains("mouselook"))
                {
                    script.enabled = !freeze;
                }
            }

            MonoBehaviour[] camScripts = cachedPlayerObj.GetComponentsInChildren<MonoBehaviour>();
            foreach (var script in camScripts)
            {
                if (script == null) continue;
                string sName = script.GetType().Name.ToLower();
                if (sName.Contains("mouselook") || sName.Contains("rotation") || sName.Contains("camera"))
                {
                    if (!sName.Contains("flashlight") && !sName.Contains("monster"))
                    {
                        script.enabled = !freeze;
                    }
                }
            }
        }

        // B. 🚨 批次定格 / 解鎖列表中的每一隻怪物
        if (targetMonsters != null)
        {
            foreach (var monster in targetMonsters)
            {
                if (monster == null) continue;

                // 1. 腳本開關 (如果是對話進行中的短暫定格)
                SetMonsterScriptStateForSingle(monster, !freeze);

                // 2. 清除 NavMesh 加速度與路徑
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

                // 3. 凍結/恢復 Animator 動畫
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