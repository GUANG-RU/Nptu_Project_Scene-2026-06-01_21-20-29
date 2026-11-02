using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(MonsterHearing3))]
[RequireComponent(typeof(Animator))] // 強制要求 Animator 組件
public class SoundMonsterAI3 : MonoBehaviour
{
    public enum State
    {
        Patrol,
        Alert,
        Chase
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

    [Header("追擊放棄設定")]
    public float losePlayerAfterNoSound = 1f;

    [Tooltip("只有大於這個強度的聲音，才會刷新追擊時間。建議比 Breath Intensity 大。")]
    public float chaseKeepIntensity = 3f;

    [Tooltip("大於這個強度時，怪物會直接進入追擊。")]
    public float chaseTriggerIntensity = 5f;

    [Header("警戒逾時")]
    public float alertTimeout = 3f;

    NavMeshAgent agent;
    MonsterHearing2 hearing;
    Renderer rend;
    Animator anim; // 動畫控制器引用

    Vector3 lastSoundPos;
    bool hasLastSoundPos;

    float lastStrongSoundTime;
    float alertTimer;

    Vector3 patrolTarget;
    bool hasPatrolTarget;

    // 動畫參數名稱的 Hash 化（優化效能，避免每幀用字串搜尋）
    readonly int speedParamId = Animator.StringToHash("speed");
    readonly int isAlertingParamId = Animator.StringToHash("isAlerting");

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        hearing = GetComponent<MonsterHearing2>();
        anim = GetComponent<Animator>(); // 取得 Animator 組件

        // 怪物模型 Renderer 常常在子物件，所以用 GetComponentInChildren
        rend = GetComponentInChildren<Renderer>();
    }

    void Start()
    {
        agent.stoppingDistance = attackRange;
        EnterPatrol();
    }

    void Update()
    {
        bool heard = hearing.HasHeardSound(out float intensity, out Vector3 soundPos);

        if (heard)
        {
            lastSoundPos = soundPos;
            hasLastSoundPos = true;

            // 重點：
            // 只有足夠大的聲音，例如走路 / 跑步，才會刷新追擊時間
            // 呼吸聲太小，不會讓怪物一直追
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
        // 聲音夠大，直接追
        if (intensity >= chaseTriggerIntensity)
        {
            return true;
        }

        // 聲音距離很近，而且不是太小聲，也可以追
        if (distToSound <= chaseDistance && intensity >= chaseKeepIntensity)
        {
            return true;
        }

        return false;
    }

    void UpdatePatrol()
    {
        agent.speed = patrolSpeed;

        // 如果怪物的 NavMesh 突然停下來或到了目的地，將動畫速度降為 0 (Idle)
        if (!agent.pathPending && agent.remainingDistance <= waypointArriveDistance)
        {
            anim.SetInteger(speedParamId, 0);
            SetRandomPatrolPoint();
        }
        else
        {
            // 巡邏中，給予對應的巡邏動畫速度（對應動態檔中的 threshold 2）
            anim.SetInteger(speedParamId, 2);
        }
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
            // 到達聲音來源點後，停下來切換到嗅聞(Sniff)狀態：速度設為 0 且關閉 isAlerting
            anim.SetInteger(speedParamId, 0);
            anim.SetBool(isAlertingParamId, false);
        }
        else
        {
            // 正在前往聲音來源點時，播放警戒走路(Walk1)：速度設為 0 且開啟 isAlerting
            anim.SetInteger(speedParamId, 0);
            anim.SetBool(isAlertingParamId, true);
        }
    }

    void UpdateChase()
    {
        agent.speed = chaseSpeed;
        agent.stoppingDistance = attackRange;

        // 玩家超過指定時間沒有發出有效聲音，怪物放棄追擊
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
            return;
        }

        float distToPlayer = Vector3.Distance(transform.position, player.position);

        // 重點：
        // 怪物靠近玩家時停止，不要繼續推玩家
        if (distToPlayer <= attackRange)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
            FacePlayer();
            
            // 抓到玩家/貼近玩家時，速度設為 0
            anim.SetInteger(speedParamId, 0);
            return;
        }

        agent.SetDestination(player.position);
        FacePlayerSoft();
        
        // 追擊移動中，動畫速度設為 6 (Crouch/Run 動作)
        anim.SetInteger(speedParamId, 6);

        if (distToPlayer > disengageDistance)
        {
            EnterAlert(lastSoundPos);
        }
    }

    void EnterPatrol()
    {
        currentState = State.Patrol;

        alertTimer = 0f;
        hasPatrolTarget = false;

        agent.speed = patrolSpeed;
        agent.stoppingDistance = 0f;

        // 初始化/同步動畫狀態
        anim.SetBool(isAlertingParamId, false);
        anim.SetInteger(speedParamId, 2); // 進入巡邏，預設切換至巡邏動作門檻

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

        agent.speed = alertSpeed;
        agent.stoppingDistance = 0f;

        if (hasLastSoundPos)
        {
            agent.SetDestination(target);
        }

        // 初始化/同步動畫狀態：前往警戒點時會用到 isAlerting = true
        anim.SetBool(isAlertingParamId, true);
        anim.SetInteger(speedParamId, 0);

        if (rend != null)
        {
            rend.material.color = new Color(1f, 0.5f, 0f);
        }

        Debug.Log("[MONSTER] State = Alert");
    }

    void EnterChase()
    {
        currentState = State.Chase;

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

        // 初始化/同步動畫狀態：追擊時將 speed 設為 6
        anim.SetBool(isAlertingParamId, false);
        anim.SetInteger(speedParamId, 6);

        if (rend != null)
        {
            rend.material.color = Color.red;
        }

        Debug.Log("[MONSTER] State = Chase");
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

        agent.ResetPath();
        anim.SetInteger(speedParamId, 0); // 若找不到巡邏點則靜止
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