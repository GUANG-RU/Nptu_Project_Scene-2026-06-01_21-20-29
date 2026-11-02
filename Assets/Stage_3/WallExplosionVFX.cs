using System.Collections;
using UnityEngine;

/// <summary>
/// 純程式碼爆炸 + 煙霧特效
/// 不需要任何外部素材，直接 AddComponent 或掛在空物件上即可。
///
/// 使用方式：
///   var vfx = WallExplosionVFX.Spawn(position, scale);
///   yield return new WaitForSeconds(vfx.HideDuration);
///   // 此時牆壁可以消失，煙霧仍在散去
/// </summary>
public class WallExplosionVFX : MonoBehaviour
{
    // ── 可調參數 ──────────────────────────────
    [Header("爆炸碎片")]
    public int   debrisCount     = 60;
    public float debrisSpeed     = 8f;
    public float debrisSize      = 0.18f;
    public Color debrisColorA    = new Color(0.55f, 0.35f, 0.15f, 1f);  // 深棕
    public Color debrisColorB    = new Color(0.80f, 0.65f, 0.40f, 1f);  // 淺棕

    [Header("煙霧")]
    public int   smokeCount      = 40;
    public float smokeSpeed      = 2.5f;
    public float smokeSize       = 1.8f;
    public Color smokeColorA     = new Color(0.55f, 0.50f, 0.45f, 0.75f);
    public Color smokeColorB     = new Color(0.30f, 0.28f, 0.25f, 0.60f);

    [Header("閃光")]
    public Color flashColor      = new Color(1f, 0.85f, 0.4f, 1f);

    [Header("時間")]
    public float hideDuration    = 0.6f;   // 牆壁消失前需等待的時間
    public float totalDuration   = 2.5f;   // 整個特效的生命週期

    // ── 唯讀屬性 ──────────────────────────────
    public float HideDuration => hideDuration;

    // ──────────────────────────────────────────
    /// <summary>在指定位置生成一個爆炸特效並自動播放</summary>
    public static WallExplosionVFX Spawn(Vector3 position, float scale = 1f)
    {
        var go  = new GameObject("WallExplosionVFX");
        go.transform.position = position;
        go.transform.localScale = Vector3.one * scale;
        var vfx = go.AddComponent<WallExplosionVFX>();
        return vfx;
    }

    void Start() => StartCoroutine(Play());

    IEnumerator Play()
    {
        SpawnFlash();
        SpawnDebris();
        SpawnSmoke();
        yield return new WaitForSeconds(totalDuration);
        Destroy(gameObject);
    }

    // ── 閃光（一個快速縮放消失的球）────────────
    void SpawnFlash()
    {
        var go  = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.zero;
        Destroy(go.GetComponent<Collider>());

        var mat = MakeUnlitMaterial(flashColor);
        go.GetComponent<Renderer>().material = mat;

        StartCoroutine(AnimateFlash(go, 0.18f));
    }

    IEnumerator AnimateFlash(GameObject go, float dur)
    {
        float s0 = 2.5f;
        for (float t = 0; t < dur; t += Time.deltaTime)
        {
            float p = t / dur;
            float s = Mathf.Lerp(s0, 0f, p * p);
            if (go) go.transform.localScale = Vector3.one * s;
            var mat = go?.GetComponent<Renderer>()?.material;
            if (mat) mat.color = new Color(flashColor.r, flashColor.g, flashColor.b, 1f - p);
            yield return null;
        }
        if (go) Destroy(go);
    }

    // ── 碎片（快速飛出的小正方體）───────────────
    void SpawnDebris()
    {
        for (int i = 0; i < debrisCount; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale    = Vector3.one * debrisSize * Random.Range(0.5f, 1.4f);
            go.transform.localRotation = Random.rotation;
            Destroy(go.GetComponent<Collider>());

            Color c   = Color.Lerp(debrisColorA, debrisColorB, Random.value);
            var   mat = MakeUnlitMaterial(c);
            go.GetComponent<Renderer>().material = mat;

            Vector3 dir   = Random.onUnitSphere;
            dir.y         = Mathf.Abs(dir.y) * 0.6f + 0.4f;  // 偏上方
            float   speed = debrisSpeed * Random.Range(0.5f, 1.5f);
            float   life  = Random.Range(0.4f, 0.9f);

            StartCoroutine(AnimateDebris(go, dir * speed, life));
        }
    }

    IEnumerator AnimateDebris(GameObject go, Vector3 vel, float life)
    {
        Vector3 pos = Vector3.zero;
        float gravity = 14f;
        var   mat = go?.GetComponent<Renderer>()?.material;
        Color col = mat?.color ?? Color.white;

        for (float t = 0; t < life; t += Time.deltaTime)
        {
            if (!go) yield break;
            vel.y -= gravity * Time.deltaTime;
            pos   += vel * Time.deltaTime;
            go.transform.localPosition = pos;
            go.transform.Rotate(vel.normalized * 200f * Time.deltaTime, Space.World);

            float alpha = Mathf.Lerp(1f, 0f, t / life);
            if (mat) mat.color = new Color(col.r, col.g, col.b, alpha);
            yield return null;
        }
        if (go) Destroy(go);
    }

    // ── 煙霧（緩慢膨脹上升的半透明球）──────────
    void SpawnSmoke()
    {
        for (int i = 0; i < smokeCount; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.SetParent(transform);
            // 從牆面面積範圍內的隨機位置噴出
            go.transform.localPosition = new Vector3(
                Random.Range(-1.5f, 1.5f),
                Random.Range(-0.5f, 1.0f),
                Random.Range(-0.3f, 0.3f));
            go.transform.localScale = Vector3.one * 0.1f;
            Destroy(go.GetComponent<Collider>());

            Color c   = Color.Lerp(smokeColorA, smokeColorB, Random.value);
            var   mat = MakeTransparentMaterial(c);
            go.GetComponent<Renderer>().material = mat;

            Vector3 dir   = new Vector3(Random.Range(-0.4f, 0.4f), 1f, Random.Range(-0.2f, 0.2f)).normalized;
            float   speed = smokeSpeed * Random.Range(0.6f, 1.4f);
            float   delay = Random.Range(0f, 0.15f);
            float   life  = Random.Range(1.0f, totalDuration);
            float   maxS  = smokeSize * Random.Range(0.5f, 1.3f);

            StartCoroutine(AnimateSmoke(go, mat, c, dir * speed, delay, life, maxS));
        }
    }

    IEnumerator AnimateSmoke(GameObject go, Material mat, Color baseColor,
                              Vector3 vel, float delay, float life, float maxSize)
    {
        yield return new WaitForSeconds(delay);

        Vector3 pos = go ? go.transform.localPosition : Vector3.zero;

        for (float t = 0; t < life; t += Time.deltaTime)
        {
            if (!go) yield break;
            pos += vel * Time.deltaTime;
            vel  = Vector3.Lerp(vel, Vector3.up * smokeSpeed * 0.3f, Time.deltaTime * 1.5f);

            go.transform.localPosition = pos;

            // 先快速膨脹，後半段漸漸淡出
            float growP  = Mathf.Clamp01(t / (life * 0.35f));
            float fadeP  = Mathf.Clamp01((t - life * 0.5f) / (life * 0.5f));
            float alpha  = baseColor.a * (1f - fadeP);
            float scale  = Mathf.Lerp(0.1f, maxSize, Mathf.SmoothStep(0f, 1f, growP));

            go.transform.localScale = Vector3.one * scale;
            if (mat) mat.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            yield return null;
        }
        if (go) Destroy(go);
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
        // 依序嘗試各管線都有的透明 Shader
        Shader s = Shader.Find("Unlit/Transparent")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("UI/Default")
                ?? Shader.Find("Standard");

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