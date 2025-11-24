using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(NetworkObject))]
[DisallowMultipleComponent]
public class GravityWell : NetworkBehaviour
{
    [Header("Well Settings")]
    [Tooltip("Wie lange der Well aktiv bleibt (Sekunden).")]
    public float duration = 5f;

    [Tooltip("Wirkungsradius (Meter).")]
    public float radius = 8f;

    [Tooltip("Basis-Beschleunigung Richtung Zentrum (m/s²).")]
    public float pullAcceleration = 12f;

    [Tooltip("Skaliert die Stärke über die Lebenszeit (0..1).")]
    public AnimationCurve strengthOverLifetime = AnimationCurve.EaseInOut(0, 1, 1, 0);

    [Tooltip("Welche Layer als Gegner gelten.")]
    public LayerMask enemyLayer;

    [Header("Behavior")]
    [Tooltip("Wenn true, rein horizontal ziehen (kein nach unten saugen).")]
    public bool ignoreVerticalPull = true;

    [Tooltip("Innerhalb dieses Radius wird kein weiterer Zug angewandt (verhindert Jittern im Zentrum).")]
    public float innerClampRadius = 1.2f;

    [Header("FX (optional)")]
    public AudioSource spawnSfx;
    public ParticleSystem loopFx;
    public ParticleSystem endFx;

    private float _spawnTime;

    public override void OnNetworkSpawn()
    {
        _spawnTime = Time.time;

        // Server terminiert selbst
        if (IsServer)
            Invoke(nameof(DespawnSelf), duration);

        // Client-seitige Effekte
        if (spawnSfx != null) spawnSfx.Play();
        if (loopFx != null) loopFx.Play();
    }

    private void FixedUpdate()
    {
        // Nur der Server „zieht“, Clients zeigen nur FX
        if (!IsServer) return;

        // Lebenszeit-Kurve
        float age = Time.time - _spawnTime;
        float lifeT = Mathf.Clamp01(duration > 0.0001f ? (age / duration) : 1f);
        float lifeStrength = Mathf.Max(0f, strengthOverLifetime.Evaluate(lifeT));
        if (lifeStrength <= 0f) return;

        Vector3 center = transform.position;

        // Alle Gegner im Radius
        Collider[] hits = Physics.OverlapSphere(center, radius, enemyLayer, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0) return;

        for (int i = 0; i < hits.Length; i++)
        {
            // PullReceiver benötigt (liegt am Enemy-Root)
            var receiver = hits[i].GetComponentInParent<PullReceiver>();
            if (receiver == null) continue;

            Vector3 targetPos = hits[i].transform.position;

            // Effektiver Sog-Mittelpunkt:
            // - horizontaler Sog: Center-Y = Ziel-Y (kein Downward-Pull)
            Vector3 effCenter = center;
            if (ignoreVerticalPull)
                effCenter.y = targetPos.y;

            // Richtung & Distanz
            Vector3 toCenter = effCenter - targetPos;
            float dist = toCenter.magnitude;
            if (dist < 0.0001f) continue;

            // im Kern nicht weiter ziehen
            if (dist <= innerClampRadius) continue;

            Vector3 dir = toCenter / dist;
            if (ignoreVerticalPull) dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) continue;

            // Am Rand stärker, innen sanfter
            float dist01 = Mathf.InverseLerp(0f, radius, dist);
            float falloff = Mathf.SmoothStep(1f, 0.15f, dist01);

            // finale Beschleunigung für diesen Tick
            Vector3 accel = dir * (pullAcceleration * lifeStrength * falloff);

            // Statt AddForce: in den PullReceiver puffern
            receiver.AddAccel(accel);
        }
    }

    private void DespawnSelf()
    {
        // FX umschalten
        if (loopFx != null) loopFx.Stop();
        if (endFx != null) endFx.Play();

        // kleines Delay, damit EndFX sichtbar sind
        StartCoroutine(DespawnSoon());
    }

    private System.Collections.IEnumerator DespawnSoon()
    {
        yield return new WaitForSeconds(0.25f);

        if (this != null && NetworkObject != null)
        {
            if (NetworkObject.IsSpawned)
                NetworkObject.Despawn(true);
            else
                Destroy(gameObject);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        var c = new Color(0.2f, 0.8f, 1f, 0.25f);
        Gizmos.color = c;
        Gizmos.DrawSphere(transform.position, radius);

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, innerClampRadius);
    }
#endif
}
