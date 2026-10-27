using UnityEngine;
using System.Collections.Generic;

public class LaserCombiner : MonoBehaviour
{
    [Header("--- 聚光發射點 ---")]
    [Tooltip("混合後的雷射發射出口 (若未指定則使用自身 Transform)")]
    public Transform outputPoint;

    [Header("--- 物理碰撞與反射設定 ---")]
    public LayerMask hitLayers;
    public int maxReflections = 5;

    [Header("--- 雷射外觀設定 ---")]
    public float laserWidth = 0.25f;

    private Color accumulatedColor = Color.black;
    private bool receivedHitThisFrame = false;
    private List<LineRenderer> lineRenderers = new List<LineRenderer>();

    private void Start()
    {
        if (outputPoint == null) outputPoint = transform;
    }

    /// <summary>
    /// 可被多條雷射在同幀內重複呼叫，進行光線強度/顏色疊加
    /// </summary>
    public void ReceiveLaser(Color incomingColor)
    {
        accumulatedColor += incomingColor;
        receivedHitThisFrame = true;
    }

    private void LateUpdate()
    {
        ClearLines();

        if (receivedHitThisFrame)
        {
            // 將多條光線的 RGB 疊加值 Clamp 在 0~1 之間，確保標準化顏色
            Color combinedColor = new Color(
                Mathf.Clamp01(accumulatedColor.r),
                Mathf.Clamp01(accumulatedColor.g),
                Mathf.Clamp01(accumulatedColor.b),
                1f
            );

            // 從輸出點前方發射混合後的光線
            CastLaserLine(outputPoint.position, outputPoint.forward, combinedColor, 0, 0);
        }

        // 重置每一幀的混色累加器
        receivedHitThisFrame = false;
        accumulatedColor = Color.black;
    }

    private void CastLaserLine(Vector3 startPos, Vector3 direction, Color color, int depth, int lineIndex)
    {
        if (depth >= maxReflections) return;

        int targetIdx = (lineIndex * maxReflections) + depth;
        LineRenderer lr = GetLineRenderer(targetIdx);

        lr.startColor = color;
        lr.endColor = color;
        lr.positionCount = 2;
        lr.SetPosition(0, startPos);

        RaycastHit hit;
        if (Physics.Raycast(startPos, direction, out hit, 50f, hitLayers))
        {
            lr.SetPosition(1, hit.point);

            if (hit.collider.CompareTag("Mirror"))
            {
                Vector3 reflectDir = Vector3.Reflect(direction, hit.normal);
                Vector3 nextStartPos = hit.point + hit.normal * 0.01f;
                CastLaserLine(nextStartPos, reflectDir, color, depth + 1, lineIndex);
            }
            else if (hit.collider.CompareTag("Photosensor"))
            {
                Photosensor sensor = hit.collider.GetComponentInParent<Photosensor>();
                if (sensor != null) sensor.ReceiveLaser(color);
            }
            else if (hit.collider.CompareTag("Splitter"))
            {
                LaserCrossSplitter splitter = hit.collider.GetComponentInParent<LaserCrossSplitter>();
                if (splitter != null) splitter.ReceiveLaser(color);
            }
            else if (hit.collider.CompareTag("Combiner"))
            {
                LaserCombiner combiner = hit.collider.GetComponentInParent<LaserCombiner>();
                if (combiner != null) combiner.ReceiveLaser(color);
            }
        }
        else
        {
            lr.SetPosition(1, startPos + direction * 50f);
        }
    }

    private LineRenderer GetLineRenderer(int targetIdx)
    {
        while (lineRenderers.Count <= targetIdx)
        {
            GameObject lineObj = new GameObject($"CombinerLine_{lineRenderers.Count}");
            lineObj.transform.SetParent(transform);
            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderers.Add(lr);
        }

        LineRenderer currentLr = lineRenderers[targetIdx];
        currentLr.enabled = true;
        currentLr.startWidth = laserWidth;
        currentLr.endWidth = laserWidth;
        return currentLr;
    }

    private void ClearLines()
    {
        foreach (var lr in lineRenderers)
        {
            if (lr != null) lr.enabled = false;
        }
    }
}