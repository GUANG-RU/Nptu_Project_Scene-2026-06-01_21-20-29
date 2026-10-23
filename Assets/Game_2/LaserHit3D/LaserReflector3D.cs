using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class LaserReflector3D : MonoBehaviour
{
    public enum LaserType { Source, Prism, Mirror }
    
    [Header("機關類型")]
    public LaserType nodeType = LaserType.Mirror;

    [Header("雷射發射設定")]
    public Color laserColor = Color.red;
    public LayerMask hitLayers; // 務必勾選 Default 或包含鏡子/牆壁的 Layer
    public int maxReflections = 5; // 最大折射次數

    [Header("分光稜鏡專用 (僅 Prism 需設定)")]
    public Transform[] prismOutputPoints; // 3 個發射點 (RGB)
    public Color[] prismColors = new Color[] { Color.red, Color.green, Color.blue };

    [Header("雷射外觀設定")]
    public float laserWidth = 0.25f; // 可在 Inspector 隨時調整數值

    private List<LineRenderer> lineRenderers = new List<LineRenderer>();

    private void Update()
    {
        ClearLines();

        if (nodeType == LaserType.Prism)
        {
            // 中央稜鏡：向三個方向分別發射 R, G, B 雷射
            for (int i = 0; i < prismOutputPoints.Length; i++)
            {
                if (i < prismColors.Length && prismOutputPoints[i] != null)
                {
                    CastLaserLine(prismOutputPoints[i].position, prismOutputPoints[i].forward, prismColors[i], 0, i);
                }
            }
        }
    }

    private void CastLaserLine(Vector3 startPos, Vector3 direction, Color color, int depth, int lineIndex)
    {
        if (depth >= maxReflections) return;

        // 使用獨占公式計算 LineRenderer 索引，避免 RGB 三光束相互抹除
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

            // 打到 3D 鏡面：計算反射角並推進第二段雷射
            if (hit.collider.CompareTag("Mirror"))
            {
                Vector3 reflectDir = Vector3.Reflect(direction, hit.normal);
                // 沿法線推開 0.01 米，避免二次 Raycast 打到鏡子本身
                Vector3 nextStartPos = hit.point + hit.normal * 0.01f;
                CastLaserLine(nextStartPos, reflectDir, color, depth + 1, lineIndex);
            }
            // 打到光敏接收器
            else if (hit.collider.CompareTag("Photosensor"))
            {
                // 使用 GetComponentInParent 防範碰撞體位在子物件的情況
                Photosensor sensor = hit.collider.GetComponentInParent<Photosensor>();
                if (sensor != null)
                {
                    sensor.ReceiveLaser(color);
                }
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
            GameObject lineObj = new GameObject($"LaserLine_{lineRenderers.Count}");
            lineObj.transform.SetParent(transform);
            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderers.Add(lr);
        }

        LineRenderer currentLr = lineRenderers[targetIdx];
        currentLr.enabled = true;
        
        // 即時讀取變數設定寬度
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