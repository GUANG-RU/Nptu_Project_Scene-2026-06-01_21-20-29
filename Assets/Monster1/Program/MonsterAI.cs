using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityStandardAssets.Characters.FirstPerson;

public class MonsterAI : MonoBehaviour
{
    [Header("Target")]
    public Transform player;

    [Header("Detection")]
    public float detectRange = 10f;
    public float attackRange = 1.5f;
    public LayerMask obstacleLayer;

    [Header("Flashlight")]
    public Transform flashlight;
    public Light flashlightLight;

    [Tooltip("正常距離時，手電筒需要對準怪物的角度。")]
    public float flashlightStopAngle = 20f;

    [Tooltip("近距離保護距離。怪物在這個距離內時，手電筒判定會比較寬鬆。")]
    public float closeLightProtectionDistance = 2.2f;

    [Tooltip("近距離時允許的手電筒角度。建議比 flashlightStopAngle 大。")]
    public float closeLightStopAngle = 60f;

    [Header("Movement")]
    public NavMeshAgent agent;
    public float rotateSpeed = 8f;

    [Tooltip("實際速度低於這個值時，不播放走路動畫")]
    public float minimumWalkVelocity = 0.08f;

    [Tooltip("走路動畫原本對應的移動速度")]
    public float animationReferenceSpeed = 2f;

    [Tooltip("走路動畫最慢播放倍率")]
    public float minimumWalkAnimationSpeed = 0.75f;

    [Tooltip("走路動畫最快播放倍率")]
    public float maximumWalkAnimationSpeed = 1.5f;

    [Header("Attack")]
    public float attackCooldown = 1.5f;

    private float attackTimer = 0f;
    private bool isGameOverSequence = false;

    [Header("Game Over")]
    public string gameOverSceneName = "GameOver";

    [Header("Animation")]
    public Animator animator;

    [Header("Sound")]
    public AudioSource walkAudioSource;
    public AudioSource sfxAudioSource;
    public AudioClip walkSound;
    public AudioClip attackSound;

    private bool isStoppedByLight = false;

    private bool currentIsLit = false;
    private bool currentIsWalking = false;
    private bool currentIsAttacking = false;

    private FirstPersonController playerController;
    private UltimateFlashlightController flashlightController;

    void Start()
    {
        InitComponents();
        InitAudio();
        InitPlayerController();
        InitFlashlightController();

        if (agent != null)
        {
            agent.updateRotation = true;
        }
    }

    void Update()
    {
        attackTimer -= Time.deltaTime;

        if (isGameOverSequence)
        {
            FacePlayer();
            return;
        }

        isStoppedByLight = IsHitByFlashlight();

        if (isStoppedByLight)
        {
            StopMonsterByLight();
            UpdateAnimationFromMovement();
            return;
        }

        if (!CanSeePlayer())
        {
            Idle();
            UpdateAnimationFromMovement();
            return;
        }

        float distance = Vector3.Distance(
            transform.position,
            player.position
        );

        if (distance <= attackRange)
        {
            // 攻擊前再檢查一次手電筒，避免近距離同一幀誤判
            if (IsHitByFlashlight())
            {
                StopMonsterByLight();
                UpdateAnimationFromMovement();
                return;
            }

            AttackPlayer();
        }
        else
        {
            ChasePlayer();
        }

        UpdateAnimationFromMovement();
    }

    void InitComponents()
    {
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (animator != null)
        {
            animator.cullingMode =
                AnimatorCullingMode.AlwaysAnimate;
        }
    }

    void InitPlayerController()
    {
        if (player == null)
        {
            return;
        }

        playerController =
            player.GetComponent<FirstPersonController>();

        if (playerController == null)
        {
            playerController =
                player.GetComponentInParent<FirstPersonController>();
        }

        if (playerController == null)
        {
            playerController =
                player.GetComponentInChildren<FirstPersonController>();
        }

        if (playerController == null)
        {
            Debug.LogWarning(
                "MonsterAI 找不到 FirstPersonController！"
            );
        }
    }

    void InitFlashlightController()
    {
        if (player == null)
        {
            return;
        }

        flashlightController =
            player.GetComponent<UltimateFlashlightController>();

        if (flashlightController == null)
        {
            flashlightController =
                player.GetComponentInChildren<UltimateFlashlightController>();
        }

        if (flashlightController == null)
        {
            flashlightController =
                player.GetComponentInParent<UltimateFlashlightController>();
        }

        if (flashlightController == null)
        {
            Debug.LogWarning(
                "MonsterAI 找不到 UltimateFlashlightController！"
            );
        }
    }

    void InitAudio()
    {
        if (walkAudioSource != null)
        {
            walkAudioSource.clip = walkSound;
            walkAudioSource.loop = true;
            walkAudioSource.playOnAwake = false;
        }

        if (sfxAudioSource != null)
        {
            sfxAudioSource.playOnAwake = false;
        }
    }

    bool CanSeePlayer()
    {
        if (player == null)
        {
            return false;
        }

        float distance = Vector3.Distance(
            transform.position,
            player.position
        );

        if (distance > detectRange)
        {
            return false;
        }

        Vector3 origin =
            transform.position +
            Vector3.up * 1.5f;

        Vector3 target =
            player.position +
            Vector3.up * 1.0f;

        Vector3 direction =
            target - origin;

        if (Physics.Raycast(
            origin,
            direction.normalized,
            distance,
            obstacleLayer))
        {
            return false;
        }

        return true;
    }

    bool IsHitByFlashlight()
    {
        if (flashlight == null ||
            flashlightLight == null)
        {
            return false;
        }

        if (!flashlightLight.enabled)
        {
            return false;
        }

        Vector3[] monsterPoints =
        {
            transform.position + Vector3.up * 0.6f,
            transform.position + Vector3.up * 1.2f,
            transform.position + Vector3.up * 1.8f
        };

        for (int i = 0; i < monsterPoints.Length; i++)
        {
            if (IsMonsterPointLit(monsterPoints[i]))
            {
                return true;
            }
        }

        return false;
    }

    bool IsMonsterPointLit(Vector3 monsterPoint)
    {
        Vector3 directionToMonster =
            monsterPoint - flashlight.position;

        float distanceToMonster =
            directionToMonster.magnitude;

        if (distanceToMonster <= 0.01f)
        {
            return true;
        }

        if (distanceToMonster > flashlightLight.range)
        {
            return false;
        }

        float allowedAngle = flashlightStopAngle;

        if (distanceToMonster <= closeLightProtectionDistance)
        {
            allowedAngle = Mathf.Max(
                flashlightStopAngle,
                closeLightStopAngle
            );
        }

        float angle = Vector3.Angle(
            flashlight.forward,
            directionToMonster.normalized
        );

        if (angle > allowedAngle)
        {
            return false;
        }

        if (!HasClearFlashlightPath(
            directionToMonster.normalized,
            distanceToMonster))
        {
            return false;
        }

        return true;
    }

    bool HasClearFlashlightPath(
        Vector3 direction,
        float distance)
    {
        RaycastHit[] hits = Physics.RaycastAll(
            flashlight.position,
            direction,
            distance,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        System.Array.Sort(
            hits,
            (a, b) => a.distance.CompareTo(b.distance)
        );

        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].transform;

            if (hitTransform == null)
            {
                continue;
            }

            // 打到怪物自己，代表手電筒有照到怪物
            if (hitTransform == transform ||
                hitTransform.IsChildOf(transform))
            {
                return true;
            }

            // 忽略玩家自己的 Collider
            if (player != null &&
                (hitTransform == player ||
                 hitTransform.IsChildOf(player)))
            {
                continue;
            }

            // 忽略手電筒自己的 Collider
            if (flashlight != null &&
                (hitTransform == flashlight ||
                 hitTransform.IsChildOf(flashlight)))
            {
                continue;
            }

            // 打到其他東西，代表中間被擋住
            return false;
        }

        // 沒打到任何東西，但角度與距離都符合時，近距離允許成立
        return distance <= closeLightProtectionDistance;
    }

    void ChasePlayer()
    {
        if (agent == null ||
            player == null)
        {
            return;
        }

        agent.isStopped = false;

        NavMeshHit hit;

        if (NavMesh.SamplePosition(
            player.position,
            out hit,
            2f,
            NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
        else
        {
            agent.SetDestination(player.position);
        }

        currentIsLit = false;
        currentIsAttacking = false;
    }

    void AttackPlayer()
    {
        if (isGameOverSequence)
        {
            return;
        }

        // 最後保險：攻擊函式內再確認一次
        if (IsHitByFlashlight())
        {
            StopMonsterByLight();
            return;
        }

        isGameOverSequence = true;

        DisablePlayerControl();

        StopAgentImmediately();

        StopWalkSound();

        FacePlayer();

        currentIsLit = false;
        currentIsWalking = false;
        currentIsAttacking = true;

        if (animator != null)
        {
            animator.speed = 1f;

            animator.SetBool(
                "IsLit",
                false
            );

            animator.SetBool(
                "IsWalking",
                false
            );

            animator.SetBool(
                "IsAttacking",
                true
            );

            animator.SetTrigger(
                "AttackTrigger"
            );
        }

        PlayAttackSound();

        Debug.Log(
            "怪物開始攻擊玩家"
        );
    }

    void DisablePlayerControl()
    {
        if (playerController == null)
        {
            InitPlayerController();
        }

        if (playerController != null)
        {
            playerController.enabled = false;

            Debug.Log(
                "FirstPersonController 已停用"
            );
        }

        if (flashlightController == null)
        {
            InitFlashlightController();
        }

        if (flashlightController != null)
        {
            flashlightController.DisableByMonsterAttack();

            Debug.Log(
                "手電筒控制已停用"
            );
        }
    }

    public void OnAttackAnimationFinished()
    {
        if (!isGameOverSequence)
        {
            return;
        }

        string currentScene =
            SceneManager.GetActiveScene().name;

        PlayerPrefs.SetString(
            "LastLevel",
            currentScene
        );

        PlayerPrefs.Save();

        SceneManager.LoadScene(
            gameOverSceneName
        );
    }

    void StopMonsterByLight()
    {
        StopAgentImmediately();

        StopWalkSound();

        currentIsLit = true;
        currentIsWalking = false;
        currentIsAttacking = false;
    }

    void Idle()
    {
        StopAgentImmediately();

        StopWalkSound();

        currentIsLit = false;
        currentIsWalking = false;
        currentIsAttacking = false;
    }

    void StopAgentImmediately()
    {
        if (agent == null)
        {
            return;
        }

        agent.isStopped = true;
        agent.velocity = Vector3.zero;

        if (agent.isOnNavMesh)
        {
            agent.ResetPath();
        }
    }

    void UpdateAnimationFromMovement()
    {
        if (animator == null)
        {
            return;
        }

        bool isActuallyMoving = false;
        float actualSpeed = 0f;

        if (agent != null &&
            agent.enabled &&
            agent.isOnNavMesh &&
            !agent.isStopped)
        {
            actualSpeed =
                agent.velocity.magnitude;

            bool hasPath =
                agent.hasPath &&
                !agent.pathPending;

            bool hasDistanceToTravel =
                agent.remainingDistance >
                agent.stoppingDistance + 0.05f;

            isActuallyMoving =
                hasPath &&
                hasDistanceToTravel &&
                actualSpeed >= minimumWalkVelocity;
        }

        if (currentIsLit ||
            currentIsAttacking)
        {
            isActuallyMoving = false;
        }

        currentIsWalking =
            isActuallyMoving;

        animator.SetBool(
            "IsLit",
            currentIsLit
        );

        animator.SetBool(
            "IsWalking",
            currentIsWalking
        );

        animator.SetBool(
            "IsAttacking",
            currentIsAttacking
        );

        UpdateAnimatorPlaybackSpeed(
            actualSpeed
        );

        if (currentIsWalking)
        {
            PlayWalkSound();
        }
        else
        {
            StopWalkSound();
        }
    }

    void UpdateAnimatorPlaybackSpeed(
        float actualSpeed)
    {
        if (animator == null)
        {
            return;
        }

        if (!currentIsWalking)
        {
            animator.speed = 1f;
            return;
        }

        float referenceSpeed =
            Mathf.Max(
                animationReferenceSpeed,
                0.01f
            );

        float animationSpeedMultiplier =
            actualSpeed /
            referenceSpeed;

        animationSpeedMultiplier =
            Mathf.Clamp(
                animationSpeedMultiplier,
                minimumWalkAnimationSpeed,
                maximumWalkAnimationSpeed
            );

        animator.speed =
            animationSpeedMultiplier;
    }

    void FacePlayer()
    {
        if (player == null)
        {
            return;
        }

        Vector3 direction =
            player.position -
            transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(
                direction
            );

        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotateSpeed *
                Time.deltaTime
            );
    }

    void PlayWalkSound()
    {
        if (walkAudioSource == null ||
            walkSound == null)
        {
            return;
        }

        if (!walkAudioSource.isPlaying)
        {
            walkAudioSource.Play();
        }
    }

    void StopWalkSound()
    {
        if (walkAudioSource == null)
        {
            return;
        }

        if (walkAudioSource.isPlaying)
        {
            walkAudioSource.Stop();
        }
    }

    void PlayAttackSound()
    {
        if (sfxAudioSource == null ||
            attackSound == null)
        {
            return;
        }

        sfxAudioSource.PlayOneShot(
            attackSound
        );
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color =
            Color.yellow;

        Gizmos.DrawWireSphere(
            transform.position,
            detectRange
        );

        Gizmos.color =
            Color.red;

        Gizmos.DrawWireSphere(
            transform.position,
            attackRange
        );

        Gizmos.color =
            Color.cyan;

        Gizmos.DrawWireSphere(
            transform.position,
            closeLightProtectionDistance
        );
    }
}