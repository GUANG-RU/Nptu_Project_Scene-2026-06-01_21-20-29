using UnityEngine;

public class SoundEmitter2 : MonoBehaviour
{
    public float soundIntensity = 10f;
    public LayerMask monsterLayer;

    public void Emit(float intensity)
    {
        soundIntensity = intensity;

        Collider[] hits = Physics.OverlapSphere(
            transform.position,
            soundIntensity,
            monsterLayer
        );

        foreach (Collider hit in hits)
        {
            if (!hit.TryGetComponent<MonsterHearing2>(out var monster)) continue;

            float perceived = CalcPerceivedIntensity(monster.transform.position);

            if (perceived > 0f)
            {
                monster.ReceiveSound(perceived, transform.position);
            }
        }

        Destroy(gameObject, 0.05f);
    }

    float CalcPerceivedIntensity(Vector3 targetPos)
    {
        float dist = Vector3.Distance(transform.position, targetPos);
        float intensity = soundIntensity - (dist * dist / soundIntensity);

        Vector3 dir = (targetPos - transform.position).normalized;
        RaycastHit[] hits = Physics.RaycastAll(transform.position, dir, dist);

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.TryGetComponent<SoundObstacle2>(out var obs))
            {
                intensity -= obs.dampening;
            }
        }

        return Mathf.Max(0f, intensity);
    }
}