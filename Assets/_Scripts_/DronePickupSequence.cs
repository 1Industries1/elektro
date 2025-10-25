using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class DronePickupSequence : NetworkBehaviour
{
    public enum State { Idle, Approach, Dock, Liftoff, Transit, Drop, Exit }
    private State state = State.Idle;

    [Header("Refs")]
    public Transform dockClamp;            // MUSS ein NetworkObject haben!
    public Transform[] pathPoints;         // Approach, Liftoff, Transit..., Drop, Exit
    private Transform player;
    private PlayerCarryMount playerCarry;
    private Coroutine carryLockCo;         // hält den Player am DockClamp „fest“

    [Header("FX")]
    public GameObject vfxThrusterL, vfxThrusterR;
    public Light thrusterLightL, thrusterLightR;
    public AudioSource engineAudio;

    [Header("Timing")]
    public float approachSpeed = 12f;
    public float dockDuration   = 0.35f;
    public float liftoffDuration= 0.8f;
    public float transitSpeed   = 40f;
    public float dropDuration   = 0.35f;

    [Header("Feel")]
    public AnimationCurve liftCurve = AnimationCurve.EaseInOut(0,0,1,1);
    public float bankAngle   = 18f;
    public float tiltForward = 10f;

    int currentIdx;

    // ---------- UI Entry ----------
    public void BeginFromUI()
    {
        Debug.Log("[Drone] BeginFromUI() Button.");
        if (IsServer) Begin();
        else StartSequenceServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    void StartSequenceServerRpc()
    {
        Debug.Log("[Drone] StartSequenceServerRpc @Server");
        if (state == State.Idle) Begin();
    }

    // ---------- Start ----------
    public void Begin()
    {
        if (!IsServer)
        {
            Debug.LogWarning("[Drone] Begin() auf Client ignoriert.");
            return;
        }
        if (state != State.Idle)
        {
            Debug.LogWarning("[Drone] Begin(): State != Idle");
            return;
        }

        EnsurePlayer();
        if (!dockClamp || !dockClamp.TryGetComponent<NetworkObject>(out _))
        {
            Debug.LogError("[Drone] DockClamp fehlt oder hat kein NetworkObject!");
            return;
        }

        StartCoroutine(Run());
    }

    void EnsurePlayer()
    {
        if (playerCarry) return;

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (!playerObj)
        {
            Debug.LogError("[Drone] Kein Player mit Tag 'Player' gefunden!");
            return;
        }
        player      = playerObj.transform;
        playerCarry = playerObj.GetComponent<PlayerCarryMount>();
        if (playerCarry) Debug.Log("[Drone] PlayerCarryMount gefunden.");
        else             Debug.LogError("[Drone] PlayerCarryMount fehlt am Player!");
    }

    // ---------- Main Sequence ----------
    IEnumerator Run()
    {
        Debug.Log("[Drone] Sequence START");
        state = State.Approach;
        currentIdx = 0;

        // APPROACH
        Vector3 target = pathPoints[currentIdx].position;
        while (Vector3.Distance(transform.position, target) > 0.2f)
        {
            MoveTowards(target, approachSpeed, true);
            ThrottleFX(0.6f);
            yield return null;
        }

        // DOCK
        state = State.Dock;
        yield return DockPlayer();

        // LIFTOFF
        state = State.Liftoff;
        var liftoffTarget = pathPoints[++currentIdx].position;
        yield return MoveTimed(transform.position, liftoffTarget, liftoffDuration, liftCurve, 1.15f, tiltForward);

        // TRANSIT
        state = State.Transit;
        while (currentIdx < pathPoints.Length - 2)
        {
            var a = pathPoints[currentIdx].position;
            var b = pathPoints[currentIdx + 1].position;
            yield return MoveSegment(a, b, transitSpeed);
            currentIdx++;
        }

        // DROP
        state = State.Drop;
        var dropSpot = pathPoints[currentIdx + 1].position;
        yield return MoveTimed(transform.position, dropSpot, dropDuration, liftCurve, 0.95f, -tiltForward * 0.5f);
        UndockPlayer();

        // EXIT
        state = State.Exit;
        var exitIdx  = Mathf.Min(currentIdx + 2, pathPoints.Length - 1);
        var exitSpot = pathPoints[exitIdx].position;
        while (Vector3.Distance(transform.position, exitSpot) > 0.2f)
        {
            MoveTowards(exitSpot, approachSpeed * 1.2f, true);
            ThrottleFX(0.5f);
            yield return null;
        }

        state = State.Idle;
        Debug.Log("[Drone] Sequence END (Idle).");
    }

    // ---------- Dock / Undock ----------
    IEnumerator DockPlayer()
    {
        EnsurePlayer();
        if (!playerCarry)
        {
            Debug.LogError("[Drone] DockPlayer(): kein PlayerCarryMount.");
            yield break;
        }

        Debug.Log("[Drone] Docking…");
        // Drohne fährt bis über den Player-Socket
        Vector3 target = playerCarry.carrySocket.position + (dockClamp.position - transform.position);
        float t = 0f;
        Vector3 from = transform.position;
        while (t < dockDuration)
        {
            float k = t / dockDuration;
            transform.position = Vector3.Lerp(from, target, k);
            ThrottleFX(0.4f + 0.3f * k);
            t += Time.deltaTime;
            yield return null;
        }
        transform.position = target;

        // Player attachen
        var clampNet = dockClamp.GetComponent<NetworkObject>();
        if (!clampNet)
        {
            Debug.LogError("[Drone] DockClamp ohne NetworkObject → Abbruch.");
            yield break;
        }

        playerCarry.BeginCarryServerRpc(clampNet);
        Debug.Log("[Drone] BeginCarryServerRpc geschickt.");

        // HARD-LOCK aktivieren: solange Carried, richtung DockClamp halten (Server-seitig)
        if (carryLockCo != null) StopCoroutine(carryLockCo);
        carryLockCo = StartCoroutine(CarryHardLock());
    }

    void UndockPlayer()
    {
        if (carryLockCo != null) { StopCoroutine(carryLockCo); carryLockCo = null; }
        if (!playerCarry) return;

        playerCarry.EndCarryServerRpc();
        Debug.Log("[Drone] EndCarryServerRpc geschickt.");

        // ===> EnemySpawner triggern
        if (IsServer)
        {
            var spawner = FindObjectOfType<EnemySpawner>();
            if (spawner != null)
            {
                //spawner.AutoStartWaveServerRpc();
                Debug.Log("[Drone] EnemySpawner AutoStartWaveServerRpc aufgerufen aber nicht aktiv.");
            }
        }
    }

    /// <summary>
    /// Hält den Player während des Carries **framegenau** an der korrekten Pose, 
    /// um Interpolations-Drift zu vermeiden (nur Server).
    /// </summary>
    IEnumerator CarryHardLock()
    {
        Debug.Log("[Drone] CarryHardLock gestartet.");
        while (playerCarry != null && playerCarry.IsCarried)
        {
            // Falls jemand lokal was verschiebt o.ä., korrigieren wir es sofort:
            AlignPlayerLocalToClamp();
            yield return null; // pro Frame
        }
        Debug.Log("[Drone] CarryHardLock beendet.");
    }

    /// <summary>
    /// Erzwingt lokal (unter DockClamp) die Pose, sodass carrySocket == Origin.
    /// (Gleiche Rechnung wie beim Snap, aber kontinuierlich.)
    /// </summary>
    void AlignPlayerLocalToClamp()
    {
        if (!playerCarry || !playerCarry.IsCarried) return;

        var socket = playerCarry.carrySocket;
        if (!socket) return;

        var invRot = Quaternion.Inverse(socket.localRotation);
        playerCarry.transform.localRotation = invRot;
        playerCarry.transform.localPosition = -(invRot * socket.localPosition);
    }

    // ---------- Movement / FX ----------
    void MoveTowards(Vector3 target, float speed, bool faceTarget)
    {
        Vector3 dir  = (target - transform.position);
        Vector3 step = dir.normalized * speed * Time.deltaTime;
        if (step.sqrMagnitude > dir.sqrMagnitude) step = dir;
        transform.position += step;

        if (faceTarget && step.sqrMagnitude > 0.0001f)
        {
            var look = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, 10f * Time.deltaTime);
        }
    }

    IEnumerator MoveTimed(Vector3 from, Vector3 to, float duration, AnimationCurve curve, float pitchBoost, float forwardTilt)
    {
        float t = 0f;
        var baseRot = transform.rotation;
        var fwdRot  = baseRot * Quaternion.Euler(forwardTilt, 0f, 0f);

        while (t < duration)
        {
            float k = curve.Evaluate(t / duration);
            transform.position = Vector3.Lerp(from, to, k);
            transform.rotation = Quaternion.Slerp(baseRot, fwdRot, k);
            ThrottleFX(Mathf.Lerp(0.7f, 1.0f, k));
            t += Time.deltaTime;
            yield return null;
        }
        transform.position = to;
        transform.rotation = baseRot;
    }

    IEnumerator MoveSegment(Vector3 a, Vector3 b, float speed)
    {
        float dist = Vector3.Distance(a, b);
        float traveled = 0f;
        while (traveled < dist)
        {
            float step = speed * Time.deltaTime;
            traveled += step;
            float k = Mathf.Clamp01(traveled / dist);

            transform.position = Vector3.Lerp(a, b, k);

            Vector3 lookDir = (b - transform.position).normalized;
            Quaternion targetRot = Quaternion.LookRotation(lookDir, Vector3.up);
            float bank = bankAngle * Mathf.Sin(k * Mathf.PI);
            targetRot *= Quaternion.Euler(tiltForward, 0, -bank);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 8f * Time.deltaTime);

            ThrottleFX(Mathf.Lerp(0.8f, 1.0f, Mathf.Sin(k * Mathf.PI)));
            yield return null;
        }
        transform.position = b;
    }

    void ThrottleFX(float intensity)
    {
        if (vfxThrusterL) vfxThrusterL.SetActive(intensity > 0.05f);
        if (vfxThrusterR) vfxThrusterR.SetActive(intensity > 0.05f);
        if (thrusterLightL) thrusterLightL.intensity = Mathf.Lerp(0f, 7000f, intensity);
        if (thrusterLightR) thrusterLightR.intensity = Mathf.Lerp(0f, 7000f, intensity);
    }
}
