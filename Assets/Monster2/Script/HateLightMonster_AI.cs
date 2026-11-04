using UnityEngine;
using UnityEngine.AI;

public class HateLightMonster_AI : MonoBehaviour {
    private Animator anim;
    private NavMeshAgent agent; 
    
    // ====== 【門解鎖機制設定】 ======
    [Header("門解鎖機制設定")]
    [Tooltip("勾選：遊戲開始時怪物原地待命，等待大門升起完成呼叫 UnlockMonster() 才開始移動。\n不勾選：維持原本設定（遊戲一開始就到處巡邏）。")]
    public bool useDoorUnlockMechanism = false;
    private bool isUnlocked = false; // 紀錄怪物目前是否解鎖行動

    [Header("目標設定")]
    public Transform player;      

    // ====== 【手電筒自主偵測設定】 ======
    [Header("手電筒自主偵測設定")]
    public Transform flashlight;         // 玩家的手電筒 Transform
    public Light flashlightLight;       // 手電筒的 Light 組件
    public float flashlightStopAngle = 20f; // 手電筒判定的光錐扇形角度
    public LayerMask obstacleLayer;     // 阻擋視線的障礙物圖層（例如牆壁、柱子）

    [Header("移動數值")]
    public float walkSpeed = 2.0f;
    public float runSpeed = 5.5f;
    public float attackRange = 2f; 

    [Header("攻擊命中設定")]
    public float hitRange = 2.5f;        // 真正揮爪時能打到玩家的極限距離
    public float hitAngle = 60f;         // 真正揮爪時能打到玩家的角度扇形範圍（度）

    [Header("狀態冷靜時間")]
    public float calmDownTime = 3.0f; 
    private float detectTimer = 0f;  
    private bool isDetected = false;

    // ====== 【鎖定玩家移動與轉向變數】 ======
    private bool isFreezingPlayer = false; // 目前是否正在定身玩家
    private Vector3 frozenPlayerPos;       // 被抓住時玩家的固定位置

    [Header("視角對準設定")]
    public float lookAtSpeed = 12.0f;    // 鏡頭強行轉向怪物的速度

    // ====== 【鏡頭抖動設定】 ======
    [Header("玩家受撞擊抖動設定")]
    public float shakeIntensity = 0.15f; // 抖動的劇烈程度
    private Vector3 originalCamLocalPos;  // 記錄相機原本的局部座標
    private bool hasSavedCamPos = false;  // 是否已經記錄相機初始位置

    [Header("音效設定")]
    public AudioSource audioSource;
    public AudioClip wanderSound;   // 走路/巡邏聲
    public AudioClip spottedSound;  // 突然發現玩家（照到光）的叫聲
    public AudioClip chaseSound;    // 追逐時的急促聲
    public AudioClip attackSound;   // 攻擊聲

    private int lastState = -1;     // 紀錄上一個狀態，防止重複執行
    private bool hasPlayedSpotted = false; // 確保被照到時的尖叫只播一次

    // 狀態常數
    private const int IDLE = 0;
    private const int WALK = 1;
    private const int RUN = 2;
    private const int ATTACK = 3;

    void Start () {
        anim = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.updateRotation = true;  

        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        // ====== 門解鎖機制初始化 ======
        if (!useDoorUnlockMechanism) {
            // 未勾選機制：預設直接解鎖，維持原本直接巡邏的行為
            isUnlocked = true;
        } else {
            // 有勾選機制：初始鎖定，使 NavMeshAgent 暫停並保持 IDLE
            isUnlocked = false;
            if (agent != null && agent.enabled && agent.isOnNavMesh) {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
            UpdateAnimation(IDLE);
        }
    }
    
    void Update () {
        if (agent == null || !agent.isOnNavMesh) return;

        // ====== 門機制控管：門未開時保持原地 IDLE 並跳過所有 AI 邏輯 ======
        if (!isUnlocked) {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            UpdateAnimation(IDLE);
            return;
        }

        // ====== 每幀自主檢查是否被玩家手電筒照到 ======
        if (IsHitByFlashlight()) {
            detectTimer = calmDownTime; // 被照到就重設驚醒計時器
        }

        // 計時器與定身邏輯
        if (detectTimer > 0) {
            isDetected = true;
            detectTimer -= Time.deltaTime;

            if (isFreezingPlayer && player != null) {
                // 1. 玩家位置處理：固定在原地
                player.position = frozenPlayerPos;

                Camera playerCam = player.GetComponentInChildren<Camera>();

                // 2. 玩家轉向處理：強迫臉朝怪物
                Vector3 lookDir = (transform.position + Vector3.up * 1.2f) - player.position;
                if (lookDir != Vector3.zero) {
                    Vector3 lookDirYaw = lookDir;
                    lookDirYaw.y = 0;
                    if (lookDirYaw != Vector3.zero) {
                        player.rotation = Quaternion.Slerp(player.rotation, Quaternion.LookRotation(lookDirYaw), Time.deltaTime * lookAtSpeed);
                    }

                    if (playerCam != null) {
                        Quaternion targetCamRot = Quaternion.LookRotation(lookDir);
                        playerCam.transform.rotation = Quaternion.Slerp(playerCam.transform.rotation, targetCamRot, Time.deltaTime * lookAtSpeed);
                    }
                }

                // 3. 怪物動作處理：原地停下並切換成 IDLE
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
                UpdateAnimation(IDLE);

                // 確保怪物在原地也會面向玩家
                Vector3 monsterLook = player.position - transform.position;
                monsterLook.y = 0;
                if (monsterLook != Vector3.zero) {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(monsterLook), Time.deltaTime * 15f);
                }

                // 4. 執行鏡頭隨機抖動
                if (playerCam != null) {
                    if (!hasSavedCamPos) {
                        originalCamLocalPos = playerCam.transform.localPosition;
                        hasSavedCamPos = true;
                    }

                    float randomX = Random.Range(-1f, 1f) * shakeIntensity;
                    float randomY = Random.Range(-1f, 1f) * shakeIntensity;
                    float randomZ = Random.Range(-1f, 1f) * (shakeIntensity * 0.3f);

                    playerCam.transform.localPosition = originalCamLocalPos + new Vector3(randomX, randomY, randomZ);
                }
            }
        } else {
            isDetected = false;
            hasPlayedSpotted = false;  

            // 時間到了，解鎖玩家並還原鏡頭
            if (isFreezingPlayer) {
                isFreezingPlayer = false;

                if (player != null && hasSavedCamPos) {
                    Camera playerCam = player.GetComponentInChildren<Camera>();
                    if (playerCam != null) {
                        playerCam.transform.localPosition = originalCamLocalPos;
                    }
                }
                hasSavedCamPos = false;

                TogglePlayerController(true); 
                Debug.Log("🔓 釋放玩家，原地定身與抖動結束。");
            }
        }

        // 核心 AI 切換（非定身狀態下）
        if (!isFreezingPlayer) {
            if (isDetected) {
                ChaseAndAttack();
            } else {
                Patrol();
            }
        }
    }

    /// <summary>
    /// 【供 DoorController 等門腳本呼叫】大門開啟完成後解鎖怪物移動
    /// </summary>
    public void UnlockMonster() {
        isUnlocked = true;
        if (agent != null && agent.enabled && agent.isOnNavMesh) {
            agent.isStopped = false;
        }
        Debug.Log($"🚪 大門完全開啟，怪物 {gameObject.name} 正式解鎖開始行動！");
    }

    // ====== 數學自主偵測手電筒邏輯 ======
    bool IsHitByFlashlight() {
        if (flashlight == null || flashlightLight == null) return false;
        if (!flashlightLight.enabled) return false;

        Vector3 monsterPoint = transform.position + Vector3.up * 1.2f;
        Vector3 directionToMonster = monsterPoint - flashlight.position;
        float distanceToMonster = directionToMonster.magnitude;

        if (distanceToMonster > flashlightLight.range) return false;

        float angle = Vector3.Angle(flashlight.forward, directionToMonster);
        if (angle > flashlightStopAngle) return false;

        if (Physics.Raycast(flashlight.position, directionToMonster.normalized, out RaycastHit hit, distanceToMonster, obstacleLayer)) {
            if (hit.transform != transform && !hit.transform.IsChildOf(transform)) {
                return false;
            }
        }

        return true;
    }

    void ChaseAndAttack() {
        float distance = Vector3.Distance(transform.position, player.position);
        
        if (distance > attackRange) {
            agent.isStopped = false;
            agent.speed = runSpeed;
            agent.SetDestination(player.position);
            UpdateAnimation(RUN);
        } else {
            Attack();
        }
    }

    void Attack() {
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        
        Vector3 targetDir = player.position - transform.position;
        targetDir.y = 0;
        if (targetDir != Vector3.zero) {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(targetDir), Time.deltaTime * 10f);
        }

        UpdateAnimation(ATTACK);
    }

    public void OnAttackHit() {
        if (player == null) return;

        float distance = Vector3.Distance(transform.position, player.position);
        Vector3 dirToPlayer = (player.position - transform.position).normalized;
        float angle = Vector3.Angle(transform.forward, dirToPlayer);

        if (distance <= hitRange && angle <= (hitAngle / 2f)) {
            
            Debug.Log("💥 攻擊成功命中！啟動原地定身、強制面朝怪物與驚恐抖動機制！");

            UltimateFlashlightController flashlightCtrl = player.GetComponentInChildren<UltimateFlashlightController>();
            if (flashlightCtrl != null) {
                flashlightCtrl.RequestTurnOff();
            }

            detectTimer = calmDownTime;
            isFreezingPlayer = true;
            frozenPlayerPos = player.position; 

            TogglePlayerController(false);

        } else {
            Debug.Log("💨 玩家成功走位，躲開了怪物的攻擊！");
        }
    }

    void TogglePlayerController(bool enable) {
        if (player == null) return;

        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = enable;

        MonoBehaviour[] scripts = player.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour script in scripts) {
            string scriptName = script.GetType().Name.ToLower();
            if (scriptName.Contains("firstperson") || scriptName.Contains("playercontroller") || scriptName.Contains("mouselook")) {
                script.enabled = enable;
            }
        }

        MonoBehaviour[] camScripts = player.GetComponentsInChildren<MonoBehaviour>();
        foreach (MonoBehaviour script in camScripts) {
            string scriptName = script.GetType().Name.ToLower();
            if (scriptName.Contains("mouselook") || scriptName.Contains("rotation") || scriptName.Contains("camera")) {
                if (!scriptName.Contains("flashlight") && !scriptName.Contains("monster")) {
                    script.enabled = enable;
                }
            }
        }
    }

    void Patrol() {
        agent.isStopped = false;
        agent.speed = walkSpeed;

        if (!agent.hasPath || agent.remainingDistance < 0.5f) {
             Vector3 randomDest = transform.position + new Vector3(Random.Range(-10, 10), 0, Random.Range(-10, 10));
             agent.SetDestination(randomDest);
        }
        UpdateAnimation(WALK);
    }

    void UpdateAnimation(int stateValue) {
        if (stateValue == lastState) return;
        lastState = stateValue;

        HandleSound(stateValue);

        anim.SetInteger("state", stateValue);
        anim.SetInteger("moving", (stateValue == 1) ? 1 : 0);
        anim.SetInteger("run",    (stateValue == 2) ? 1 : 0);
        anim.SetInteger("attack", (stateValue == 3) ? 1 : 0);
    }

    void HandleSound(int state) {
        if (audioSource == null) return;

        switch (state) {
            case WALK:
                audioSource.clip = wanderSound;
                audioSource.loop = true;
                audioSource.Play();
                break;

            case RUN:
                if (!hasPlayedSpotted && spottedSound != null) {
                    audioSource.PlayOneShot(spottedSound);
                    hasPlayedSpotted = true;
                }
                audioSource.clip = chaseSound;
                audioSource.loop = true;
                audioSource.Play();
                break;

            case ATTACK:
                if (attackSound != null) {
                    audioSource.PlayOneShot(attackSound);
                }
                break;

            case IDLE:
                audioSource.Stop();
                break;
        }
    }
}