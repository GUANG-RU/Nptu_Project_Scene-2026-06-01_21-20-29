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

    [Header("狀態")]
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

    [Header("攻擊 / 停止距離")]
    public float attackRange = 1.5f;
    public float attackCooldown = 1.2f;

    [Header("追擊放棄設定")]
    public float losePlayerAfterNoSound = 1f;

    [Tooltip("只有大於這個強度的聲音，才會刷新追擊時間。建議比 Breath Intensity 大。")]
    public float chaseKeepIntensity = 3f;

    [Tooltip("大於這個強度時，怪物會直接進入追擊。")]
    public float chaseTriggerIntensity = 5f;

    [Header("警戒逾時")]
    public float alertTimeout = 3f;

    [Header("動畫狀態名稱")]
    public string idleAnim = "Idle";
    public string walk1Anim = "Walk1";
    public string walk2Anim = "Walk2";
    public string biteAnim = "Bite";

    [Header("動畫切換設定")]
    [Tooltip("速度大於這個值才算有移動")]
    public float moveAnimThreshold = 0.02f;

    [Tooltip("停住多久後才切回 Idle，避免走路時瞬間跳 Idle")]
    public float idleDelay = 0.45f;

    [Tooltip("只要還離目的地超過這個距離，就視為正在移動，避免遠距離追擊時平移")]
    public float movingDistanceBuffer = 0.2f;

    NavMeshAgent agent;
    MonsterHearing2 hearing;
    Renderer rend;
    Animator animator;

    Vector3 lastSoundPos;
    bool hasLastSoundPos;

    float lastStrongSoundTime;
    float alertTimer;
    float attackTimer;

    Vector3 patrolTarget;
    bool hasPatrolTarget;

    string currentAnim = "";
    float idleTimer = 0f;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        hearing = GetComponent<MonsterHearing2>();
        rend = GetComponentInChildren<Renderer>();
        animator = GetComponentInChildren<Animator>();
    }

    void Start()
    {
        if (agent != null)
        {
            agent.stoppingDistance = attackRange;
        }

        EnterPatrol();
    }

    void Update()
    {
        bool heard = hearing.HasHeardSound(out float intensity, out Vector3 soundPos);

        if (heard)
        {
            lastSoundPos = soundPos;
            hasLastSoundPos = true;

            if (intensity >= chaseKeepIntensity)
            {
                lastStrongSoundTime = Time.time;
            }

            Debug.Log($"[MONSTER] Heard sound intensity:{intensity:F1} pos:{soundPos}");

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
    }

    void HandleSoundInput(float intensity, Vector3 soundPos)
    {
        float distToSound = Vector3.Distance(transform.position, soundPos);

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

        if (distToSound <= chaseDistance && intensity >= chaseKeepIntensity)
        {
            return true;
        }

        return false;
    }

    void UpdatePatrol()
    {
        agent.speed = patrolSpeed;
        agent.stoppingDistance = 0f;

        if (!hasPatrolTarget || (!agent.pathPending && agent.remainingDistance <= waypointArriveDistance))
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

        if (hasLastSoundPos && !agent.pathPending && agent.remainingDistance <= waypointArriveDistance)
        {
            agent.ResetPath();
        }

        UpdateMoveAnimation();
    }

    void UpdateChase()
    {
        agent.speed = chaseSpeed;
        agent.stoppingDistance = attackRange;

        if (Time.time - lastStrongSoundTime >= losePlayerAfterNoSound)
        {
            Debug.Log("[MONSTER] Lost player: no strong sound for 1 second.");
            EnterAlert(lastSoundPos);
            return;
        }

        if (player == null)
        {
            if (hasLastSoundPos)
            {
                agent.SetDestination(lastSoundPos);
            }

            UpdateMoveAnimation();
            return;
        }

        float distToPlayer = Vector3.Distance(transform.position, player.position);

        if (distToPlayer <= attackRange)
        {
            EnterAttack();
            return;
        }

        agent.SetDestination(player.position);
        FacePlayerSoft();

        UpdateMoveAnimation();

        if (distToPlayer > disengageDistance)
        {
            EnterAlert(lastSoundPos);
        }
    }

    void UpdateAttack()
    {
        agent.ResetPath();
        agent.velocity = Vector3.zero;

        FacePlayer();

        attackTimer += Time.deltaTime;

        if (attackTimer >= attackCooldown)
        {
            attackTimer = 0f;

            if (Time.time - lastStrongSoundTime >= losePlayerAfterNoSound)
            {
                EnterAlert(lastSoundPos);
                return;
            }

            if (player == null)
            {
                EnterAlert(lastSoundPos);
                return;
            }

            float distToPlayer = Vector3.Distance(transform.position, player.position);

            if (distToPlayer > attackRange)
            {
                EnterChase();
                return;
            }

            PlayAnim(biteAnim);
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

        // 不直接播放 Walk1，避免剛進巡邏但還沒移動時播錯
        PlayAnim(idleAnim);

        if (rend != null)
        {
            rend.material.color = Color.white;
        }

        Debug.Log("[MONSTER] State = Patrol");
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

        if (rend != null)
        {
            rend.material.color = new Color(1f, 0.5f, 0f);
        }

        Debug.Log("[MONSTER] State = Alert");
    }

    void EnterChase()
    {
        currentState = State.Chase;

        idleTimer = 0f;

        agent.speed = chaseSpeed;
        agent.stoppingDistance = attackRange;

        lastStrongSoundTime = Time.time;

        if (player != null)
        {
            agent.SetDestination(player.position);
        }
        else if (hasLastSoundPos)
        {
            agent.SetDestination(lastSoundPos);
        }

        // 追擊開始時先播 Walk2，避免遠距離剛開始追時平移
        PlayAnim(walk2Anim);

        if (rend != null)
        {
            rend.material.color = Color.red;
        }

        Debug.Log("[MONSTER] State = Chase");
    }

    void EnterAttack()
    {
        currentState = State.Attack;

        agent.ResetPath();
        agent.velocity = Vector3.zero;

        attackTimer = 0f;
        idleTimer = 0f;

        FacePlayer();
        PlayAnim(biteAnim);

        if (rend != null)
        {
            rend.material.color = new Color(0.4f, 0f, 0f);
        }

        Debug.Log("[MONSTER] State = Attack");
    }

    void SetRandomPatrolPoint()
    {
        for (int i = 0; i < 10; i++)
        {
            Vector3 randomDir = Random.insideUnitSphere * patrolRadius;
            randomDir += transform.position;
            randomDir.y = transform.position.y;

            if (NavMesh.SamplePosition(randomDir, out NavMeshHit hit, patrolRadius, NavMesh.AllAreas))
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
        if (animator == null) return;
        if (currentState == State.Attack) return;
        if (agent == null || !agent.enabled) return;

        bool hasValidPath =
            agent.hasPath &&
            !agent.pathPending &&
            agent.remainingDistance > agent.stoppingDistance + movingDistanceBuffer;

        bool isCalculatingPath =
            agent.pathPending;

        bool hasVelocity =
            agent.velocity.magnitude > moveAnimThreshold ||
            agent.desiredVelocity.magnitude > moveAnimThreshold;

        // 重點：
        // 只要有路徑還沒到，或正在算路，或有速度，都視為正在走
        // 這樣遠距離追玩家時不會變成平移
        bool shouldPlayWalk = hasValidPath || isCalculatingPath || hasVelocity;

        if (shouldPlayWalk)
        {
            idleTimer = 0f;

            if (currentState == State.Patrol)
            {
                PlayAnim(walk1Anim);
            }
            else if (currentState == State.Alert || currentState == State.Chase)
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

    void FacePlayer()
    {
        if (player == null) return;

        Vector3 dir = player.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude <= 0.001f) return;

        Quaternion lookRotation = Quaternion.LookRotation(dir);
        transform.rotation = lookRotation;
    }

    void FacePlayerSoft()
    {
        if (player == null) return;

        Vector3 dir = player.position - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude <= 0.001f) return;

        Quaternion lookRotation = Quaternion.LookRotation(dir);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            lookRotation,
            Time.deltaTime * 5f
        );
    }

    void PlayAnim(string animName)
    {
        if (animator == null) return;
        if (string.IsNullOrEmpty(animName)) return;
        if (currentAnim == animName) return;

        currentAnim = animName;
        animator.CrossFade(animName, 0.15f);

        Debug.Log("[ANIM] Play " + animName);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, patrolRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, chaseDistance);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, disengageDistance);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        if (hasLastSoundPos)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawSphere(lastSoundPos, 0.4f);
            Gizmos.DrawLine(transform.position, lastSoundPos);
        }
    }
}