using UnityEngine;

/// <summary>
/// V2 通道盡頭觸發器
/// 放在浮動走道（WrongPath / CorrectPath）的盡頭
/// 玩家走到這裡 → 通知系統準備無縫傳送
/// </summary>
[RequireComponent(typeof(Collider))]
public class TurnTrigger_V2 : MonoBehaviour
{
    public System.Action OnPlayerEnter;

    void Awake()
    {
        var col = GetComponent<Collider>();
        col.isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            OnPlayerEnter?.Invoke();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.1f, 0.5f);
        if (TryGetComponent<BoxCollider>(out var bc))
            Gizmos.DrawCube(transform.position, bc.size);
        else
            Gizmos.DrawSphere(transform.position, 0.4f);
    }
}
