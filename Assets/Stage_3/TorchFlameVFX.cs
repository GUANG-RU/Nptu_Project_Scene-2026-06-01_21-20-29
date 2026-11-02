using System.Collections;
using UnityEngine;

/// <summary>
/// 純程式碼火把特效（不需要任何外部素材）
/// 直接掛在火把/燈架物件上即可
///
/// 包含：
///   - 火焰粒子（橘黃，往上飄散）
///   - 火星（偶爾噴出的小點）
///   - 煙霧（灰色，緩慢上升）
///   - Point Light 隨機閃爍
/// </summary>
public class TorchFlameVFX : MonoBehaviour
{
    [Header("火焰")]
    public int   flameCount     = 12;
    public float flameHeight    = 0.0008f;  // 火焰上升高度（火把寬~0.002）
    public float flameSpeed     = 0.0015f;  // 上升速度
    public float flameSize      = 0.0004f;  // 最大尺寸
    public Color flameColorCore = new Color(1.00f, 0.90f, 0.30f, 0.90f);
    public Color flameColorTip  = new Color(1.00f, 0.30f, 0.05f, 0.00f);

    [Header("火星")]
    public int   sparkCount     = 6;
    public float sparkSpeed     = 0.003f;
    public Color sparkColor     = new Color(1.00f, 0.75f, 0.10f, 1.00f);

    [Header("煙霧")]
    public int   smokeCount     = 5;
    public float smokeSpeed     = 0.0008f;
    public float smokeSize      = 0.0006f;
    public Color smokeColor     = new Color(0.35f, 0.32f, 0.28f, 0.25f);

    [Header("光源")]
    public bool  useLight       = true;
    public float lightRange     = 6f;       // 光照範圍維持大（照亮走廊）
    public float lightIntensity = 1.4f;
    public Color lightColor     = new Color(1.00f, 0.65f, 0.25f);
    [Tooltip("閃爍強度（0=不閃）")]
    public float flickerAmount  = 0.25f;
    public float flickerSpeed   = 8f;
    [Tooltip("光源位置搖晃幅度（0=完全固定）")]
    public float positionWobble = 0.00005f;

    // ── 內部 ──────────────────────────────────
    private Light     _light;
    private float     _baseIntensity;
    private float     _flickerOffset;

    // ──────────────────────────────────────────
    void Start()
    {
        if (useLight) SetupLight();
        _flickerOffset = Random.Range(0f, 100f);

        StartCoroutine(SpawnLoop(SpawnFlame,  1f  / flameCount, flameCount  * 2));
        StartCoroutine(SpawnLoop(SpawnSpark,  0.18f,            999));
        StartCoroutine(SpawnLoop(SpawnSmoke,  1f  / smokeCount, smokeCount  * 2));
    }

    void Update()
    {
        if (_light == null) return;
        float noise = Mathf.PerlinNoise(_flickerOffset, Time.time * flickerSpeed);
        _light.intensity = _baseIntensity * (1f - flickerAmount + flickerAmount * 2f * noise);
        // 微微偏移光源位置模擬火焰搖曳
        _light.transform.localPosition = new Vector3(
            Mathf.Sin(Time.time * 3.7f + _flickerOffset) * positionWobble,
            0f,
            Mathf.Cos(Time.time * 2.9f + _flickerOffset) * positionWobble);
    }

    // ── 持續生成迴圈 ──────────────────────────
    IEnumerator SpawnLoop(System.Action spawn, float interval, int maxBurst)
    {
        // 第一幀先生成一批，避免剛掛上去是空的
        int burst = Mathf.Min(maxBurst, 8);
        for (int i = 0; i < burst; i++)
        {
            spawn();
            yield return new WaitForSeconds(interval * Random.Range(0.5f, 1.5f));
        }
        while (true)
        {
            spawn();
            yield return new WaitForSeconds(interval * Random.Range(0.7f, 1.3f));
        }
    }

    // ── 火焰粒子 ──────────────────────────────
    void SpawnFlame()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.transform.SetParent(transform);
        DestroyImmediate(go.GetComponent<Collider>());  // 立即移除，避免第一幀碰撞

        go.transform.localPosition = new Vector3(
            Random.Range(-0.0001f, 0.0001f),
            0f,
            Random.Range(-0.0001f, 0.0001f));
        go.transform.localScale = Vector3.one * 0.0001f;

        var mat = MakeTransparentMaterial(flameColorCore);
        go.GetComponent<Renderer>().material = mat;

        float life    = flameHeight / flameSpeed * Random.Range(0.7f, 1.3f);
        float wobbleX = Random.Range(-0.6f, 0.6f);
        float wobbleZ = Random.Range(-0.6f, 0.6f);

        StartCoroutine(AnimateFlame(go, mat, life, wobbleX, wobbleZ));
    }

    IEnumerator AnimateFlame(GameObject go, Material mat, float life, float wobX, float wobZ)
    {
        for (float t = 0; t < life; t += Time.deltaTime)
        {
            if (!go) yield break;
            float p = t / life;

            // 位置：往上 + 左右搖擺
            float y    = flameSpeed * t;
            float x    = wobX * Mathf.Sin(t * 4f) * 0.00004f;
            float z    = wobZ * Mathf.Sin(t * 3f + 1f) * 0.00004f;
            go.transform.localPosition = new Vector3(x, y, z);

            // 大小：先長大再縮小
            float s = flameSize * Mathf.Sin(p * Mathf.PI);
            go.transform.localScale = Vector3.one * s;

            // 顏色：從火芯漸變到火尖（透明）
            Color c = Color.Lerp(flameColorCore, flameColorTip, Mathf.Pow(p, 0.7f));
            mat.color = c;

            yield return null;
        }
        if (go) Destroy(go);
    }

    // ── 火星 ──────────────────────────────────
    void SpawnSpark()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.transform.SetParent(transform);
        DestroyImmediate(go.GetComponent<Collider>());
        go.transform.localPosition = new Vector3(
            Random.Range(-0.0001f, 0.0001f), 0.0001f, Random.Range(-0.0001f, 0.0001f));

        float size = Random.Range(0.00002f, 0.00006f);
        go.transform.localScale = Vector3.one * size;

        var mat = MakeUnlitMaterial(sparkColor);
        go.GetComponent<Renderer>().material = mat;

        Vector3 vel = new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(1.5f, 3f),
            Random.Range(-1f, 1f)).normalized * sparkSpeed * Random.Range(0.5f, 1.5f);

        float life = Random.Range(0.3f, 0.7f);
        StartCoroutine(AnimateSpark(go, mat, vel, life));
    }

    IEnumerator AnimateSpark(GameObject go, Material mat, Vector3 vel, float life)
    {
        Vector3 pos = go.transform.localPosition;
        for (float t = 0; t < life; t += Time.deltaTime)
        {
            if (!go) yield break;
            vel.y    -= 0.04f * Time.deltaTime;  // 重力（縮小比例）
            pos      += vel * Time.deltaTime;
            go.transform.localPosition = pos;

            float alpha = Mathf.Lerp(1f, 0f, t / life);
            mat.color   = new Color(sparkColor.r, sparkColor.g, sparkColor.b, alpha);
            yield return null;
        }
        if (go) Destroy(go);
    }

    // ── 煙霧 ──────────────────────────────────
    void SpawnSmoke()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.transform.SetParent(transform);
        DestroyImmediate(go.GetComponent<Collider>());
        go.transform.localPosition = new Vector3(
            Random.Range(-0.00008f, 0.00008f),
            flameHeight * 0.6f,
            Random.Range(-0.00008f, 0.00008f));
        go.transform.localScale = Vector3.one * 0.0001f;

        var mat = MakeTransparentMaterial(smokeColor);
        go.GetComponent<Renderer>().material = mat;

        float life = Random.Range(1.2f, 2.0f);
        float wobX = Random.Range(-0.2f, 0.2f);
        StartCoroutine(AnimateSmoke(go, mat, life, wobX));
    }

    IEnumerator AnimateSmoke(GameObject go, Material mat, float life, float wobX)
    {
        float startY = go.transform.localPosition.y;
        for (float t = 0; t < life; t += Time.deltaTime)
        {
            if (!go) yield break;
            float p = t / life;
            float y = startY + smokeSpeed * t;
            float x = wobX * Mathf.Sin(t * 1.5f) * 0.00008f;
            go.transform.localPosition = new Vector3(x, y, 0f);

            float s     = smokeSize * Mathf.Clamp01(t / (life * 0.3f));
            go.transform.localScale = Vector3.one * s;

            float alpha = smokeColor.a * (1f - Mathf.Pow(p, 0.6f));
            mat.color   = new Color(smokeColor.r, smokeColor.g, smokeColor.b, alpha);
            yield return null;
        }
        if (go) Destroy(go);
    }

    // ── 光源設定 ──────────────────────────────
    void SetupLight()
    {
        var lightGo = new GameObject("FlameLight");
        lightGo.transform.SetParent(transform);
        lightGo.transform.localPosition = new Vector3(0f, 0.1f, 0f);

        _light                = lightGo.AddComponent<Light>();
        _light.type           = LightType.Point;
        _light.color          = lightColor;
        _light.range          = lightRange;
        _light.intensity      = lightIntensity;
        _light.shadows        = LightShadows.Soft;
        _baseIntensity        = lightIntensity;
    }

    // ── Material 工廠 ─────────────────────────
    static Material MakeUnlitMaterial(Color color)
    {
        Shader s = Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
        var mat  = new Material(s);
        mat.color = color;
        return mat;
    }

    static Material MakeTransparentMaterial(Color color)
    {
        // Built-in 管線都有的透明 Shader，依序嘗試
        string[] candidates = {
            "Legacy Shaders/Transparent/Diffuse",
            "Unlit/Transparent",
            "Sprites/Default",
            "UI/Default",
            "Standard",
        };

        Shader s = null;
        foreach (var name in candidates)
        {
            s = Shader.Find(name);
            if (s != null) break;
        }

        var mat = new Material(s);

        // 若落回 Standard，手動開透明模式
        if (s != null && s.name == "Standard")
        {
            mat.SetFloat("_Mode", 3f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
        }

        mat.color = color;
        return mat;
    }
}