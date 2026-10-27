using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class LaserCrossSplitter : MonoBehaviour
{
    [Header("--- 發射中心設定 ---")]
    [Tooltip("可手動指定中心點物件；若留空，腳本會自動計算 Collider 幾何正中心")]
    public Transform centerPoint;

    [Header("--- 物理碰撞與反射設定 ---")]
    public LayerMask hitLayers;
    public int maxReflections = 5;

    [Header("--- 雷射外觀設定 ---")]
    public float laserWidth = 0.25f;

    private Color currentLaserColor = Color.black;
    private bool receivedHitThisFrame = false;
    private List<LineRenderer> lineRenderers = new List<LineRenderer>();
    private Collider selfCollider;

    private void Awake()
    {
        selfCollider = GetComponent<Collider>();
    }

    /// <summary>
    /// 由 LaserReflector3D 或其他發射器每幀呼叫
    /// </summary>
    public void ReceiveLaser(Color incomingColor)
    {
        currentLaserColor = incomingColor;
        receivedHitThisFrame = true;
    }

    private void LateUpdate()
    {
        ClearLines();

        if (receivedHitThisFrame)
        {
            // 🌟 1. 自動取得中心點 (優先使用自訂中心，其次使用 Collider 實體中心)
            Vector3 startCenter = transform.position;
            if (centerPoint != null)
            {
                startCenter = centerPoint.position;
            }
            else if (selfCollider != null)
            {
                startCenter = selfCollider.bounds.center;
            }

            // 🌟 2. 向 4 個十字方向 (0°, 90°, 180°, 270°) 發射分光
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f;
                Vector3 launchDir = transform.rotation * Quaternion.Euler(0f, angle, 0f) * Vector3.forward;

                // 沿發射方向微調 0.05 米起點，避免 Raycast 撞到分光器自己
                Vector3 launchPos = startCenter + launchDir * 0.05f;

                CastLaserLine(launchPos, launchDir, currentLaserColor, 0, i);
            }
        }

        // 重置標記
        receivedHitThisFrame = false;
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
            GameObject lineObj = new GameObject($"SplitterLine_{lineRenderers.Count}");
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