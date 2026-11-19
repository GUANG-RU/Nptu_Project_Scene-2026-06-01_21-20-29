using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public class M2forgame4 : MonoBehaviour
{
    private Animator anim;
    private NavMeshAgent agent;

    // ==========================================
    // 大門解鎖機制
    // ==========================================
    [Header("門解鎖機制設定")]

    [Tooltip("勾選後等待大門開啟，未勾選則遊戲開始立即追擊")]
    public bool useDoorUnlockMechanism = false;

    private bool isUnlocked = false;

    [Header("目標設定")]
    public Transform player;

    // ==========================================
    // 移動與攻擊設定
    // ==========================================
    [Header("移動數值")]
    public float runSpeed = 5.5f;
    public float attackRange = 2f;

    [Header("攻擊命中設定")]
    public float hitRange = 2.5f;

    // ==========================================
    // 玩家定身設定
    // ==========================================
    private bool isFreezingPlayer = false;
    private Vector3 frozenPlayerPos;

    private bool isShakingCamera = false;
    private bool isGameOverSequence = false;

    [Header("視角對準設定")]
    public float lookAtSpeed = 12f;

    // ==========================================
    // 鏡頭抖動設定
    // ==========================================
    [Header("玩家受撞擊抖動設定")]
    public float shakeIntensity = 0.15f;

    private Vector3 originalCamLocalPos;
    private bool hasSavedCamPos = false;

    // ==========================================
    // Game Over 設定
    // ==========================================
    [Header("Game Over 設定")]

    [Tooltip("攻擊命中後多久進入 GameOver")]
    public float gameOverDelay = 2f;

    public string gameOverSceneName = "GameOver";

    // ==========================================
    // 音效設定
    // ==========================================
    [Header("音效設定")]
    public AudioSource audioSource;

    public AudioClip spottedSound;
    public AudioClip chaseSound;
    public AudioClip attackSound;

    private int lastState = -1;
    private bool hasPlayedSpotted = false;

    // 保留原本 Animator 的狀態數值
    private const int IDLE = 0;
    private const int WALK = 1;
    private const int RUN = 2;
    private const int ATTACK = 3;


    // ==========================================
    // 初始化
    // ==========================================
    void Start()
    {
        anim = GetComponent<Animator>();

        if (anim == null)
        {
            anim = GetComponentInChildren<Animator>();
        }

        agent = GetComponent<NavMeshAgent>();

        if (anim != null)
        {
            anim.cullingMode =
                AnimatorCullingMode.AlwaysAnimate;
        }

        if (agent != null)
        {
            agent.updateRotation = true;

            agent.stoppingDistance = Mathf.Min(
                agent.stoppingDistance,
                Mathf.Max(0f, attackRange * 0.7f)
            );
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (!useDoorUnlockMechanism)
        {
            isUnlocked = true;
        }
        else
        {
            isUnlocked = false;

            StopMonster();

            UpdateAnimation(IDLE);
        }
    }


    // ==========================================
    // 無條件追擊玩家
    // ==========================================
    void Update()
    {
        if (player == null)
        {
            return;
        }

        // 攻擊期間不再進行追擊
        if (isFreezingPlayer)
        {
            UpdateFreezeEffect();
            return;
        }

        if (agent == null ||
            !agent.enabled ||
            !agent.isOnNavMesh)
        {
            return;
        }

        // 選擇性保留大門解鎖機制
        if (!isUnlocked)
        {
            StopMonster();

            UpdateAnimation(IDLE);

            return;
        }

        // 不再檢查手電筒
        // 不再檢查偵測角度
        // 不再檢查偵測距離
        // 不再檢查障礙物

        ChaseAndAttack();
    }


    // ==========================================
    // 強制固定玩家的位置
    // ==========================================
    void LateUpdate()
    {
        if (isFreezingPlayer && player != null)
        {
            player.position = frozenPlayerPos;
        }
    }


    // ==========================================
    // 大門開啟後解鎖怪物
    // ==========================================
    public void UnlockMonster()
    {
        isUnlocked = true;

        if (!isFreezingPlayer &&
            agent != null &&
            agent.enabled &&
            agent.isOnNavMesh)
        {
            agent.isStopped = false;
        }

        Debug.Log(
            "[M2forgame4] 怪物正式解鎖開始追擊！"
        );
    }


    // ==========================================
    // 持續追擊與攻擊
    // ==========================================
    void ChaseAndAttack()
    {
        float distance = Vector3.Distance(
            transform.position,
            player.position
        );

        if (distance > attackRange)
        {
            agent.isStopped = false;

            agent.speed = runSpeed;

            agent.SetDestination(player.position);

            // 直接播放原本的奔跑動畫
            UpdateAnimation(RUN);
        }
        else
        {
            Attack();
        }
    }


    // ==========================================
    // 攻擊玩家
    // ==========================================
    void Attack()
    {
        if (isFreezingPlayer ||
            isGameOverSequence)
        {
            return;
        }

        StopMonster();

        // 讓怪物面向玩家
        Vector3 targetDir =
            player.position - transform.position;

        targetDir.y = 0f;

        if (targetDir.sqrMagnitude > 0.001f)
        {
            transform.rotation =
                Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(targetDir),
                    Time.deltaTime * 10f
                );
        }

        // 固定玩家的位置
        isFreezingPlayer = true;

        frozenPlayerPos = player.position;

        // 禁止玩家移動及轉動視角
        // 仍保留原本手電筒操作功能
        TogglePlayerController(false);

        // 播放原本的攻擊動畫
        UpdateAnimation(ATTACK);

        Debug.Log(
            "[M2forgame4] 進入攻擊範圍，玩家被定身！"
        );
    }


    // ==========================================
    // 定身、強制轉向與鏡頭抖動
    // ==========================================
    void UpdateFreezeEffect()
    {
        if (player == null)
        {
            return;
        }

        // 固定玩家位置
        player.position = frozenPlayerPos;

        Camera playerCam =
            player.GetComponentInChildren<Camera>();

        // 強制玩家看向怪物
        Vector3 lookDir =
            (transform.position + Vector3.up * 1.2f)
            - player.position;

        if (lookDir.sqrMagnitude > 0.001f)
        {
            Vector3 lookDirYaw = lookDir;

            lookDirYaw.y = 0f;

            if (lookDirYaw.sqrMagnitude > 0.001f)
            {
                player.rotation =
                    Quaternion.Slerp(
                        player.rotation,
                        Quaternion.LookRotation(lookDirYaw),
                        Time.deltaTime * lookAtSpeed
                    );
            }

            if (playerCam != null)
            {
                Quaternion targetCamRot =
                    Quaternion.LookRotation(lookDir);

                playerCam.transform.rotation =
                    Quaternion.Slerp(
                        playerCam.transform.rotation,
                        targetCamRot,
                        Time.deltaTime * lookAtSpeed
                    );
            }
        }

        // 怪物停止移動，但不中斷攻擊動畫
        StopMonster();

        // 讓怪物持續面向玩家
        Vector3 monsterLook =
            player.position - transform.position;

        monsterLook.y = 0f;

        if (monsterLook.sqrMagnitude > 0.001f)
        {
            transform.rotation =
                Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(monsterLook),
                    Time.deltaTime * 15f
                );
        }

        // 攻擊命中後執行鏡頭抖動
        if (isShakingCamera && playerCam != null)
        {
            if (!hasSavedCamPos)
            {
                originalCamLocalPos =
                    playerCam.transform.localPosition;

                hasSavedCamPos = true;
            }

            float randomX =
                Random.Range(-1f, 1f) * shakeIntensity;

            float randomY =
                Random.Range(-1f, 1f) * shakeIntensity;

            float randomZ =
                Random.Range(-1f, 1f) *
                (shakeIntensity * 0.3f);

            playerCam.transform.localPosition =
                originalCamLocalPos +
                new Vector3(
                    randomX,
                    randomY,
                    randomZ
                );
        }
    }


    // ==========================================
    // 攻擊動畫命中事件
    // ==========================================
    public void OnAttackHit()
    {
        if (player == null ||
            !isFreezingPlayer ||
            isGameOverSequence)
        {
            return;
        }

        float distance = Vector3.Distance(
            transform.position,
            player.position
        );

        // 不再判斷手電筒是否開啟
        // 只保留原本的實際命中距離
        if (distance <= hitRange)
        {
            isGameOverSequence = true;

            Debug.Log(
                "[M2forgame4] 攻擊命中玩家！"
            );

            // 保留原本攻擊後關閉手電筒效果
            UltimateFlashlightController flashlightCtrl =
                player.GetComponentInChildren
                <UltimateFlashlightController>();

            if (flashlightCtrl != null)
            {
                flashlightCtrl.RequestTurnOff();
            }

            // 開始鏡頭抖動
            isShakingCamera = true;

            // 等待恐怖演出結束後進入 GameOver
            StartCoroutine(
                TriggerGameOverAfterScare()
            );
        }
        else
        {
            // 超出實際命中距離時解除定身
            ReleasePlayer();

            UpdateAnimation(IDLE);

            Debug.Log(
                "[M2forgame4] 攻擊未命中，繼續追擊！"
            );
        }
    }


    // ==========================================
    // 停止怪物移動
    // ==========================================
    void StopMonster()
    {
        if (agent != null &&
            agent.enabled &&
            agent.isOnNavMesh)
        {
            agent.isStopped = true;

            agent.velocity = Vector3.zero;
        }
    }


    // ==========================================
    // 解除玩家定身
    // ==========================================
    void ReleasePlayer()
    {
        isFreezingPlayer = false;

        isShakingCamera = false;

        if (player != null && hasSavedCamPos)
        {
            Camera playerCam =
                player.GetComponentInChildren<Camera>();

            if (playerCam != null)
            {
                playerCam.transform.localPosition =
                    originalCamLocalPos;
            }
        }

        hasSavedCamPos = false;

        TogglePlayerController(true);
    }


    // ==========================================
    // 控制玩家移動與視角操作
    // 保留原本的控制方式
    // ==========================================
    void TogglePlayerController(bool enable)
    {
        if (player == null)
        {
            return;
        }

        CharacterController cc =
            player.GetComponent<CharacterController>();

        if (cc != null)
        {
            cc.enabled = enable;
        }

        MonoBehaviour[] scripts =
            player.GetComponents<MonoBehaviour>();

        foreach (MonoBehaviour script in scripts)
        {
            if (script == null)
            {
                continue;
            }

            string scriptName =
                script.GetType().Name.ToLower();

            if (scriptName.Contains("firstperson") ||
                scriptName.Contains("playercontroller") ||
                scriptName.Contains("mouselook"))
            {
                script.enabled = enable;
            }
        }

        MonoBehaviour[] camScripts =
            player.GetComponentsInChildren<MonoBehaviour>();

        foreach (MonoBehaviour script in camScripts)
        {
            if (script == null)
            {
                continue;
            }

            string scriptName =
                script.GetType().Name.ToLower();

            if (scriptName.Contains("mouselook") ||
                scriptName.Contains("rotation") ||
                scriptName.Contains("camera"))
            {
                if (!scriptName.Contains("flashlight") &&
                    !scriptName.Contains("monster"))
                {
                    script.enabled = enable;
                }
            }
        }
    }


    // ==========================================
    // 動畫控制
    // 完全保留原本的 Animator Parameters
    // ==========================================
    void UpdateAnimation(int stateValue)
    {
        if (stateValue == lastState)
        {
            return;
        }

        lastState = stateValue;

        HandleSound(stateValue);

        if (anim == null)
        {
            return;
        }

        anim.SetInteger(
            "state",
            stateValue
        );

        anim.SetInteger(
            "moving",
            stateValue == WALK ? 1 : 0
        );

        anim.SetInteger(
            "run",
            stateValue == RUN ? 1 : 0
        );

        anim.SetInteger(
            "attack",
            stateValue == ATTACK ? 1 : 0
        );
    }


    // ==========================================
    // 音效控制
    // ==========================================
    void HandleSound(int state)
    {
        if (audioSource == null)
        {
            return;
        }

        switch (state)
        {
            case RUN:

                audioSource.clip = chaseSound;

                audioSource.loop = true;

                if (chaseSound != null)
                {
                    audioSource.Play();
                }

                // 第一次追擊時播放發現玩家音效
                if (!hasPlayedSpotted &&
                    spottedSound != null)
                {
                    audioSource.PlayOneShot(
                        spottedSound
                    );

                    hasPlayedSpotted = true;
                }

                break;

            case ATTACK:

                // 保留原本攻擊音效
                if (attackSound != null)
                {
                    audioSource.PlayOneShot(
                        attackSound
                    );
                }

                break;

            case IDLE:

                audioSource.Stop();

                break;
        }
    }


    // ==========================================
    // 攻擊命中後進入 GameOver
    // ==========================================
    IEnumerator TriggerGameOverAfterScare()
    {
        yield return new WaitForSeconds(
            gameOverDelay
        );

        PlayerPrefs.SetString(
            "LastLevel",
            SceneManager.GetActiveScene().name
        );

        PlayerPrefs.Save();

        SceneManager.LoadScene(
            gameOverSceneName
        );
    }


    // ==========================================
    // Scene 視窗顯示攻擊範圍
    // ==========================================
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        Gizmos.DrawWireSphere(
            transform.position,
            attackRange
        );

        Gizmos.color = Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            hitRange
        );
    }
}