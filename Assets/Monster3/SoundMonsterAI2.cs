
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(MonsterHearing2))]
public class SoundMonsterAI2 : MonoBehaviour
{
    public enum State
    {
        Patrol,
        Alert,
        Chase,
        Attack
    }

    [Header("目標")]
    public Transform player;

    [Header("目前狀態")]
    public State currentState = State.Patrol;

    [Header("移動速度")]
    public float patrolSpeed = 2f;
    public float alertSpeed = 3f;
    public float chaseSpeed = 6f;

    [Header("巡邏設定")]
    public float patrolRadius = 12f;
    public float waypointArriveDistance = 1f;

    [Header("追擊設定")]
    public float chaseDistance = 30f;
    public float disengageDistance = 50f;

    [Header("攻擊設定")]
    public float attackRange = 3f;
    public float attackCooldown = 1.2f;

    [Header("追擊放棄設定")]
    public float losePlayerAfterNoSound = 0.3f;

    [Tooltip("大於此強度的聲音才會刷新追擊時間")]
    public float chaseKeepIntensity = 3f;

    [Tooltip("達到此強度的聲音會觸發追擊")]
    public float chaseTriggerIntensity = 5f;

    [Header("警戒時間")]
    public float alertTimeout = 3f;

    [Header("Animator")]
    public Animator animator;

    [Header("動畫名稱")]
    public string idleAnim = "Idle";
    public string walk1Anim = "Walk1";
    public string walk2Anim = "Walk2";
    public string biteAnim = "Bite";

    [Header("動畫移動判斷")]
    public float moveAnimThreshold = 0.02f;
    public float idleDelay = 0.45f;
    public float movingDistanceBuffer = 0.2f;

    [Header("動畫速度同步")]
    public bool syncAnimationSpeed = true;

    [Tooltip("Walk1 動畫在 1 倍速時對應的移動速度")]
    public float walk1ReferenceSpeed = 2f;

    [Tooltip("Walk2 動畫在 1 倍速時對應的移動速度")]
    public float walk2ReferenceSpeed = 3f;

    [Tooltip("走路動畫最低播放倍率")]
    public float minAnimationSpeed = 0.5f;

    [Tooltip("走路動畫最高播放倍率")]
    public float maxAnimationSpeed = 2.5f;

    [Tooltip("動畫速度變化平滑程度")]
    public float animationSpeedSmooth = 10f;

    [Header("Debug")]
    public bool showDebugLog = true;

    NavMeshAgent agent;
    MonsterHearing2 hearing;
    Renderer rend;

    PlayerSoundEmitter2 playerSound;

    Vector3 lastSoundPos;
    bool hasLastSoundPos;

    float lastStrongSoundTime;
    float alertTimer;
    float attackTimer;

    Vector3 patrolTarget;
    bool hasPatrolTarget;

    string currentAnim = "";
    float idleTimer = 0f;

    float currentAnimationSpeed = 1f;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        hearing = GetComponent<MonsterHearing2>();

        rend = GetComponentInChildren<Renderer>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }
    }

    void Start()
    {
        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.speed = 1f;
        }

        if (player != null)
        {
            playerSound =
                player.GetComponent<PlayerSoundEmitter2>();

            if (playerSound == null)
            {
                playerSound =
                    player.GetComponentInChildren<PlayerSoundEmitter2>();
            }
        }

        if (agent != null)
        {
            agent.stoppingDistance = attackRange;
        }

        EnterPatrol();
    }

    void Update()
    {
        bool heard = hearing.HasHeardSound(
            out float intensity,
            out Vector3 soundPos
        );

        if (heard)
        {
            lastSoundPos = soundPos;
            hasLastSoundPos = true;

            if (intensity >= chaseKeepIntensity)
            {
                lastStrongSoundTime = Time.time;
            }

            if (showDebugLog)
            {
                Debug.Log(
                    $"[MONSTER] Heard sound {intensity:F1}"
                );
            }

            HandleSoundInput(intensity, soundPos);
        }

        switch (currentState)
        {
            case State.Patrol:
                UpdatePatrol();
                break;

            case State.Alert:
                UpdateAlert(heard);
                break;

            case State.Chase:
                UpdateChase();
                break;

            case State.Attack:
                UpdateAttack();
                break;
        }

        // 每幀同步走路動畫速度
        UpdateAnimationSpeed();
    }

    void HandleSoundInput(float intensity, Vector3 soundPos)
    {
        float distToSound = Vector3.Distance(
            transform.position,
            soundPos
        );

        if (currentState == State.Patrol)
        {
            if (ShouldChase(intensity, distToSound))
            {
                EnterChase();
            }
            else
            {
                EnterAlert(soundPos);
            }
        }
        else if (currentState == State.Alert)
        {
            if (ShouldChase(intensity, distToSound))
            {
                EnterChase();
            }
            else
            {
                agent.SetDestination(soundPos);
            }
        }
        else if (currentState == State.Chase)
        {
            if (player != null)
            {
                agent.SetDestination(player.position);
            }
        }
    }

    bool ShouldChase(float intensity, float distToSound)
    {
        if (intensity >= chaseTriggerIntensity)
        {
            return true;
        }

        if (distToSound <= chaseDistance &&
            intensity >= chaseKeepIntensity)
        {
            return true;
        }

        return false;
    }

    void UpdatePatrol()
    {
        agent.speed = patrolSpeed;
        agent.stoppingDistance = 0f;

        if (!hasPatrolTarget ||
            (!agent.pathPending &&
             agent.remainingDistance <= waypointArriveDistance))
        {
            SetRandomPatrolPoint();
        }

        UpdateMoveAnimation();
    }

    void UpdateAlert(bool heardThisFrame)
    {
        agent.speed = alertSpeed;
        agent.stoppingDistance = 0f;

        if (heardThisFrame)
        {
            alertTimer = 0f;
        }
        else
        {
            alertTimer += Time.deltaTime;

            if (alertTimer >= alertTimeout)
            {
                EnterPatrol();
                return;
            }
        }

        if (hasLastSoundPos &&
            !agent.pathPending &&
            agent.remainingDistance <= waypointArriveDistance)
        {
            agent.ResetPath();
        }

        UpdateMoveAnimation();
    }

    void UpdateChase()
    {
        agent.speed = chaseSpeed;
        agent.stoppingDistance = attackRange;

        // 玩家停止移動時立即放棄追擊
        if (playerSound != null &&
            !playerSound.IsMovingInput)
        {
            GiveUpChase();
            return;
        }

        // 太久沒有強聲音也放棄追擊
        if (Time.time - lastStrongSoundTime >=
            losePlayerAfterNoSound)
        {
            GiveUpChase();
            return;
        }

        if (player == null)
        {
            GiveUpChase();
            return;
        }

        float distToPlayer = Vector3.Distance(
            transform.position,
            player.position
        );

        if (distToPlayer > disengageDistance)
        {
            GiveUpChase();
            return;
        }

        if (distToPlayer <= attackRange)
        {
            EnterAttack();
            return;
        }

        agent.SetDestination(player.position);

        FacePlayerSoft();

        UpdateMoveAnimation();
    }

    void UpdateAttack()
    {
        agent.ResetPath();
        agent.velocity = Vector3.zero;

        FacePlayer();

        // 玩家停止移動就放棄攻擊
        if (playerSound != null &&
            !playerSound.IsMovingInput)
        {
            GiveUpChase();
            return;
        }

        attackTimer += Time.deltaTime;

        if (Time.time - lastStrongSoundTime >=
            losePlayerAfterNoSound)
        {
            GiveUpChase();
            return;
        }

        if (player == null)
        {
            GiveUpChase();
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

        if (attackTimer >= attackCooldown)
        {
            attackTimer = 0f;

            PlayAnim(biteAnim, true);
        }
    }

    void GiveUpChase()
    {
        // 放棄追擊後立即回到白色
        EnterPatrol();

        if (showDebugLog)
        {
            Debug.Log(
                "[MONSTER] Give up chase, return to white."
            );
        }
    }

    void EnterPatrol()
    {
        currentState = State.Patrol;

        alertTimer = 0f;
        hasPatrolTarget = false;
        idleTimer = 0f;

        agent.speed = patrolSpeed;
        agent.stoppingDistance = 0f;

        SetRandomPatrolPoint();

        PlayAnim(idleAnim);

        SetMonsterColor(Color.white);

        if (showDebugLog)
        {
            Debug.Log("[MONSTER] State = Patrol");
        }
    }

    void EnterAlert(Vector3 target)
    {
        currentState = State.Alert;

        alertTimer = 0f;
        idleTimer = 0f;

        agent.speed = alertSpeed;
        agent.stoppingDistance = 0f;

        if (hasLastSoundPos)
        {
            agent.SetDestination(target);
        }

        PlayAnim(idleAnim);

        SetMonsterColor(
            new Color(1f, 0.5f, 0f)
        );

        if (showDebugLog)
        {
            Debug.Log("[MONSTER] State = Alert");
        }
    }

    void EnterChase()
    {
        currentState = State.Chase;

        idleTimer = 0f;
        lastStrongSoundTime = Time.time;

        agent.speed = chaseSpeed;
        agent.stoppingDistance = attackRange;

        if (player != null)
        {
            agent.SetDestination(player.position);
        }
        else if (hasLastSoundPos)
        {
            agent.SetDestination(lastSoundPos);
        }

        PlayAnim(walk2Anim);

        SetMonsterColor(Color.red);

        if (showDebugLog)
        {
            Debug.Log("[MONSTER] State = Chase");
        }
    }

    void EnterAttack()
    {
        currentState = State.Attack;

        agent.ResetPath();
        agent.velocity = Vector3.zero;

        attackTimer = 0f;
        idleTimer = 0f;

        FacePlayer();

        PlayAnim(biteAnim, true);

        SetMonsterColor(
            new Color(0.4f, 0f, 0f)
        );

        if (showDebugLog)
        {
            Debug.Log("[MONSTER] State = Attack");
        }
    }

    void SetRandomPatrolPoint()
    {
        for (int i = 0; i < 10; i++)
        {
            Vector3 randomDir =
                Random.insideUnitSphere * patrolRadius;

            randomDir += transform.position;
            randomDir.y = transform.position.y;

            if (NavMesh.SamplePosition(
                randomDir,
                out NavMeshHit hit,
                patrolRadius,
                NavMesh.AllAreas))
            {
                patrolTarget = hit.position;
                hasPatrolTarget = true;

                agent.SetDestination(patrolTarget);
                return;
            }
        }

        hasPatrolTarget = false;
        agent.ResetPath();
    }

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

        bool isCalculatingPath = agent.pathPending;

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

            if (currentState == State.Patrol)
            {
                PlayAnim(walk1Anim);
            }
            else
            {
                PlayAnim(walk2Anim);
            }

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
            (currentAnim == walk1Anim ||
             currentAnim == walk2Anim))
        {
            // 使用實際移動速度，而不是 Inspector 設定速度
            float actualSpeed =
                agent.velocity.magnitude;

            float referenceSpeed =
                currentAnim == walk1Anim
                ? walk1ReferenceSpeed
                : walk2ReferenceSpeed;

            referenceSpeed = Mathf.Max(
                0.01f,
                referenceSpeed
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

    void PlayAnim(string animName, bool forceRestart = false)
    {
        if (animator == null)
            return;

        if (string.IsNullOrEmpty(animName))
            return;

        if (!forceRestart && currentAnim == animName)
            return;

        currentAnim = animName;

        // 非走路動畫立即恢復正常播放速度
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
                "[ANIM] Play " + animName
            );
        }
    }

    void SetMonsterColor(Color color)
    {
        if (rend != null)
        {
            rend.material.color = color;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(
            transform.position,
            patrolRadius
        );

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(
            transform.position,
            chaseDistance
        );

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(
            transform.position,
            disengageDistance
        );

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(
            transform.position,
            attackRange
        );

        if (hasLastSoundPos)
        {
            Gizmos.color = Color.magenta;

            Gizmos.DrawSphere(
                lastSoundPos,
                0.4f
            );

            Gizmos.DrawLine(
                transform.position,
                lastSoundPos
            );
        }
    }
}
