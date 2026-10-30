using UnityEngine;
using TMPro;

[RequireComponent(typeof(AudioSource))]
public class RadioController : MonoBehaviour
{
    [Header("Player")]
    public Transform player;

    [Header("Interaction")]
    public KeyCode interactKey = KeyCode.E;
    public float interactDistance = 2.5f;

    [Header("Radio Audio")]
    public AudioSource radioAudio;

    [Range(0f, 1f)]
    public float maxVolume = 1f;

    [Tooltip("此距離內維持最大音量")]
    public float minDistance = 3f;

    [Tooltip("超過此距離時完全聽不到")]
    public float maxDistance = 50f;

    [Header("Volume Falloff")]
    [Tooltip("越大代表音量隨距離下降得越快")]
    [Range(0.5f, 5f)]
    public float volumeFalloffPower = 2f;

    [Header("Hint UI")]
    public GameObject hintPanel;
    public TMP_Text hintText;

    [Header("Debug")]
    public bool showDistanceDebug = false;

    private bool radioIsOn = true;
    private float debugTimer = 0f;

    void Awake()
    {
        if (radioAudio == null)
        {
            radioAudio = GetComponent<AudioSource>();
        }

        radioAudio.playOnAwake = false;
        radioAudio.loop = true;

        // 保留 3D 音源方向效果
        radioAudio.spatialBlend = 1f;

        // 將 Unity 內建距離衰減影響降到很低
        // 主要由 UpdateRadioVolume() 控制音量
        radioAudio.rolloffMode = AudioRolloffMode.Linear;
        radioAudio.minDistance = 0.01f;
        radioAudio.maxDistance = Mathf.Max(
            1000f,
            maxDistance * 100f
        );

        radioAudio.dopplerLevel = 0f;

        HideHint();
    }

    void Start()
    {
        radioIsOn = true;

        if (radioAudio.clip != null)
        {
            radioAudio.Play();
        }
        else
        {
            Debug.LogWarning("Radio 沒有指定 AudioClip！");
        }

        UpdateRadioVolume();
    }

    void Update()
    {
        UpdateRadioVolume();
        HandleInteraction();
    }

    // 根據玩家與 Radio 的距離調整音量
    void UpdateRadioVolume()
    {
        if (radioAudio == null || player == null)
        {
            return;
        }

        float distance = Vector3.Distance(
            player.position,
            radioAudio.transform.position
        );

        // 防止設定不合理
        float near = Mathf.Max(0f, minDistance);
        float far = Mathf.Max(maxDistance, near + 0.1f);

        float volume;

        if (distance <= near)
        {
            volume = maxVolume;
        }
        else if (distance >= far)
        {
            volume = 0f;
        }
        else
        {
            // 0 = 最近，1 = 最遠
            float t = Mathf.InverseLerp(
                near,
                far,
                distance
            );

            // 使用曲線讓音量差異更明顯
            float falloff = Mathf.Pow(
                1f - t,
                volumeFalloffPower
            );

            volume = maxVolume * falloff;
        }

        radioAudio.volume = volume;

        // 可選：Console 顯示目前距離與音量
        if (showDistanceDebug)
        {
            debugTimer += Time.deltaTime;

            if (debugTimer >= 1f)
            {
                Debug.Log(
                    "Radio Distance: " +
                    distance.ToString("F1") +
                    " | Volume: " +
                    volume.ToString("F2")
                );

                debugTimer = 0f;
            }
        }
    }

    // 玩家靠近時才可以按 E
    void HandleInteraction()
    {
        if (player == null)
        {
            HideHint();
            return;
        }

        float distance = Vector3.Distance(
            player.position,
            transform.position
        );

        bool playerIsNear =
            distance <= interactDistance;

        if (playerIsNear)
        {
            ShowHint();

            if (Input.GetKeyDown(interactKey))
            {
                ToggleRadio();
            }
        }
        else
        {
            HideHint();
        }
    }

    // 開啟或關閉 Radio
    void ToggleRadio()
    {
        if (radioAudio == null)
        {
            return;
        }

        radioIsOn = !radioIsOn;

        if (radioIsOn)
        {
            radioAudio.Play();
            UpdateRadioVolume();

            Debug.Log("Radio turned ON");
        }
        else
        {
            radioAudio.Stop();

            Debug.Log("Radio turned OFF");
        }

        ShowHint();
    }

    void ShowHint()
    {
        if (hintPanel != null)
        {
            hintPanel.SetActive(true);
        }

        if (hintText != null)
        {
            hintText.text = radioIsOn
                ? "Press E to turn off radio"
                : "Press E to turn on radio";

            if (hintPanel == null)
            {
                hintText.gameObject.SetActive(true);
            }
        }
    }

    void HideHint()
    {
        if (hintPanel != null)
        {
            hintPanel.SetActive(false);
        }

        if (hintText != null && hintPanel == null)
        {
            hintText.gameObject.SetActive(false);
        }
    }
}