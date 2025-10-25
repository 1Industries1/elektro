using UnityEngine;
using Unity.Netcode;
using UnityEngine.EventSystems;

public class EnergyExplosionAbility : NetworkBehaviour
{
    [Header("Placement")]
    public NetworkObject energyExplosionPrefab;      // Dein Prefab (muss NetworkObject haben)
    public LayerMask baseLayer;                      // = „Base“
    public float maxPlaceDistance = 40f;             // wie weit vor dem Spieler platzierbar
    public float cooldown = 8f;

    [Tooltip("Wie weit über dem Boden (entlang der Boden-Normale) gespawnt wird, um Boden-Kollision zu vermeiden.")]
    public float surfaceOffset = 1.8f;


    [Header("Input")]
    public KeyCode placeKey = KeyCode.Mouse0;        // linke Maustaste
    public bool blockWhenPointerOverUI = true;


    [Header("Bullet Time")]
    public bool triggerBulletTime = true;
    [Range(0.01f,1f)] public float bulletScale = 0.2f;
    public float bulletIn  = 0.25f;
    public float bulletHold= 0.75f;
    public float bulletOut = 0.35f;

    private float _nextAllowedTime;
    private Camera _cam;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
            _cam = Camera.main;

    }

    void Update()
    {
        if (!IsOwner) return;

        if (Input.GetKeyDown(placeKey))
        {
            if (blockWhenPointerOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            if (Time.time < _nextAllowedTime) return;


            if (_cam == null) _cam = Camera.main;

            if (_cam != null && Physics.Raycast(_cam.ScreenPointToRay(Input.mousePosition), out var hit, 500f, baseLayer, QueryTriggerInteraction.Ignore))
            {
                // Reichweiten-Validierung (vom Spieler aus)
                if (Vector3.Distance(transform.position, hit.point) <= maxPlaceDistance)
                {
                    // an Server schicken (Position + Normalen für Ausrichtung)
                    SpawnEnergyExplosionServerRpc(hit.point, hit.normal);
                    _nextAllowedTime = Time.time + cooldown;
                }
            }
        }
    }

    [ServerRpc]
    private void SpawnEnergyExplosionServerRpc(Vector3 hitPoint, Vector3 hitNormal, ServerRpcParams rpcParams = default)
    {
        // Sicherheit: nur Owner darf für sich selbst
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        if (energyExplosionPrefab == null) return;

        // Reichweite serverseitig prüfen
        if (Vector3.Distance(transform.position, hitPoint) > maxPlaceDistance) return;

        // Bodenvalidierung serverseitig (kleiner Down-Ray auf „Base“)
        var origin = hitPoint + Vector3.up * 2f;
        if (!Physics.Raycast(origin, Vector3.down, out var groundHit, 4f, baseLayer, QueryTriggerInteraction.Ignore))
            return;


        // Spawn-Pose: leicht über dem Boden entlang der Normale
        Vector3 up = groundHit.normal.sqrMagnitude > 0.01f ? groundHit.normal.normalized : Vector3.up;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, up);
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
        Quaternion rot = Quaternion.LookRotation(forward.normalized, up);

        Vector3 spawnPos = groundHit.point + up * Mathf.Max(0f, surfaceOffset);

        // (Optional) winziger Safety-Adjust: stelle sicher, dass wir nicht im Boden stecken
        if (Physics.Raycast(spawnPos + up * 0.2f, -up, out var poke, 0.25f, baseLayer, QueryTriggerInteraction.Ignore))
            spawnPos = poke.point + up * surfaceOffset;

        // Spawnen
        NetworkObject well = Instantiate(energyExplosionPrefab, spawnPos, rot);
        well.Spawn(true);

        if (triggerBulletTime)
            TriggerBulletTimeClientRpc(bulletScale, bulletIn, bulletHold, bulletOut);
    }

    [ClientRpc]
    private void TriggerBulletTimeClientRpc(float scale, float inDur, float hold, float outDur)
    {
        // Läuft auf ALLEN Clients (auch Host-Client). Dedicated Server bleibt unberührt.
        if (SlowMoManager.Instance != null)
            SlowMoManager.Instance.BulletTime(scale, inDur, hold, outDur);
    }
}
