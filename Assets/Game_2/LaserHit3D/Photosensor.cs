using UnityEngine;

public class Photosensor : MonoBehaviour
{
    public Color requiredColor = Color.red;
    public MeshRenderer sensorLightRenderer;
    public bool isActivated = false;

    private bool m_HitThisFrame = false;

    public void ReceiveLaser(Color incomingColor)
    {
        // 比對顏色 RGB 是否接近
        if (ColorsMatch(incomingColor, requiredColor))
        {
            m_HitThisFrame = true;
        }
    }

    private void LateUpdate()
    {
        isActivated = m_HitThisFrame;
        
        // 更改感應器材質顏色亮起反饋
        if (sensorLightRenderer != null)
        {
            sensorLightRenderer.material.color = isActivated ? requiredColor : Color.black;
        }

        m_HitThisFrame = false; // 每影格重置
    }

    private bool ColorsMatch(Color c1, Color c2)
    {
        return Mathf.Abs(c1.r - c2.r) < 0.1f &&
               Mathf.Abs(c1.g - c2.g) < 0.1f &&
               Mathf.Abs(c1.b - c2.b) < 0.1f;
    }
}