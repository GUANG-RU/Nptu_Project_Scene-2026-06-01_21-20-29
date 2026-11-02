using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerSoundEmitter2 : MonoBehaviour
{
    [Header("聲音強度")]
    public float breathIntensity = 0.5f;
    public float crouchIntensity = 5f;
    public float walkIntensity   = 10f;
    public float sprintIntensity = 25f;

    [Header("腳步間隔（秒）")]
    public float crouchInterval  = 0.75f;
    public float walkInterval    = 0.40f;
    public float sprintInterval  = 0.20f;

    [Header("呼吸間隔（秒）")]
    public float breathInterval  = 3.0f;

    [Header("蹲下 Input，可不設定")]
    public InputAction crouchAction;

    [Header("目前玩家輸入狀態")]
    public bool IsMovingInput { get; private set; }

    CharacterController cc;

    float footstepTimer;
    float breathTimer;

    [HideInInspector] public float lastIntensity;
    [HideInInspector] public string lastType = "-";

    void Awake()
    {
        cc = GetComponent<CharacterController>();

        if (cc == null)
        {
            Debug.LogWarning("PlayerSoundEmitter2：找不到 CharacterController。", this);
        }
    }

    void Update()
    {
        UpdateMoveInputState();
        HandleBreath();
        HandleFootstep();
    }

    void UpdateMoveInputState()
    {
        if (Keyboard.current == null)
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

    void HandleBreath()
    {
        breathTimer += Time.deltaTime;

        if (breathTimer < breathInterval) return;

        breathTimer = 0f;
        Emit(breathIntensity, "Breath");
    }

    void HandleFootstep()
    {
        // 重點：
        // 只看玩家輸入，不看 CharacterController.velocity
        // 這樣怪物推動玩家時，不會誤判成腳步聲
        if (!IsMovingInput)
        {
            footstepTimer = 0f;
            return;
        }

        bool isCrouch = crouchAction != null && crouchAction.IsPressed();

        bool isSprint =
            Keyboard.current.leftShiftKey.isPressed ||
            Keyboard.current.rightShiftKey.isPressed;

        float intensity = isCrouch ? crouchIntensity
                        : isSprint ? sprintIntensity
                        :            walkIntensity;

        float interval = isCrouch ? crouchInterval
                       : isSprint ? sprintInterval
                       :            walkInterval;

        footstepTimer += Time.deltaTime;

        if (footstepTimer < interval) return;

        footstepTimer = 0f;

        string type = isCrouch ? "Crouch"
                    : isSprint ? "Sprint"
                    :            "Walk";

        Emit(intensity, type);
    }

    void Emit(float intensity, string type)
    {
        lastIntensity = intensity;
        lastType = type;

        GameObject go = new GameObject($"SoundPulse_{type}");
        go.transform.position = transform.position;

        SoundEmitter2 emitter = go.AddComponent<SoundEmitter2>();
        emitter.monsterLayer = LayerMask.GetMask("Monster");
        emitter.Emit(intensity);

        Debug.Log($"[SOUND] Player/{type} intensity:{intensity:F1} @ {transform.position}");
    }

    void OnGUI()
    {
        if (Keyboard.current == null) return;

        bool isCrouch = crouchAction != null && crouchAction.IsPressed();

        bool isSprint =
            Keyboard.current.leftShiftKey.isPressed ||
            Keyboard.current.rightShiftKey.isPressed;

        float speed = cc != null
            ? new Vector2(cc.velocity.x, cc.velocity.z).magnitude
            : 0f;

        string mode = isCrouch ? "CROUCH" : isSprint ? "SPRINT" : "WALK";
        string moving = IsMovingInput ? "YES" : "NO";

        GUI.color = Color.black;
        GUI.Box(new Rect(9, 9, 270, 135), "");

        GUI.color = Color.white;
        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13
        };

        GUI.Label(new Rect(14, 14, 250, 20), $"Mode       : {mode}", style);
        GUI.Label(new Rect(14, 32, 250, 20), $"Input Move : {moving}", style);
        GUI.Label(new Rect(14, 50, 250, 20), $"Velocity   : {speed:F2} m/s", style);
        GUI.Label(new Rect(14, 68, 250, 20), $"Last       : {lastType} ({lastIntensity:F1})", style);
        GUI.Label(new Rect(14, 86, 250, 20), $"Breath     : {breathInterval - breathTimer:F1}s", style);

        float maxIntensity = sprintIntensity;
        float ratio = maxIntensity > 0f
            ? Mathf.Clamp01(lastIntensity / maxIntensity)
            : 0f;

        float barWidth = 200f * ratio;

        GUI.color = new Color(0.3f, 0.3f, 0.3f);
        GUI.DrawTexture(new Rect(14, 108, 200, 16), Texture2D.whiteTexture);

        GUI.color = Color.Lerp(Color.green, Color.red, ratio);
        GUI.DrawTexture(new Rect(14, 108, barWidth, 16), Texture2D.whiteTexture);

        GUI.color = Color.white;
        GUI.Label(new Rect(220, 106, 40, 20), $"{lastIntensity:F1}", style);
    }

    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        Gizmos.color = new Color(1f, 1f, 0f, 0.12f);
        Gizmos.DrawSphere(transform.position, lastIntensity);

        Gizmos.color = new Color(1f, 1f, 0f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, lastIntensity);
    }
}