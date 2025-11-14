using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(PlayerMovement))]
public class PlayerGroundSlamAbility : NetworkBehaviour
{
    [Header("Input")]
    public KeyCode slamKey = KeyCode.F;

    [Header("Super Jump / Slam")]
    [Tooltip("Aufwärtsgeschwindigkeit beim Super-Sprung (m/s). ~40 ≈ ~80-100m bei Standardgravity.")]
    public float superJumpUpVelocity = 100f;

    [Tooltip("Abwärtsgeschwindigkeit beim Slam (m/s, POSITIV eintragen!).")]
    public float slamDownVelocity = 100f;

    [Tooltip("Cooldown für die Fähigkeit (Sekunden).")]
    public float slamCooldown = 8f;

    [Header("Timing")]
    [Tooltip("Maximale Dauer der Aufwärtsphase, bevor wir auf jeden Fall in den Slam übergehen.")]
    public float maxSuperJumpTime = 1.0f;

    [Tooltip("Kurze Verzögerung nach Erreichen des Scheitelpunkts, bevor der Slam startet.")]
    public float autoSlamAfterApexDelay = 0.05f;

    [Header("Impact")]
    [Tooltip("Optionales VFX beim Einschlag.")]
    public GameObject slamImpactVfx;

    [Tooltip("Einmaliger kleiner Hop nach dem Einschlag.")]
    public float slamImpactUpKick = 2f;

    [Tooltip("Radius für den AoE-Effekt (optional, noch nicht benutzt).")]
    public float slamImpactRadius = 6f;

    private PlayerMovement _movement;
    private Rigidbody _rb;

    private bool _superJumpActive;
    private bool _slamDescending;
    private float _nextSlamAllowedTime;

    private bool _wasGroundedLastFrame;
    private float _superJumpStartTime = -999f;
    private float _apexReachedTime = -999f;

    private void Awake()
    {
        _movement = GetComponent<PlayerMovement>();
        _rb       = GetComponent<Rigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (_movement != null)
        {
            _wasGroundedLastFrame = _movement.ServerGrounded;
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        if (Input.GetKeyDown(slamKey))
        {
            Debug.Log("[Slam][CLIENT] F pressed, sending RPC.");
            RequestGroundSlamServerRpc();
        }
    }

    private void FixedUpdate()
    {
        if (!IsServer || _movement == null || _rb == null) return;

        bool groundedNow = _movement.ServerGrounded;

        // Landen nach Slam → Impact
        if (groundedNow && !_wasGroundedLastFrame && _slamDescending)
        {
            DoSlamImpact();
        }

        _wasGroundedLastFrame = groundedNow;

        // ===============================
        // Superjump → automatisch in Slam
        // ===============================
        if (_superJumpActive && !_slamDescending)
        {
            float vy = _rb.linearVelocity.y;

            // Scheitelpunkt merken
            if (vy <= 0f && _apexReachedTime < 0f)
            {
                _apexReachedTime = Time.time;
            }

            // Wenn wir fallen ODER zu lange in der Luft sind → Slam starten
            if ((vy <= 0f && _apexReachedTime > 0f &&
                 Time.time >= _apexReachedTime + autoSlamAfterApexDelay)
                ||
                (Time.time >= _superJumpStartTime + maxSuperJumpTime))
            {
                StartSlamDown();
            }
        }

        // ===============================
        // Während Slam: Downward-Speed erzwingen
        // ===============================
        if (_slamDescending)
        {
            Vector3 v = _rb.linearVelocity;
            float targetVy = -Mathf.Abs(slamDownVelocity);   // immer nach unten

            // Wenn wir nicht schnell genug nach unten fallen, korrigieren
            if (v.y > targetVy)
            {
                v.y = targetVy;
                _rb.linearVelocity = v;
            }
        }
    }

    // =========================
    //   RPC & Ablauf
    // =========================

    [ServerRpc]
    private void RequestGroundSlamServerRpc(ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        if (_rb == null || _movement == null) return;

        Debug.Log($"[Slam] Request from client {rpcParams.Receive.SenderClientId}. " +
                  $"Grounded={_movement.ServerGrounded}, vY={_rb.linearVelocity.y}, " +
                  $"superJumpActive={_superJumpActive}, slamDescending={_slamDescending}");

        // Wenn wir bereits in einem Superjump sind (aufwärtsphase) und der Spieler nochmal F drückt:
        // sofort in Slam übergehen.
        if (_superJumpActive && !_slamDescending)
        {
            Debug.Log("[Slam] Force slam while in super jump.");
            StartSlamDownDirect();
            return;
        }

        // Cooldown
        if (Time.time < _nextSlamAllowedTime)
        {
            Debug.Log("[Slam] On cooldown.");
            return;
        }

        if (_movement.ServerGrounded)
        {
            // Vom Boden aus: erst Superjump, dann Auto-Slam
            StartSuperJump();
        }
        else
        {
            // In der Luft: sofort Slam nach unten
            StartSlamDownDirect();
        }

        _nextSlamAllowedTime = Time.time + slamCooldown;
    }

    private void StartSuperJump()
    {
        Debug.Log("[Slam] StartSuperJump");

        if (_movement != null)
        {
            _movement.externalBlockDashAndRoll   = true;
            _movement.externalBlockHover         = true;
            _movement.externalIgnoreMaxFallSpeed = true;   // damit MaxFallSpeed nicht capped
        }

        _superJumpActive     = true;
        _slamDescending      = false;
        _superJumpStartTime  = Time.time;
        _apexReachedTime     = -999f;

        // Vertikale Geschwindigkeit resetten
        Vector3 v = _rb.linearVelocity;
        v.y = 0f;
        _rb.linearVelocity = v;

        // Superjump nach oben
        _rb.AddForce(Vector3.up * superJumpUpVelocity, ForceMode.VelocityChange);
    }

    /// <summary>
    /// Wird vom Apex aus aufgerufen (automatisch).
    /// </summary>
    private void StartSlamDown()
    {
        Debug.Log("[Slam] StartSlamDown (from apex)");

        if (_movement != null)
        {
            _movement.externalBlockDashAndRoll   = true;
            _movement.externalBlockHover         = true;
            _movement.externalIgnoreMaxFallSpeed = true;
        }

        _slamDescending  = true;
        _superJumpActive = true; // wir sind noch in der Fähigkeit

        Vector3 v = _rb.linearVelocity;
        v.y = -Mathf.Abs(slamDownVelocity);
        _rb.linearVelocity = v;
    }

    /// <summary>
    /// Sofortiger Slam (z.B. in der Luft BEGINNEN).
    /// </summary>
    private void StartSlamDownDirect()
    {
        Debug.Log("[Slam] StartSlamDownDirect (air slam or forced)");

        if (_movement != null)
        {
            _movement.externalBlockDashAndRoll   = true;
            _movement.externalBlockHover         = true;
            _movement.externalIgnoreMaxFallSpeed = true;
        }

        _superJumpActive = false;
        _slamDescending  = true;

        Vector3 v = _rb.linearVelocity;
        v.y = -Mathf.Abs(slamDownVelocity);
        _rb.linearVelocity = v;
    }

    private void DoSlamImpact()
    {
        Debug.Log("[Slam] Impact");

        _superJumpActive = false;
        _slamDescending  = false;
        _superJumpStartTime = -999f;
        _apexReachedTime    = -999f;

        if (_movement != null)
        {
            _movement.externalBlockDashAndRoll   = false;
            _movement.externalBlockHover         = false;
            _movement.externalIgnoreMaxFallSpeed = false;
        }

        if (_rb != null && slamImpactUpKick != 0f)
        {
            var v = _rb.linearVelocity;
            if (v.y < 0f) v.y = slamImpactUpKick;
            _rb.linearVelocity = v;
        }

        // VFX
        if (slamImpactVfx != null)
        {
            var go = Instantiate(slamImpactVfx, transform.position, Quaternion.identity);
            Destroy(go, 5f);
        }

        // TODO: AoE-Schaden über slamImpactRadius usw.
    }
}
