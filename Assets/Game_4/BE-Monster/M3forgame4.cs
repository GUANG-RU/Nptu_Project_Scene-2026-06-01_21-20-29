
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(NavMeshAgent))]
public class M3forgame4 : MonoBehaviour
{
    public enum State
    {
        Chase = 2,
        Attack = 3
    }

    [Header("目標")]
    public Transform player;

    [Header("目前狀態")]
    public State currentState = State.Chase;

    [Header("移動速度")]
    public float chaseSpeed = 6f;

    [Header("攻擊設定")]
    public float attackRange = 3f;
    public float attackCooldown = 1.2f;

    [Header("Animator")]
    public Animator animator;

    [Header("動畫名稱")]
    public string idleAnim = "Idle";
    public string walk2Anim = "Walk2";
    public string biteAnim = "Bite";

    [Header("動畫移動判斷")]
    public float moveAnimThreshold = 0.02f;
    public float idleDelay = 0.45f;
    public float movingDistanceBuffer = 0.2f;

    [Header("動畫速度同步")]
    public bool syncAnimationSpeed = true;

    [Tooltip("Walk2 動畫在 1 倍速時對應的移動速度")]
    public float walk2ReferenceSpeed = 3f;

    public float minAnimationSpeed = 0.5f;
    public float maxAnimationSpeed = 2.5f;
    public float animationSpeedSmooth = 10f;


    // ==================================
    // 玩家與重影效果設定
    // ==================================

    [Header("玩家設定")]
    public Camera playerCamera;

    [Tooltip("玩家真正的移動腳本，例如 FirstPersonController")]
    public MonoBehaviour playerMovementScript;

    [Header("重影攻擊效果")]
    public float effectDuration = 2.5f;
    public float freezeDuration = 2.5f;
    public float effectCooldown = 5f;

    [Header("Game Claer")]
    public string gameOverSceneName = "GameClear";

    [Tooltip("重影效果結束後額外等待的秒數")]
    public float extraGameOverDelay = 0f;

    [Header("Debug")]
    public bool showDebugLog = true;

    private NavMeshAgent agent;
    private Renderer rend;

    private GhostVisionEffect2 ghostEffect;
    private PlayerFreezeController2 freezeController;

    private float attackTimer;
    private float idleTimer = 0f;

    private string currentAnim = "";
    private float currentAnimationSpeed = 1f;

    // 防止重複觸發死亡
    private bool isGameOverSequence = false;
    private bool hasLoadedGameOver = false;


    // ==================================
    // 初始化元件
    // ==================================

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        rend = GetComponentInChildren<Renderer>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }


    // ==================================
    // 遊戲開始立即追擊
    // ==================================

    void Start()
    {
        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.speed = 1f;
        }

        InitGhostEffect();
        InitFreezeController();

        EnterChase();

        if (player == null)
        {
            Debug.LogWarning(
                "[M3forgame4] 尚未指定 Player！"
            );
        }
    }


    // ==================================
    // 初始化視覺重影效果
    // 沿用原本 GhostVisionEffect2
    // ==================================

    void InitGhostEffect()
    {
        if (playerCamera == null && player != null)
        {
            playerCamera =
                player.GetComponentInChildren<Camera>();
        }

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (playerCamera == null)
        {
            Debug.LogWarning(
                "[M3forgame4] 找不到玩家攝影機！"
            );

            return;
        }

        ghostEffect =
            playerCamera.GetComponent<GhostVisionEffect2>();

        if (ghostEffect == null)
        {
            ghostEffect =
                playerCamera.gameObject
                    .AddComponent<GhostVisionEffect2>();
        }
    }


    // ==================================
    // 初始化玩家定身管理器
    // 沿用原本 PlayerFreezeController2
    // ==================================

    void InitFreezeController()
    {
        if (player == null)
        {
            return;
        }

        GameObject playerObject = player.gameObject;

        freezeController =
            playerObject.GetComponent<PlayerFreezeController2>();

        if (freezeController == null)
        {
            freezeController =
                playerObject.AddComponent<PlayerFreezeController2>();
        }

        if (playerMovementScript != null)
        {
            freezeController.playerMovementScript =
                playerMovementScript;
        }
        else
        {
            playerMovementScript =
                freezeController.playerMovementScript;
        }

        if (playerMovementScript == null)
        {
            Debug.LogError(
                "[M3forgame4] 請指定 Player Movement Script！"
            );
        }
    }


    // ==================================
    // 持續追擊及攻擊玩家
    // ==================================

    void Update()
    {
        // 死亡演出期間停止 AI 行為
        if (isGameOverSequence)
        {
            StopMonster();

            FacePlayer();

            return;
        }

        if (agent == null ||
            !agent.enabled ||
            !agent.isOnNavMesh)
        {
            return;
        }

        if (player == null)
        {
            StopMonster();

            PlayAnim(idleAnim);

            return;
        }

        switch (currentState)
        {
            case State.Chase:

                UpdateChase();

                break;

            case State.Attack:

                UpdateAttack();

                break;
        }

        UpdateAnimationSpeed();
    }


    // ==================================
    // 死亡演出期間維持玩家定身
    // 避免其他定身計時器提前恢復操作
    // ==================================

    void LateUpdate()
    {
        if (!isGameOverSequence)
        {
            return;
        }

        if (playerMovementScript != null)
        {
            playerMovementScript.enabled = false;
        }
    }


    // ==================================
    // 無條件追擊玩家
    // ==================================

    void UpdateChase()
    {
        agent.speed = chaseSpeed;

        agent.stoppingDistance =
            Mathf.Max(0f, attackRange * 0.7f);

        float distToPlayer = Vector3.Distance(
            transform.position,
            player.position
        );

        if (distToPlayer <= attackRange)
        {
            EnterAttack();

            return;
        }

        agent.isStopped = false;

        // 不判斷聲音、手電筒或玩家移動狀態
        agent.SetDestination(player.position);

        FacePlayerSoft();

        UpdateMoveAnimation();
    }


    // ==================================
    // 攻擊狀態更新
    // ==================================

    void UpdateAttack()
    {
        StopMonster();

        FacePlayer();

        if (isGameOverSequence)
        {
            return;
        }

        float distToPlayer = Vector3.Distance(
            transform.position,
            player.position
        );

        if (distToPlayer > attackRange)
        {
            EnterChase();

            return;
        }

        attackTimer += Time.deltaTime;

        if (attackTimer >= attackCooldown)
        {
            attackTimer = 0f;

            PlayAnim(biteAnim, true);

            TriggerGhostAttack();
        }
    }


    // ==================================
    // 進入追擊狀態
    // ==================================

    void EnterChase()
    {
        currentState = State.Chase;

        idleTimer = 0f;

        if (agent != null &&
            agent.enabled &&
            agent.isOnNavMesh)
        {
            agent.isStopped = false;

            agent.speed = chaseSpeed;

            agent.stoppingDistance =
                Mathf.Max(0f, attackRange * 0.7f);

            if (player != null)
            {
                agent.SetDestination(player.position);
            }
        }

        if (player != null)
        {
            PlayAnim(walk2Anim);
        }
        else
        {
            PlayAnim(idleAnim);
        }

        SetMonsterColor(Color.red);

        if (showDebugLog)
        {
            Debug.Log(
                "[M3forgame4] State = Chase"
            );
        }
    }


    // ==================================
    // 進入攻擊狀態
    // ==================================

    void EnterAttack()
    {
        if (isGameOverSequence)
        {
            return;
        }

        currentState = State.Attack;

        StopMonster();

        attackTimer = 0f;
        idleTimer = 0f;

        FacePlayer();

        // 播放原本 Bite 攻擊動畫
        PlayAnim(biteAnim, true);

        // 保留原本攻擊時的深紅色
        SetMonsterColor(
            new Color(0.4f, 0f, 0f)
        );

        if (showDebugLog)
        {
            Debug.Log(
                "[M3forgame4] State = Attack"
            );
        }

        // 直接觸發原本的視覺重影效果
        TriggerGhostAttack();
    }


    // ==================================
    // 重影攻擊與玩家定身
    // ==================================

    void TriggerGhostAttack()
    {
        if (isGameOverSequence)
        {
            return;
        }

        if (player == null)
        {
            return;
        }

        // 確認 GameOver 場景存在
        if (!Application.CanStreamedLevelBeLoaded(
            gameOverSceneName))
        {
            Debug.LogError(
                "[M3forgame4] 找不到 GameOver 場景：" +
                gameOverSceneName
            );

            return;
        }

        // 鎖定死亡流程，避免重複攻擊
        isGameOverSequence = true;

        StopMonster();

        float ghostTime =
            Mathf.Max(0.01f, effectDuration);

        float extraDelay =
            Mathf.Max(0f, extraGameOverDelay);

        // 確保定身時間涵蓋整個死亡演出
        float finalFreezeDuration = Mathf.Max(
            freezeDuration,
            ghostTime + extraDelay + 0.1f
        );

        // 使用原本的定身管理器
        if (freezeController != null)
        {
            bool success = freezeController.TryFreeze(
                finalFreezeDuration,
                effectCooldown
            );

            if (!success && showDebugLog)
            {
                Debug.Log(
                    "[M3forgame4] 玩家可能已定身或處於冷卻中，" +
                    "仍進行死亡演出。"
                );
            }
        }

        // Game4 的攻擊必定觸發死亡
        // 不受原本定身冷卻時間影響
        if (playerMovementScript != null)
        {
            playerMovementScript.enabled = false;
        }

        // 沿用原本的視覺重影效果
        if (ghostEffect != null)
        {
            ghostEffect.PlayEffect(ghostTime);
        }
        else
        {
            Debug.LogWarning(
                "[M3forgame4] 找不到 GhostVisionEffect2！"
            );
        }

        if (showDebugLog)
        {
            Debug.Log(
                "[M3forgame4] Bite! " +
                $"重影 {ghostTime:F1} 秒，" +
                $"定身 {finalFreezeDuration:F1} 秒。"
            );
        }

        // 等待重影播放完再進入 GameOver
        StartCoroutine(
            GameOverSequence(ghostTime + extraDelay)
        );
    }


    // ==================================
    // 重影效果結束後進入 GameOver
    // ==================================

    IEnumerator GameOverSequence(float delay)
    {
        yield return new WaitForSeconds(delay);

        LoadGameOver();
    }


    // ==================================
    // 切換 GameOver 場景
    // ==================================

    void LoadGameOver()
    {
        if (hasLoadedGameOver)
        {
            return;
        }

        hasLoadedGameOver = true;

        string currentScene =
            SceneManager.GetActiveScene().name;

        PlayerPrefs.SetString(
            "LastLevel",
            currentScene
        );

        PlayerPrefs.Save();

        Time.timeScale = 1f;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SceneManager.LoadScene(gameOverSceneName);
    }


    // ==================================
    // 停止怪物移動
    // ==================================

    void StopMonster()
    {
        if (agent == null ||
            !agent.enabled ||
            !agent.isOnNavMesh)
        {
            return;
        }

        agent.isStopped = true;

        agent.ResetPath();

        agent.velocity = Vector3.zero;
    }


    // ==================================
    // 移動動畫判斷
    // ==================================

    void UpdateMoveAnimation()
    {
        if (animator == null)
            return;

        if (currentState == State.Attack)
            return;

        bool hasValidPath =
            agent.hasPath &&
            !agent.pathPending &&
            agent.remainingDistance >
            agent.stoppingDistance + movingDistanceBuffer;

        bool isCalculatingPath =
            agent.pathPending;

        bool hasVelocity =
            agent.velocity.magnitude > moveAnimThreshold ||
            agent.desiredVelocity.magnitude > moveAnimThreshold;

        bool shouldPlayWalk =
            hasValidPath ||
            isCalculatingPath ||
            hasVelocity;

        if (shouldPlayWalk)
        {
            idleTimer = 0f;

            PlayAnim(walk2Anim);

            return;
        }

        idleTimer += Time.deltaTime;

        if (idleTimer >= idleDelay)
        {
            PlayAnim(idleAnim);
        }
    }


    // ==================================
    // 動畫播放速度同步
    // ==================================

    void UpdateAnimationSpeed()
    {
        if (animator == null)
            return;

        float targetSpeed = 1f;

        if (syncAnimationSpeed &&
            currentState != State.Attack &&
            currentAnim == walk2Anim)
        {
            float actualSpeed =
                agent.velocity.magnitude;

            float referenceSpeed = Mathf.Max(
                0.01f,
                walk2ReferenceSpeed
            );

            targetSpeed =
                actualSpeed / referenceSpeed;

            targetSpeed = Mathf.Clamp(
                targetSpeed,
                minAnimationSpeed,
                maxAnimationSpeed
            );
        }

        currentAnimationSpeed = Mathf.Lerp(
            currentAnimationSpeed,
            targetSpeed,
            Time.deltaTime * animationSpeedSmooth
        );

        animator.speed = currentAnimationSpeed;
    }


    // ==================================
    // 立即面向玩家
    // ==================================

    void FacePlayer()
    {
        if (player == null)
            return;

        Vector3 dir =
            player.position - transform.position;

        dir.y = 0f;

        if (dir.sqrMagnitude <= 0.001f)
            return;

        transform.rotation =
            Quaternion.LookRotation(dir);
    }


    // ==================================
    // 平滑面向玩家
    // ==================================

    void FacePlayerSoft()
    {
        if (player == null)
            return;

        Vector3 dir =
            player.position - transform.position;

        dir.y = 0f;

        if (dir.sqrMagnitude <= 0.001f)
            return;

        Quaternion lookRotation =
            Quaternion.LookRotation(dir);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            lookRotation,
            Time.deltaTime * 5f
        );
    }


    // ==================================
    // 播放動畫
    // ==================================

    void PlayAnim(
        string animName,
        bool forceRestart = false
    )
    {
        if (animator == null)
            return;

        if (string.IsNullOrEmpty(animName))
            return;

        if (!forceRestart && currentAnim == animName)
            return;

        currentAnim = animName;

        if (animName == idleAnim ||
            animName == biteAnim)
        {
            currentAnimationSpeed = 1f;

            animator.speed = 1f;
        }

        animator.CrossFade(
            animName,
            0.15f,
            0,
            0f
        );

        if (showDebugLog)
        {
            Debug.Log(
                "[M3forgame4 ANIM] Play " + animName
            );
        }
    }


    // ==================================
    // 怪物顏色設定
    // ==================================

    void SetMonsterColor(Color color)
    {
        if (rend != null)
        {
            rend.material.color = color;
        }
    }


    // ==================================
    // 手動測試重影攻擊
    // 請在 Play Mode 中使用
    // ==================================

    [ContextMenu("Test Ghost Attack")]
    public void TestGhostAttack()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (player == null)
        {
            return;
        }

        EnterAttack();
    }


    // ==================================
    // 攻擊範圍輔助顯示
    // ==================================

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;

        Gizmos.DrawWireSphere(
            transform.position,
            attackRange
        );
    }
}
