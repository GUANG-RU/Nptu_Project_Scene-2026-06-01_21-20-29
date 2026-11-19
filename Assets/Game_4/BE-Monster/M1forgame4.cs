
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityStandardAssets.Characters.FirstPerson;

public class M1forgame4 : MonoBehaviour
{
    [Header("Target")]
    public Transform player;

    [Header("Attack")]
    public float attackRange = 1.5f;

    [Header("Movement")]
    public NavMeshAgent agent;
    public float rotateSpeed = 8f;

    public float minimumWalkVelocity = 0.01f;
    public float animationReferenceSpeed = 2f;
    public float minimumWalkAnimationSpeed = 0.75f;
    public float maximumWalkAnimationSpeed = 1.5f;

    [Tooltip("重新計算追擊路徑的時間間隔")]
    public float pathUpdateInterval = 0.1f;

    [Header("Red Screen - 攻擊紅色畫面")]
    public Color redScreenColor = Color.red;

    [Range(0f, 1f)]
    public float redScreenAlpha = 0.8f;

    public float redFadeDuration = 0.35f;
    public float redHoldTime = 1.5f;

    [Header("Attack Animation Finish")]
    public bool waitForAttackAnimation = false;
    public float animationFinishTimeout = 3f;

    [Header("Game Over")]
    public string gameOverSceneName = "GameOver";

    [Header("Animation")]
    public Animator animator;

    [Header("Sound")]
    public AudioSource walkAudioSource;
    public AudioSource sfxAudioSource;
    public AudioClip walkSound;
    public AudioClip attackSound;

    [Header("Debug - 執行時觀察")]
    [SerializeField] private float actualMoveSpeed;
    [SerializeField] private bool currentIsWalking;

    private bool isGameOverSequence = false;
    private bool hasLoadedGameOver = false;
    private bool attackAnimationFinished = false;

    private FirstPersonController playerController;
    private UltimateFlashlightController flashlightController;

    private GameObject redScreenCanvas;
    private Image redScreenImage;

    private float attackStartTime;
    private float nextPathUpdateTime;

    private Vector3 lastPosition;


    // ==========================================
    // 初始化
    // ==========================================
    void Start()
    {
        InitComponents();
        InitAudio();
        InitPlayerController();
        InitFlashlightController();

        lastPosition = transform.position;
        nextPathUpdateTime = 0f;

        if (agent != null)
        {
            agent.updateRotation = true;

            agent.stoppingDistance = Mathf.Min(
                agent.stoppingDistance,
                Mathf.Max(0f, attackRange * 0.7f)
            );
        }
    }


    // ==========================================
    // 無條件追擊玩家
    // ==========================================
    void Update()
    {
        if (isGameOverSequence)
        {
            FacePlayer();
            return;
        }

        if (player == null)
        {
            return;
        }

        float distance = Vector3.Distance(
            transform.position,
            player.position
        );

        if (distance <= attackRange)
        {
            AttackPlayer();
            return;
        }

        ChasePlayer();

        UpdateAnimationFromMovement();
    }


    // ==========================================
    // 初始化元件
    // ==========================================
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

            animator.speed = 1f;

            animator.SetBool("IsLit", false);
            animator.SetBool("IsWalking", false);
            animator.SetBool("IsAttacking", false);
        }
        else
        {
            Debug.LogWarning(
                "[M1forgame4] 找不到 Animator！"
            );
        }
    }


    // ==========================================
    // 初始化音效
    // ==========================================
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


    // ==========================================
    // 初始化玩家控制器
    // ==========================================
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
                player.GetComponentInParent
                <FirstPersonController>();
        }

        if (playerController == null)
        {
            playerController =
                player.GetComponentInChildren
                <FirstPersonController>();
        }

        if (playerController == null)
        {
            Debug.LogWarning(
                "[M1forgame4] 找不到 FirstPersonController！"
            );
        }
    }


    // ==========================================
    // 初始化手電筒控制器
    // ==========================================
    void InitFlashlightController()
    {
        if (player == null)
        {
            return;
        }

        flashlightController =
            player.GetComponent
            <UltimateFlashlightController>();

        if (flashlightController == null)
        {
            flashlightController =
                player.GetComponentInChildren
                <UltimateFlashlightController>();
        }

        if (flashlightController == null)
        {
            flashlightController =
                player.GetComponentInParent
                <UltimateFlashlightController>();
        }
    }


    // ==========================================
    // 持續追擊玩家
    // ==========================================
    void ChasePlayer()
    {
        if (agent == null ||
            !agent.enabled ||
            !agent.isOnNavMesh ||
            player == null)
        {
            return;
        }

        agent.isStopped = false;

        // 避免每一幀都重新計算 NavMesh 路徑
        if (Time.time < nextPathUpdateTime)
        {
            return;
        }

        nextPathUpdateTime =
            Time.time +
            Mathf.Max(0.01f, pathUpdateInterval);

        NavMeshHit hit;

        if (NavMesh.SamplePosition(
            player.position,
            out hit,
            2f,
            agent.areaMask))
        {
            agent.SetDestination(hit.position);
        }
        else
        {
            agent.SetDestination(player.position);
        }
    }


    // ==========================================
    // 修正版：根據實際速度播放走路動畫
    // ==========================================
    void UpdateAnimationFromMovement()
    {
        float agentSpeed = 0f;
        float positionSpeed = 0f;

        // 取得 NavMeshAgent 實際移動速度
        if (agent != null &&
            agent.enabled &&
            agent.isOnNavMesh)
        {
            Vector3 velocity = agent.velocity;

            velocity.y = 0f;

            agentSpeed = velocity.magnitude;
        }

        // 根據怪物實際座標變化計算速度
        Vector3 movement =
            transform.position - lastPosition;

        movement.y = 0f;

        if (Time.deltaTime > 0f)
        {
            positionSpeed =
                movement.magnitude / Time.deltaTime;
        }

        lastPosition = transform.position;

        // 取兩者較大的速度
        actualMoveSpeed = Mathf.Max(
            agentSpeed,
            positionSpeed
        );

        // 不再檢查 hasPath、pathPending
        // 或 remainingDistance
        currentIsWalking =
            actualMoveSpeed >=
            Mathf.Max(0.001f, minimumWalkVelocity);

        if (isGameOverSequence)
        {
            currentIsWalking = false;
        }

        // 更新 Animator
        if (animator != null)
        {
            animator.SetBool("IsLit", false);

            animator.SetBool(
                "IsWalking",
                currentIsWalking
            );

            animator.SetBool(
                "IsAttacking",
                false
            );

            UpdateAnimatorPlaybackSpeed(
                actualMoveSpeed
            );
        }

        // 同步走路音效
        if (currentIsWalking)
        {
            PlayWalkSound();
        }
        else
        {
            StopWalkSound();
        }
    }


    // ==========================================
    // 同步移動速度與動畫播放速度
    // ==========================================
    void UpdateAnimatorPlaybackSpeed(float actualSpeed)
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

        float referenceSpeed = Mathf.Max(
            animationReferenceSpeed,
            0.01f
        );

        float multiplier =
            actualSpeed / referenceSpeed;

        multiplier = Mathf.Clamp(
            multiplier,
            minimumWalkAnimationSpeed,
            maximumWalkAnimationSpeed
        );

        animator.speed = multiplier;
    }


    // ==========================================
    // 攻擊玩家
    // ==========================================
    void AttackPlayer()
    {
        if (isGameOverSequence)
        {
            return;
        }

        isGameOverSequence = true;
        attackAnimationFinished = false;

        attackStartTime = Time.unscaledTime;

        // 停止玩家操作
        DisablePlayerControl();

        // 停止怪物移動
        StopAgentImmediately();

        // 停止走路音效
        StopWalkSound();

        // 立即面向玩家
        Vector3 direction =
            player.position - transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation =
                Quaternion.LookRotation(direction);
        }

        currentIsWalking = false;
        actualMoveSpeed = 0f;

        // 播放攻擊動畫
        if (animator != null)
        {
            animator.speed = 1f;

            animator.SetBool("IsLit", false);
            animator.SetBool("IsWalking", false);
            animator.SetBool("IsAttacking", true);

            animator.SetTrigger("AttackTrigger");
        }

        PlayAttackSound();

        // 開始紅色畫面效果
        StartCoroutine(RedScreenSequence());

        Debug.Log(
            "[M1forgame4] 玩家受到攻擊"
        );
    }


    // ==========================================
    // 停止玩家操作
    // ==========================================
    void DisablePlayerControl()
    {
        if (playerController == null)
        {
            InitPlayerController();
        }

        if (playerController != null)
        {
            playerController.enabled = false;
        }

        if (flashlightController == null)
        {
            InitFlashlightController();
        }

        if (flashlightController != null)
        {
            flashlightController.DisableByMonsterAttack();
        }
    }


    // ==========================================
    // 自動建立全螢幕紅色 UI
    // ==========================================
    void CreateRedScreen()
    {
        if (redScreenCanvas != null)
        {
            return;
        }

        redScreenCanvas =
            new GameObject("AttackRedScreenCanvas");

        Canvas canvas =
            redScreenCanvas.AddComponent<Canvas>();

        canvas.renderMode =
            RenderMode.ScreenSpaceOverlay;

        canvas.sortingOrder = 32767;

        GameObject imageObject =
            new GameObject(
                "AttackRedScreen",
                typeof(RectTransform),
                typeof(Image)
            );

        imageObject.transform.SetParent(
            redScreenCanvas.transform,
            false
        );

        RectTransform rect =
            imageObject.GetComponent<RectTransform>();

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;

        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        redScreenImage =
            imageObject.GetComponent<Image>();

        redScreenImage.raycastTarget = false;

        Color initialColor = redScreenColor;
        initialColor.a = 0f;

        redScreenImage.color = initialColor;
    }


    // ==========================================
    // 畫面變紅 → 停留 → GameOver
    // ==========================================
    IEnumerator RedScreenSequence()
    {
        CreateRedScreen();

        float timer = 0f;

        float duration =
            Mathf.Max(0.01f, redFadeDuration);

        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(
                timer / duration
            );

            Color color = redScreenColor;

            color.a = Mathf.Lerp(
                0f,
                redScreenAlpha,
                t
            );

            redScreenImage.color = color;

            yield return null;
        }

        Color finalColor = redScreenColor;
        finalColor.a = redScreenAlpha;

        redScreenImage.color = finalColor;

        yield return new WaitForSecondsRealtime(
            Mathf.Max(0f, redHoldTime)
        );

        // 選擇性等待攻擊動畫完成
        while (waitForAttackAnimation &&
               !attackAnimationFinished &&
               Time.unscaledTime - attackStartTime <
               animationFinishTimeout)
        {
            yield return null;
        }

        LoadGameOver();
    }


    // ==========================================
    // 攻擊動畫結束事件
    // ==========================================
    public void OnAttackAnimationFinished()
    {
        if (!isGameOverSequence)
        {
            return;
        }

        attackAnimationFinished = true;
    }


    // ==========================================
    // 進入 GameOver
    // ==========================================
    void LoadGameOver()
    {
        if (hasLoadedGameOver)
        {
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(
            gameOverSceneName))
        {
            Debug.LogError(
                "找不到 GameOver 場景：" +
                gameOverSceneName
            );

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


    // ==========================================
    // 停止怪物移動
    // ==========================================
    void StopAgentImmediately()
    {
        if (agent == null ||
            !agent.enabled ||
            !agent.isOnNavMesh)
        {
            return;
        }

        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        agent.ResetPath();

        agent.updateRotation = false;
    }


    // ==========================================
    // 讓怪物面向玩家
    // ==========================================
    void FacePlayer()
    {
        if (player == null)
        {
            return;
        }

        Vector3 direction =
            player.position - transform.position;

        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(direction);

        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotateSpeed * Time.unscaledDeltaTime
            );
    }


    // ==========================================
    // 播放走路音效
    // ==========================================
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


    // ==========================================
    // 停止走路音效
    // ==========================================
    void StopWalkSound()
    {
        if (walkAudioSource != null &&
            walkAudioSource.isPlaying)
        {
            walkAudioSource.Stop();
        }
    }


    // ==========================================
    // 播放攻擊音效
    // ==========================================
    void PlayAttackSound()
    {
        if (sfxAudioSource == null ||
            attackSound == null)
        {
            return;
        }

        sfxAudioSource.PlayOneShot(attackSound);
    }


    // ==========================================
    // 清除自動建立的 UI
    // ==========================================
    void OnDestroy()
    {
        if (redScreenCanvas != null)
        {
            Destroy(redScreenCanvas);
        }
    }


    // ==========================================
    // 攻擊範圍輔助顯示
    // ==========================================
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        Gizmos.DrawWireSphere(
            transform.position,
            attackRange
        );
    }
}
