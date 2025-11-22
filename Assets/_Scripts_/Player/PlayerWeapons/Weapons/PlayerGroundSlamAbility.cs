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

    [Tooltip("Basisgeschwindigkeit des Slams entlang der Slam-Richtung (m/s, positiv).")]
    public float slamDownVelocity = 100f;

    [Tooltip("Cooldown für die Fähigkeit (Sekunden).")]
    public float slamCooldown = 8f;



    [Header("Timing")]
    [Tooltip("Maximale Dauer der Aufwärtsphase, bevor wir auf jeden Fall in den Slam übergehen.")]
    public float maxSuperJumpTime = 1.0f;

    [Tooltip("Kurze Verzögerung nach Erreichen des Scheitelpunkts, bevor der Slam startet.")]
    public float autoSlamAfterApexDelay = 0.05f;



    [Header("Directional Slam")]
    [Tooltip("Falls true, richtet der Slam sich nach vorne-unten statt nur straight down.")]
    public bool directionalSlam = true;

    [Tooltip("Winkel unterhalb der Horizontalen (0 = waagerecht, 90 = straight down).")]
    [Range(0f, 89f)]
    public float slamAngleFromHorizontal = 45f;


    [Header("Impact")]
    [Tooltip("Optionales VFX beim Einschlag.")]
    public GameObject slamImpactVfx;

    [Tooltip("Einmaliger kleiner Hop nach dem Einschlag.")]
    public float slamImpactUpKick = 2f;
    public float slamImpactRadius = 6f;
    public float slamImpactDamage = 10f;
    public LayerMask enemyLayers;




    [Header("Audio")]
    [Tooltip("AudioSource am Player (3D, Spatialize, Doppler=0). Wenn leer, wird versucht, eine zu finden.")]
    public AudioSource audioSource;

    [Tooltip("Sound beim Start des Super-Jumps (vom Boden aus).")]
    public AudioClip superJumpSfx;

    [Tooltip("Sound beim Start des Slams nach unten (egal ob vom Apex oder aus der Luft).")]
    public AudioClip slamStartSfx;

    [Tooltip("Sound beim Impact auf dem Boden.")]
    public AudioClip slamImpactSfx;

    [Tooltip("Lautstärke der Slam-SFX.")]
    [Range(0f, 1f)] public float slamSfxVolume = 1f;

    private PlayerMovement _movement;
    private Rigidbody _rb;

    private bool _superJumpActive;
    private bool _slamDescending;
    private float _nextSlamAllowedTime;

    private bool _wasGroundedLastFrame;
    private float _superJumpStartTime = -999f;
    private float _apexReachedTime = -999f;

    // aktuelle Slam-Richtung (z.B. 45° nach vorn-unten)
    private Vector3 _currentSlamDir = Vector3.down;

    private void Awake()
    {
        _movement = GetComponent<PlayerMovement>();
        _rb       = GetComponent<Rigidbody>();
        EnsureAudioSource();
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
            Debug.Log("[Slam][CLIENT] Slam key pressed, sending RPC.");
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

            if (vy <= 0f && _apexReachedTime < 0f)
            {
                _apexReachedTime = Time.time;
            }

            if ((vy <= 0f && _apexReachedTime > 0f &&
                 Time.time >= _apexReachedTime + autoSlamAfterApexDelay)
                ||
                (Time.time >= _superJumpStartTime + maxSuperJumpTime))
            {
                StartSlamDown();
            }
        }

        // ===============================
        // Während Slam: min. Speed entlang Slam-Richtung
        // ===============================
        if (_slamDescending)
        {
            Vector3 v   = _rb.linearVelocity;
            Vector3 dir = _currentSlamDir.sqrMagnitude > 0.0001f
                ? _currentSlamDir.normalized
                : Vector3.down;

            // Anteil der Geschwindigkeit entlang der Slam-Richtung
            float along = Vector3.Dot(v, dir);
            // seitliche/tangentiale Komponente (wird nicht gekillt → Steering möglich)
            Vector3 tangent = v - along * dir;

            float minSpeed = Mathf.Abs(slamDownVelocity);
            if (along < minSpeed)
            {
                along = minSpeed;
            }

            _rb.linearVelocity = dir * along + tangent;
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

        // Wenn wir bereits in einem Superjump sind (aufwärtsphase) und der Spieler nochmal drückt:
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
            // In der Luft: sofort Slam nach unten / vorne-unten
            StartSlamDownDirect();
        }

        _nextSlamAllowedTime = Time.time + slamCooldown;
    }

    private void StartSuperJump()
    {
        Debug.Log("[Slam] StartSuperJump");

        if (_movement != null)
        {
            _movement.externalBlockDashAndRoll    = true;
            _movement.externalBlockHover          = true;
            _movement.externalIgnoreMaxFallSpeed  = true;
            _movement.externalForceFullAirControl = false; // beim Hochfliegen noch normal
        }

        _superJumpActive     = true;
        _slamDescending      = false;
        _superJumpStartTime  = Time.time;
        _apexReachedTime     = -999f;

        Vector3 v = _rb.linearVelocity;
        v.y = 0f;
        _rb.linearVelocity = v;

        _rb.AddForce(Vector3.up * superJumpUpVelocity, ForceMode.VelocityChange);

        PlaySuperJumpSfxClientRpc();
    }

    /// <summary>
    /// Vom Apex in den Slam.
    ///</summary>
    private void StartSlamDown()
    {
        Debug.Log("[Slam] StartSlamDown (from apex)");

        if (_movement != null)
        {
            _movement.externalBlockDashAndRoll    = true;
            _movement.externalBlockHover          = true;
            _movement.externalIgnoreMaxFallSpeed  = true;
            _movement.externalForceFullAirControl = true; // volle Steuerung während Slam
        }

        _slamDescending  = true;
        _superJumpActive = true;

        _currentSlamDir    = GetSlamDirection();
        _rb.linearVelocity = _currentSlamDir * Mathf.Abs(slamDownVelocity);

        PlaySlamStartSfxClientRpc();
    }

    /// <summary>
    /// Sofortiger Slam (z.B. in der Luft ausgelöst).
    /// </summary>
    private void StartSlamDownDirect()
    {
        Debug.Log("[Slam] StartSlamDownDirect (air slam or forced)");

        if (_movement != null)
        {
            _movement.externalBlockDashAndRoll    = true;
            _movement.externalBlockHover          = true;
            _movement.externalIgnoreMaxFallSpeed  = true;
            _movement.externalForceFullAirControl = true;
        }

        _superJumpActive = false;
        _slamDescending  = true;

        _currentSlamDir    = GetSlamDirection();
        _rb.linearVelocity = _currentSlamDir * Mathf.Abs(slamDownVelocity);

        PlaySlamStartSfxClientRpc();
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
            _movement.externalBlockDashAndRoll    = false;
            _movement.externalBlockHover          = false;
            _movement.externalIgnoreMaxFallSpeed  = false;
            _movement.externalForceFullAirControl = false;
        }

        // kleiner Hop nach oben
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
            Destroy(go, 6f);
        }

        // ======================
        // AoE DAMAGE (SERVER)
        // ======================
        if (IsServer && slamImpactDamage > 0f && slamImpactRadius > 0f)
        {
            Vector3 center = transform.position;

            // alle Collider im Radius
            Collider[] hits = Physics.OverlapSphere(
                center,
                slamImpactRadius,
                enemyLayers,                // nur Enemy-Layer
                QueryTriggerInteraction.Ignore
            );

            // damit wir einen Gegner nicht mehrfach treffen (falls mehrere Collider)
            var alreadyHit = new System.Collections.Generic.HashSet<IEnemy>();

            foreach (var col in hits)
            {
                if (col == null) continue;

                // IEnemy kann auf dem gleichen Objekt oder Parent hängen
                IEnemy enemy = col.GetComponentInParent<IEnemy>();
                if (enemy == null) continue;
                if (alreadyHit.Contains(enemy)) continue;

                alreadyHit.Add(enemy);

                // Hitpoint = nächster Punkt des Colliders zur Mitte
                Vector3 hitPoint = col.ClosestPoint(center);

                // OwnerClientId ist der Angreifer
                enemy.TakeDamage(slamImpactDamage, OwnerClientId, hitPoint);
            }
        }

        // (optional noch SFX, falls du welche hast)
        PlaySlamImpactSfxClientRpc();
    }


    // =========================
    //   Richtung für Slam
    // =========================

    private Vector3 GetSlamDirection()
    {
        if (!directionalSlam)
            return Vector3.down;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        forward.Normalize();

        float rad = slamAngleFromHorizontal * Mathf.Deg2Rad;

        Vector3 dir = forward * Mathf.Cos(rad) + Vector3.down * Mathf.Sin(rad);
        return dir.normalized;
    }

    // =========================
    //   AUDIO-HILFSMETHODEN
    // =========================

    private void EnsureAudioSource()
    {
        if (audioSource != null) return;

        audioSource = GetComponentInChildren<AudioSource>();

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.dopplerLevel = 0f;
        }
    }

    private void PlayClipLocal(AudioClip clip)
    {
        if (clip == null) return;

        EnsureAudioSource();
        if (audioSource == null) return;

        audioSource.PlayOneShot(clip, slamSfxVolume);
    }

    [ClientRpc]
    private void PlaySuperJumpSfxClientRpc()
    {
        PlayClipLocal(superJumpSfx);
    }

    [ClientRpc]
    private void PlaySlamStartSfxClientRpc()
    {
        PlayClipLocal(slamStartSfx);
    }

    [ClientRpc]
    private void PlaySlamImpactSfxClientRpc()
    {
        PlayClipLocal(slamImpactSfx);
    }
}
