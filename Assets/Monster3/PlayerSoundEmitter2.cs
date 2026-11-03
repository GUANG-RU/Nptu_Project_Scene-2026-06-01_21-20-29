using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerSoundEmitter2 : MonoBehaviour
{
    [Header("聲音強度")]
    public float breathIntensity = 0.5f;
    public float crouchIntensity = 5f;
    public float walkIntensity = 10f;
    public float sprintIntensity = 25f;

    [Header("腳步間隔（秒）")]
    public float crouchInterval = 0.75f;
    public float walkInterval = 0.40f;
    public float sprintInterval = 0.20f;

    [Header("呼吸間隔（秒）")]
    public float breathInterval = 3.0f;

    [Header("蹲下 Input，可不設定")]
    public InputAction crouchAction;

    [Header("多面鏡子設定")]
    [Tooltip("操作任何一面鏡子時，停止產生聲音")]
    public bool muteWhileUsingMirror = true;

    [Header("玩家狀態")]
    public bool IsMovingInput { get; private set; }

    CharacterController cc;

    // 所有鏡子
    RotateMirror[] mirrors;

    bool isUsingMirror = false;

    float footstepTimer;
    float breathTimer;

    [HideInInspector]
    public float lastIntensity;

    [HideInInspector]
    public string lastType = "-";

    void Awake()
    {
        cc = GetComponent<CharacterController>();

        if (cc == null)
        {
            Debug.LogWarning(
                "PlayerSoundEmitter2：找不到 CharacterController。",
                this
            );
        }
    }

    void Start()
    {
        // 自動搜尋場景中所有啟用的 RotateMirror
        RefreshMirrors();
    }

    void RefreshMirrors()
    {
        mirrors = FindObjectsByType<RotateMirror>(
            FindObjectsSortMode.None
        );

        Debug.Log(
            $"[PLAYER] 找到 {mirrors.Length} 面鏡子"
        );
    }

    void Update()
    {
        CheckMirrorState();
        UpdateMoveInputState();

        // 操作鏡子時，不發出任何聲音
        if (isUsingMirror && muteWhileUsingMirror)
        {
            IsMovingInput = false;

            footstepTimer = 0f;
            breathTimer = 0f;

            return;
        }

        HandleBreath();
        HandleFootstep();
    }

    // ============================
    // 檢查所有鏡子的操作狀態
    // ============================

    void CheckMirrorState()
    {
        bool previousState = isUsingMirror;

        isUsingMirror = false;

        if (mirrors == null)
            return;

        foreach (RotateMirror mirror in mirrors)
        {
            if (mirror == null)
                continue;

            if (!mirror.isActiveAndEnabled)
                continue;

            if (mirror.isControlling)
            {
                isUsingMirror = true;
                break;
            }
        }

        if (!previousState && isUsingMirror)
        {
            Debug.Log(
                "[PLAYER] 開始操作鏡子，停止產生聲音"
            );
        }
        else if (previousState && !isUsingMirror)
        {
            Debug.Log(
                "[PLAYER] 結束操作鏡子，恢復聲音系統"
            );
        }
    }

    // ============================
    // 玩家移動輸入
    // ============================

    void UpdateMoveInputState()
    {
        if (Keyboard.current == null)
        {
            IsMovingInput = false;
            return;
        }

        // 任一鏡子正在操作，都不算玩家移動
        if (isUsingMirror)
        {
            IsMovingInput = false;
            return;
        }

        IsMovingInput =
            Keyboard.current.wKey.isPressed ||
            Keyboard.current.aKey.isPressed ||
            Keyboard.current.sKey.isPressed ||
            Keyboard.current.dKey.isPressed ||
            Keyboard.current.upArrowKey.isPressed ||
            Keyboard.current.downArrowKey.isPressed ||
            Keyboard.current.leftArrowKey.isPressed ||
            Keyboard.current.rightArrowKey.isPressed;
    }

    // ============================
    // 呼吸聲
    // ============================

    void HandleBreath()
    {
        breathTimer += Time.deltaTime;

        if (breathTimer < breathInterval)
            return;

        breathTimer = 0f;

        Emit(breathIntensity, "Breath");
    }

    // ============================
    // 腳步聲
    // ============================

    void HandleFootstep()
    {
        if (!IsMovingInput)
        {
            footstepTimer = 0f;
            return;
        }

        bool isCrouch =
            crouchAction != null &&
            crouchAction.IsPressed();

        bool isSprint =
            Keyboard.current != null &&
            (
                Keyboard.current.leftShiftKey.isPressed ||
                Keyboard.current.rightShiftKey.isPressed
            );

        float intensity =
            isCrouch ? crouchIntensity :
            isSprint ? sprintIntensity :
            walkIntensity;

        float interval =
            isCrouch ? crouchInterval :
            isSprint ? sprintInterval :
            walkInterval;

        footstepTimer += Time.deltaTime;

        if (footstepTimer < interval)
            return;

        footstepTimer = 0f;

        string type =
            isCrouch ? "Crouch" :
            isSprint ? "Sprint" :
            "Walk";

        Emit(intensity, type);
    }

    // ============================
    // 發出聲音
    // ============================

    void Emit(float intensity, string type)
    {
        if (isUsingMirror && muteWhileUsingMirror)
            return;

        if (intensity <= 0f)
            return;

        lastIntensity = intensity;
        lastType = type;

        GameObject go = new GameObject(
            $"SoundPulse_{type}"
        );

        go.transform.position = transform.position;

        SoundEmitter2 emitter =
            go.AddComponent<SoundEmitter2>();

        emitter.monsterLayer =
            LayerMask.GetMask("Monster");

        emitter.Emit(intensity);

        Debug.Log(
            $"[SOUND] Player/{type} " +
            $"intensity:{intensity:F1} @ {transform.position}"
        );
    }

#if UNITY_EDITOR

    // ============================
    // Debug HUD
    // ============================

    void OnGUI()
    {
        if (Keyboard.current == null)
            return;

        bool isCrouch =
            crouchAction != null &&
            crouchAction.IsPressed();

        bool isSprint =
            Keyboard.current.leftShiftKey.isPressed ||
            Keyboard.current.rightShiftKey.isPressed;

        float speed = cc != null
            ? new Vector2(
                cc.velocity.x,
                cc.velocity.z
              ).magnitude
            : 0f;

        string mode;

        if (isUsingMirror)
        {
            mode = "MIRROR";
        }
        else if (isCrouch)
        {
            mode = "CROUCH";
        }
        else if (isSprint)
        {
            mode = "SPRINT";
        }
        else
        {
            mode = "WALK";
        }

        string moving =
            IsMovingInput ? "YES" : "NO";

        GUI.color = Color.black;

        GUI.Box(
            new Rect(9, 9, 290, 155),
            ""
        );

        GUI.color = Color.white;

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13
        };

        GUI.Label(
            new Rect(14, 14, 270, 20),
            $"Mode       : {mode}",
            style
        );

        GUI.Label(
            new Rect(14, 32, 270, 20),
            $"Input Move : {moving}",
            style
        );

        GUI.Label(
            new Rect(14, 50, 270, 20),
            $"Velocity   : {speed:F2} m/s",
            style
        );

        GUI.Label(
            new Rect(14, 68, 270, 20),
            $"Last       : {lastType} ({lastIntensity:F1})",
            style
        );

        GUI.Label(
            new Rect(14, 86, 270, 20),
            $"Breath     : {breathInterval - breathTimer:F1}s",
            style
        );

        GUI.Label(
            new Rect(14, 104, 270, 20),
            $"Mirror     : {(isUsingMirror ? "ON" : "OFF")}",
            style
        );

        float maxIntensity = sprintIntensity;

        float ratio = maxIntensity > 0f
            ? Mathf.Clamp01(lastIntensity / maxIntensity)
            : 0f;

        if (isUsingMirror && muteWhileUsingMirror)
        {
            ratio = 0f;
        }

        float barWidth = 200f * ratio;

        GUI.color = new Color(
            0.3f, 0.3f, 0.3f
        );

        GUI.DrawTexture(
            new Rect(14, 130, 200, 16),
            Texture2D.whiteTexture
        );

        GUI.color = Color.Lerp(
            Color.green,
            Color.red,
            ratio
        );

        GUI.DrawTexture(
            new Rect(14, 130, barWidth, 16),
            Texture2D.whiteTexture
        );

        GUI.color = Color.white;
    }

#endif

    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
            return;

        if (isUsingMirror && muteWhileUsingMirror)
            return;

        Gizmos.color = new Color(
            1f, 1f, 0f, 0.12f
        );

        Gizmos.DrawSphere(
            transform.position,
            lastIntensity
        );

        Gizmos.color = new Color(
            1f, 1f, 0f, 0.7f
        );

        Gizmos.DrawWireSphere(
            transform.position,
            lastIntensity
        );
    }
}