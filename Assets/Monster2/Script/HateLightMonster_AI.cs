using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement; 

public class HateLightMonster_AI : MonoBehaviour {
    private Animator anim;
    private NavMeshAgent agent; 
    
    // ====== 【門解鎖機制設定】 ======
    [Header("門解鎖機制設定")]
    [Tooltip("勾選：遊戲開始時怪物原地待命，等待大門升起完成呼叫 UnlockMonster() 才開始移動。\n不勾選：維持原本設定（遊戲一開始就到處巡邏）。")]
    public bool useDoorUnlockMechanism = false;
    private bool isUnlocked = false; 

    [Header("目標設定")]
    public Transform player;      

    // ====== 【手電筒自主偵測設定】 ======
    [Header("手電筒自主偵測設定")]
    public Transform flashlight;         
    public Light flashlightLight;       
    public float flashlightStopAngle = 20f; 
    public LayerMask obstacleLayer;     

    [Header("移動數值")]
    public float walkSpeed = 2.0f;
    public float runSpeed = 5.5f;
    public float attackRange = 2f; 

    [Header("攻擊命中設定")]
    public float hitRange = 2.5f;        
    public float hitAngle = 60f;         

    [Header("狀態冷靜時間")]
    public float calmDownTime = 3.0f; 
    private float detectTimer = 0f;  
    private bool isDetected = false;

    // ====== 【鎖定玩家移動與轉向變數】 ======
    private bool isFreezingPlayer = false; 
    private Vector3 frozenPlayerPos;       
    private bool isShakingCamera = false; // 【新增這行】專門用來控制抖動     

    [Header("視角對準設定")]
    public float lookAtSpeed = 12.0f;    

    // ====== 【鏡頭抖動設定】 ======
    [Header("玩家受撞擊抖動設定")]
    public float shakeIntensity = 0.15f; 
    private Vector3 originalCamLocalPos;  
    private bool hasSavedCamPos = false;  

    [Header("Game Over 設定")]
    [Tooltip("定身演出後，等待多久才跳轉結算畫面")]
    public float gameOverDelay = 2f;
    [Tooltip("結算畫面的場景名稱")]
    public string gameOverSceneName = "GameOver";

    [Header("音效設定")]
    public AudioSource audioSource;
    public AudioClip wanderSound;   
    public AudioClip spottedSound;  
    public AudioClip chaseSound;    
    public AudioClip attackSound;   

    private int lastState = -1;     
    private bool hasPlayedSpotted = false; 

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

        if (!useDoorUnlockMechanism) {
            isUnlocked = true;
        } else {
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

        if (!isUnlocked) {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            UpdateAnimation(IDLE);
            return;
        }

        if (IsHitByFlashlight()) {
            detectTimer = calmDownTime; 
        }

        if (detectTimer > 0) {
            isDetected = true;
            detectTimer -= Time.deltaTime;

            if (isFreezingPlayer && player != null) {
                // 1. 固定玩家位置
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

                // 3. 怪物原地停下 (已刪除 UpdateAnimation(IDLE) 避免打斷揮爪動畫)
                agent.isStopped = true;
                agent.velocity = Vector3.zero;

                Vector3 monsterLook = player.position - transform.position;
                monsterLook.y = 0;
                if (monsterLook != Vector3.zero) {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(monsterLook), Time.deltaTime * 15f);
                }

                // 4. 執行鏡頭隨機抖動
                if (isShakingCamera && playerCam != null) {
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

            // 逃過一劫後，時間歸零，解鎖玩家並還原鏡頭
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

        if (!isFreezingPlayer) {
            if (isDetected) {
                ChaseAndAttack();
            } else {
                Patrol();
            }
        }
    }

    void LateUpdate() {
        // 在所有腳本都執行完位移後，強制把你拉回定身點，讓你一步都退不了
        if (isFreezingPlayer && player != null) {
            player.position = frozenPlayerPos;
        }
    }

    public void UnlockMonster() {
        isUnlocked = true;
        if (agent != null && agent.enabled && agent.isOnNavMesh) {
            agent.isStopped = false;
        }
        Debug.Log($"🚪 大門完全開啟，怪物 {gameObject.name} 正式解鎖開始行動！");
    }

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

        // 一旦到達攻擊距離，立刻定身玩家（保留手電筒操作權限）
        if (!isFreezingPlayer) {
            isFreezingPlayer = true;
            frozenPlayerPos = player.position;
            TogglePlayerController(false); 
            Debug.Log("⚠️ 進入攻擊範圍，玩家被定身！快關掉手電筒！");
        }

        UpdateAnimation(ATTACK);
    }

    public void OnAttackHit() {
        if (player == null) return;

        // 計算你跟怪物揮爪時的實際距離
        float distance = Vector3.Distance(transform.position, player.position);
        
        bool isFlashlightOn = (flashlightLight != null && flashlightLight.enabled);

        // 【雙重判定】必須「手電筒亮著」且「沒脫離命中極限距離 (hitRange)」，兩者皆成立才會被殺
        if (isFlashlightOn && distance <= hitRange) {
            
            Debug.Log("💀 揮爪時手電筒亮著且被命中！擊殺玩家！");

            UltimateFlashlightController flashlightCtrl = player.GetComponentInChildren<UltimateFlashlightController>();
            if (flashlightCtrl != null) {
                flashlightCtrl.RequestTurnOff();
            }

            detectTimer = 10f; 
            isShakingCamera = true; 

            StartCoroutine(TriggerGameOverAfterScare());
            
        } else {
            
            // 不管是因為你及時關了手電筒，還是你用什麼魔法逃出了攻擊範圍，都算你逃過一劫
            Debug.Log("😌 玩家逃過一劫！");
            detectTimer = 0f; 
            
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

    private System.Collections.IEnumerator TriggerGameOverAfterScare()
    {
        yield return new WaitForSeconds(gameOverDelay);
        PlayerPrefs.SetString("LastLevel", SceneManager.GetActiveScene().name);
        PlayerPrefs.Save();
        SceneManager.LoadScene(gameOverSceneName);
    }
}