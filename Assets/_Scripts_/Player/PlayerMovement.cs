using System.Collections;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float acceleration = 10f;
    public float deceleration = 8f;
    [Range(0f, 1f)] public float airControl = 0.5f;

    [Header("Jump & Dash (Base)")]
    [Tooltip("Start-DeltaV nach oben (m/s), massenunabhängig.")]
    public float jumpForce = 6.5f;

    [Header("Dash 2.0 (nur Physik/VFX, keine Anim)")]
    public float dashForce = 12f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 0.6f;
    public int maxAirDashes = 1;
    public AnimationCurve dashSpeedCurve = null; // wird bei Bedarf gesetzt
    [Range(0f, 1f)] public float dashGravityFactor = 0.15f;
    public bool keepVerticalVelocityOnDash = false;
    [Range(0.5f, 1.0f)] public float dashEndHorizontalDamping = 0.85f;

    // --- Grounded (Variante A: Probe unter BoxCollider) ---
    [Header("Grounded (BoxCollider-Probe) — ohne Grace")]
    [SerializeField] private LayerMask groundLayer;   // Terrain/“Base”-Layer rein
    [SerializeField] private float groundProbeExtra = 0.08f; // wie tief wir unter die Kugel tasten
    [SerializeField] private float maxSlopeDeg = 65f;// Boden bis zu dieser Steilheit

    private SphereCollider sc;


    [Header("Better Jump (Gravity)")]
    public float gravityScale = 1.0f;
    public float upGravityMultiplierHeld = 1.15f;
    public float upGravityMultiplierReleased = 2.0f;
    public float downGravityMultiplier = 2.6f;
    public float apexBonusMultiplier = 1.15f;
    public float apexThreshold = 0.75f;
    public float maxFallSpeed = 30f;

    [Header("Jump Leniency")]
    public float coyoteTime = 0.1f;
    public float jumpBufferTime = 0.12f;


    // ===== NEW: Server-cached Zustände, die andere Systeme lesen dürfen =====
    public bool ServerGrounded { get; private set; }
    public bool ServerRollHeld => srvRollHeld;       // bereits server-seitig geführt
    public float ServerHorizontalSpeed { get; private set; }


    // öffentliches Standing-Flag (auf Basis eines Schwellwerts)
    [Header("Energy Drain Settings (Movement -> Battery)")]
    [Tooltip("Horizontale Geschw.-Schwelle, unter der wir 'Stehen' annehmen (m/s)")]
    public float standingSpeedThreshold = 0.15f;     // deckt sich mit Walk_Anim-Threshold
    public bool ServerStanding { get; private set; } // Grounded & sehr geringe Bewegung


    [Header("Animation")]
    [Tooltip("Animator am RobotModel (Child). Erwartet Bools: Walk_Anim, Roll_Anim.")]
    public Animator anim;                     // im Inspector setzen
    private NetworkAnimator netAnim;          // nicht zwingend benötigt für Bools, aber ok zu haben

    [Header("Turning")]
    public float turnSpeedDegPerSec = 720f;   // Server-seitig, sanftes Mitdrehen
    private Vector3 desiredLookDir;           // nur horizontal (XZ)

    [Header("Roll (LSHIFT gehalten)")]
    [Tooltip("Faktor auf die Laufgeschwindigkeit, solange Roll_Anim aktiv ist.")]
    public float rollSpeedMultiplier = 2f;

    [Header("Roll in Air")]
    public bool keepRollBoostInAir = true;          // Boost in der Luft behalten
    public bool keepMomentumWhileRollAir = true;    // horizontales Tempo nicht aktiv abbremsen
    public float minAirControlWhileRoll = 0.95f;    // AirControl beim Rollen fast wie am Boden

    [Tooltip("Beschleunigung/Abbremsen beim Rollen ebenfalls skalieren?")]
    public bool scaleAccelWhileRoll = true;

    [Header("Look")]
    public bool lookWithCameraWhenNoMove = true; // auch ohne Move-Input drehen (z. B. in der Luft)

    [Header("Facing")]
    public bool faceMovementDirection = true;   // bei Bewegung in Lauf-Richtung ausrichten

    [Header("Audio")]
    [Tooltip("AudioSource am Player (3D, Spatialize, Doppler=0)")]
    public AudioSource audioSource;

    [Tooltip("Random Footstep Clips für's Laufen")]
    public AudioClip[] footstepClips;

    [Tooltip("Zeit zwischen Schritten bei normalem Tempo (Sekunden)")]
    public float baseStepInterval = 0.45f;

    [Tooltip("Minimaler/Maximaler Pitch-Jitter für Varianz")]
    [Range(0.7f, 1.3f)] public float footstepPitchMin = 0.92f;
    [Range(0.7f, 1.3f)] public float footstepPitchMax = 1.08f;

    [Tooltip("Start-OneShot beim Beginn des Rollens")]
    public AudioClip rollStartClip;

    [Tooltip("Optionales Loop-Geräusch während des Rollens (z.B. Rutschen)")]
    public AudioClip rollLoopClip;
    public float rollLoopVolume = 0.6f;

    [Header("Upgrades")]
    [SerializeField] private OverclockRuntime overclocks;

    // Runtime
    private Rigidbody rb;
    private bool isDashing;
    private float dashElapsed;
    private float nextDashAllowedTime;
    private int airDashesLeft;
    private Vector3 dashDir;
    private bool invulnerable;

    // Audio
    private Coroutine footstepRoutine;
    private bool footstepRunning;
    private Vector3 lastPosForSpeed;
    private AudioSource rollLoopSource;  // eigener Source für Loop

    // Input-States (Server-seitig)
    private bool srvRollHeld;                 // LSHIFT gehalten → Roll_Anim==true
    private bool srvJumpHeld;                 // Space gehalten
    private float lastGroundedTime = -999f;
    private float lastJumpPressedTime = -999f;
    private bool jumpedThisFrame;
    private bool wasGroundedLastFrame;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        sc = GetComponent<SphereCollider>();

        if (dashSpeedCurve == null || dashSpeedCurve.length == 0)
            dashSpeedCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

        if (anim == null)
            anim = GetComponentInChildren<Animator>(true);

        if (anim != null)
            netAnim = anim.GetComponent<NetworkAnimator>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            CameraFollow.Instance?.SetTarget(transform);
        }
        else
        {
            var cam = GetComponentInChildren<Camera>();
            if (cam) cam.enabled = false;
            var audio = GetComponentInChildren<AudioListener>();
            if (audio) audio.enabled = false;
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        // --- Input lesen ---
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");
        Vector2 rawInput = new Vector2(moveX, moveZ);

        // --- Kamera-Rotation einrechnen ---
        Vector3 moveDir;
        if (Camera.main != null)
        {
            Transform cam = Camera.main.transform;
            Vector3 camForward = cam.forward; camForward.y = 0; camForward.Normalize();
            Vector3 camRight = cam.right; camRight.y = 0; camRight.Normalize();
            moveDir = (camRight * rawInput.x + camForward * rawInput.y).normalized;
        }
        else
        {
            moveDir = new Vector3(rawInput.x, 0, rawInput.y).normalized;
        }

        // Movement Input senden (Owner → Server)
        SendMoveInputServerRpc(new Vector2(moveDir.x, moveDir.z));

        // >>> Facing (auch ohne Move-Input drehen)
        if (faceMovementDirection)
        {
            if (moveDir.sqrMagnitude > 0.0001f)
            {
                // klassische Ausrichtung auf Bewegungsrichtung
                SetLookDirServerRpc(new Vector2(moveDir.x, moveDir.z));
            }
            else if (lookWithCameraWhenNoMove && Camera.main != null)
            {
                // keine Bewegung → auf Kamera blicken (Drehung in der Luft / im Stand)
                var cam = Camera.main.transform;
                Vector3 camForward = cam.forward; camForward.y = 0; camForward.Normalize();
                if (camForward.sqrMagnitude > 0.0001f)
                    SetLookDirServerRpc(new Vector2(camForward.x, camForward.z));
            }
        }

        // Jump: Press & Held (Owner → Server)
        if (Input.GetKeyDown(KeyCode.Space))
            NotifyJumpPressedServerRpc();
        SetJumpHeldServerRpc(Input.GetKey(KeyCode.Space));

        // Roll-Anim (Owner → Server): solange LSHIFT gehalten wird
        SetRollHeldServerRpc(Input.GetKey(KeyCode.LeftShift));

        // Dash (Owner → Server): nur auf KeyDown (keine Anim)
        if (Input.GetKeyDown(KeyCode.LeftShift))
            DashServerRpc(new Vector2(moveDir.x, moveDir.z));

        // Optional: „Open_Anim“ Toggle (Owner → Server)
        if (Input.GetKeyDown(KeyCode.LeftControl))
            ToggleOpenAnimServerRpc();
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;

        jumpedThisFrame = false;

        // Grounded-Tracking
        bool groundedNow = IsGrounded();
        if (groundedNow)
            lastGroundedTime = Time.time;

        // Air-Dash wiederherstellen beim Landen
        if (groundedNow && !wasGroundedLastFrame)
            airDashesLeft = maxAirDashes;
        wasGroundedLastFrame = groundedNow;

        ServerGrounded = groundedNow;

        // Gepufferten Sprung ggf. ausführen
        TryConsumeBufferedJump();

        // Dash Movement (Kurve + reduzierte Grav)
        if (isDashing)
        {
            dashElapsed += Time.fixedDeltaTime;
            float t = Mathf.Clamp01(dashElapsed / dashDuration);

            float speed = dashForce * dashSpeedCurve.Evaluate(t);
            Vector3 v = rb.linearVelocity;
            Vector3 horiz = new Vector3(v.x, 0f, v.z);
            Vector3 targetHoriz = dashDir * speed;

            Vector3 diff = targetHoriz - horiz;
            rb.AddForce(diff, ForceMode.Acceleration);

            if (dashElapsed >= dashDuration)
            {
                isDashing = false;
                invulnerable = false;

                // weiches Ausrollen (nur horizontal)
                Vector3 vel = rb.linearVelocity;
                Vector3 outHoriz = new Vector3(vel.x, 0, vel.z) * dashEndHorizontalDamping;
                rb.linearVelocity = new Vector3(outHoriz.x, vel.y, outHoriz.z);
            }
        }

        // sanft in Blickrichtung drehen (nur Yaw)
        if (desiredLookDir.sqrMagnitude > 0.0001f)
        {
            float targetY = Quaternion.LookRotation(desiredLookDir, Vector3.up).eulerAngles.y;
            Quaternion yOnly = Quaternion.Euler(0f, targetY, 0f);
            float maxStep = turnSpeedDegPerSec * Time.fixedDeltaTime;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, yOnly, maxStep);
        }

        // Rotation aufrecht halten
        Vector3 euler = transform.rotation.eulerAngles;
        transform.rotation = Quaternion.Euler(0f, euler.y, 0f);

        // Gravitation anpassen
        ApplyBetterGravity();

        // Animator-Parameter setzen (nur Walk/ Roll)
        UpdateAnimatorServer();

        Vector3 rbVel = rb.linearVelocity;
        ServerHorizontalSpeed = new Vector3(rbVel.x, 0f, rbVel.z).magnitude;
        ServerStanding = ServerGrounded && ServerHorizontalSpeed < standingSpeedThreshold;
    }


    private bool IsGrounded()
    {
        if (!sc) return false;

        // Maske: Boden, eigenen Layer ausschließen
        int mask = groundLayer.value == 0 ? ~0 : groundLayer.value;
        mask &= ~(1 << gameObject.layer);

        // Welt-Radius/-Center
        float worldRadius = sc.radius * Mathf.Max(
            Mathf.Abs(sc.transform.lossyScale.x),
            Mathf.Abs(sc.transform.lossyScale.y),
            Mathf.Abs(sc.transform.lossyScale.z)
        );
        Vector3 worldCenter = sc.transform.TransformPoint(sc.center);

        // Fußpunkt knapp über Unterkante
        float r = worldRadius * 0.98f;
        Vector3 feet = worldCenter + Vector3.down * (r - 0.005f);

        // 1) Wenn wir bereits den Boden berühren/überlappen → grounded
        if (Physics.CheckSphere(feet, r * 0.98f, mask, QueryTriggerInteraction.Ignore))
            return true;

        // 2) Sonst kurzer Ray nach unten (robust für Slope)
        float rayDist = r + Mathf.Max(0.04f, groundProbeExtra) + 0.06f;
        if (Physics.Raycast(worldCenter, Vector3.down, out var hit, rayDist, mask, QueryTriggerInteraction.Ignore))
        {
            float minY = Mathf.Cos(maxSlopeDeg * Mathf.Deg2Rad);
            Debug.Log("GROUNDED!");
            return hit.normal.y >= minY;
        }

        return false;
    }




    private void OnDrawGizmos()
    {
        if (!sc) sc = GetComponent<SphereCollider>();
        if (!sc) return;

        float worldRadius = sc.radius * Mathf.Max(
            Mathf.Abs(sc.transform.lossyScale.x),
            Mathf.Abs(sc.transform.lossyScale.y),
            Mathf.Abs(sc.transform.lossyScale.z)
        );
        Vector3 worldCenter = sc.transform.TransformPoint(sc.center);
        Vector3 origin = worldCenter + Vector3.down * (worldRadius - 0.01f);
        float distance = Mathf.Max(0.04f, groundProbeExtra);

        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(origin, worldRadius * 0.98f);
        Gizmos.color = Color.white;  Gizmos.DrawLine(origin, origin + Vector3.down * distance);
        Gizmos.color = Color.cyan;   Gizmos.DrawWireSphere(origin + Vector3.down * distance, worldRadius * 0.98f);
    }





    // ===== Movement (Server) =====
    [ServerRpc]
    private void SendMoveInputServerRpc(Vector2 input, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        if (rb == null || isDashing) return;

        Vector3 moveDirection = new Vector3(input.x, 0, input.y).normalized;

        bool grounded = IsGrounded();

        // 1) Control in der Luft: beim Rollen fast voll erhalten
        float control = grounded ? 1f :
            (keepRollBoostInAir && srvRollHeld ? Mathf.Max(airControl, minAirControlWhileRoll) : airControl);

        // 2) Speed-Multiplikator durch Rollen
        float speedMult = srvRollHeld ? rollSpeedMultiplier : 1f;

        // >>> SPIDER: Slow anwenden <<<
        float slow = 1f;
        var slowRecv = GetComponent<PlayerSlowReceiver>();
        if (slowRecv != null) slow = slowRecv.CurrentMultiplier;

        float ocMove = overclocks ? overclocks.GetMoveSpeedMult() : 1f;
        Vector3 targetVelocity = moveDirection * moveSpeed * speedMult * control * ocMove * slow;

        Vector3 currentVel   = rb.linearVelocity;
        Vector3 horizontalVel = new Vector3(currentVel.x, 0, currentVel.z);

        // 3) Beschleunigung / Abbremsen
        float accelRate = (targetVelocity.magnitude > 0.1f) ? acceleration : deceleration;

        // In der Luft beim Rollen: nicht aktiv abbremsen, lieber Momentum behalten
        if (!grounded && srvRollHeld)
        {
            accelRate = acceleration; // niemals deceleration erzwingen
            if (keepMomentumWhileRollAir)
            {
                // Wenn target langsamer als aktuelles Horizont-Tempo wäre: nicht bremsen
                if (targetVelocity.magnitude < horizontalVel.magnitude)
                {
                    // Nichts anwenden (kein Abbremsen)
                    return;
                }
            }
        }

        Vector3 velocityDiff = targetVelocity - horizontalVel;
        rb.AddForce(velocityDiff * accelRate, ForceMode.Acceleration);
    }


    // ===== Jump (Server) =====
    [ServerRpc]
    private void NotifyJumpPressedServerRpc(ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        lastJumpPressedTime = Time.time;
    }

    [ServerRpc]
    private void SetJumpHeldServerRpc(bool held, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        srvJumpHeld = held;
    }

    // ===== Roll-Hold (Server) =====
    [ServerRpc]
    private void SetRollHeldServerRpc(bool held, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        srvRollHeld = held;
    }

    // ===== Optional: Öffnen/Schließen (Server) =====
    [ServerRpc]
    private void ToggleOpenAnimServerRpc(ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        if (!anim) return;
        bool current = false;
        try { current = anim.GetBool("Open_Anim"); } catch { return; }
        anim.SetBool("Open_Anim", !current);
    }

    // ===== Client → Server: gewünschte Blickrichtung (von CameraFollow) =====
    [ServerRpc]
    public void SetLookDirServerRpc(Vector2 dir, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        Vector3 d = new Vector3(dir.x, 0f, dir.y);
        if (d.sqrMagnitude > 0.0001f) desiredLookDir = d.normalized;
    }

    // ===== Animator =====
    // Zustands-Cache (Server)
    private bool srvWasWalking;
    private bool srvWasRolling;

    private void UpdateAnimatorServer()
    {
        if (!anim) return;

        Vector3 rbVel = rb.linearVelocity;
        float horizontalSpeed = new Vector3(rbVel.x, 0, rbVel.z).magnitude;

        bool grounded = IsGrounded();
        bool walking = grounded && horizontalSpeed > 0.15f && !srvRollHeld;
        bool rolling = srvRollHeld;

        SafeSetBool(anim, "Walk_Anim", walking);
        SafeSetBool(anim, "Roll_Anim", rolling);

        // --- NEW: State Transitions -> Audio RPCs ---
        if (walking && !srvWasWalking)
            StartFootstepsClientRpc();          // Walk beginnt
        else if (!walking && srvWasWalking)
            StopFootstepsClientRpc();           // Walk endet

        if (!grounded && srvWasWalking)
            StopFootstepsClientRpc();

        if (rolling && !srvWasRolling)
        {
            StopFootstepsClientRpc();           // Sicherheit: Steps aus
            PlayRollStartClientRpc();           // Roll-Start + Loop
        }
        else if (!rolling && srvWasRolling)
        {
            StopRollLoopClientRpc();            // Roll-Loop aus
            // Falls direkt in Walk übergeht, startet oben sowieso wieder
        }

        srvWasWalking = walking;
        srvWasRolling = rolling;
    }


    private void EnsureAudioSources()
    {
        if (!audioSource)
            audioSource = GetComponentInChildren<AudioSource>();

        if (!rollLoopSource)
        {
            // separater Source für das Roll-Loop, damit OneShots nicht abgeschnitten werden
            rollLoopSource = gameObject.AddComponent<AudioSource>();
            rollLoopSource.playOnAwake = false;
            rollLoopSource.loop = true;
            rollLoopSource.spatialBlend = 1f;
            rollLoopSource.dopplerLevel = 0f;
        }
    }

    private IEnumerator FootstepLoop()
    {
        footstepRunning = true;
        lastPosForSpeed = transform.position;

        while (footstepRunning)
        {
            // NICHT in der Luft treten
            if (!IsGrounded())
            {
                yield return new WaitForSeconds(0.05f);
                continue;
            }

            // horizontale Geschwindigkeit lokal abschätzen (funktioniert auch auf Nicht-Ownern)
            Vector3 now = transform.position;
            Vector3 delta = now - lastPosForSpeed;
            lastPosForSpeed = now;

            float horizSpeed = new Vector3(delta.x, 0f, delta.z).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);

            // Falls kaum Bewegung: kein Step
            if (horizSpeed < 0.5f)
            {
                yield return new WaitForSeconds(0.05f);
                continue;
            }

            // Schritt-Intervall an Geschwindigkeit koppeln (clamp, damit bei Minibewegungen nix flackert)
            float speed01 = Mathf.InverseLerp(0.5f, moveSpeed * rollSpeedMultiplier, horizSpeed);
            float interval = Mathf.Lerp(baseStepInterval * 1.2f, baseStepInterval * 0.55f, speed01);

            PlayRandomFootstep();

            // kleine natürliche Varianz
            float jitter = Random.Range(-0.04f, 0.04f);
            yield return new WaitForSeconds(Mathf.Max(0.08f, interval + jitter));
        }
    }

    private void PlayRandomFootstep()
    {
        if (!audioSource || footstepClips == null || footstepClips.Length == 0) return;
        var clip = footstepClips[Random.Range(0, footstepClips.Length)];
        float pitch = Random.Range(footstepPitchMin, footstepPitchMax);

        audioSource.pitch = pitch;
        audioSource.PlayOneShot(clip, 1f);
    }

    private void StartFootstepsClient()
    {
        EnsureAudioSources();
        if (footstepRoutine == null)
            footstepRoutine = StartCoroutine(FootstepLoop());
    }

    private void StopFootstepsClient()
    {
        if (footstepRoutine != null)
        {
            footstepRunning = false;
            StopCoroutine(footstepRoutine);
            footstepRoutine = null;
        }
    }

    private void PlayRollStartClient()
    {
        EnsureAudioSources();
        if (rollStartClip && audioSource)
            audioSource.PlayOneShot(rollStartClip, 1f);

        if (rollLoopClip && rollLoopSource)
        {
            rollLoopSource.clip = rollLoopClip;
            rollLoopSource.volume = rollLoopVolume;
            if (!rollLoopSource.isPlaying)
                rollLoopSource.Play();
        }
    }

    private void StopRollLoopClient()
    {
        if (rollLoopSource && rollLoopSource.isPlaying)
            rollLoopSource.Stop();
    }

    [ClientRpc]
    private void StartFootstepsClientRpc()
    {
        StartFootstepsClient();
    }

    [ClientRpc]
    private void StopFootstepsClientRpc()
    {
        StopFootstepsClient();
    }

    [ClientRpc]
    private void PlayRollStartClientRpc()
    {
        PlayRollStartClient();
    }

    [ClientRpc]
    private void StopRollLoopClientRpc()
    {
        StopRollLoopClient();
    }

    private void OnDisable()
    {
        StopFootstepsClient();
        StopRollLoopClient();
    }


    private static void SafeSetBool(Animator a, string name, bool value)
    {
        // Vermeidet Console-Errors, falls Parameter im Controller fehlt
        foreach (var p in a.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Bool && p.name == name)
            {
                a.SetBool(name, value);
                return;
            }
        }
        // falls nicht vorhanden: ignoriere still
    }

    private void TryConsumeBufferedJump()
    {
        if (jumpedThisFrame) return;

        bool canCoyote = (Time.time - lastGroundedTime) <= coyoteTime;
        bool hasBuffer = (Time.time - lastJumpPressedTime) <= jumpBufferTime;

        if (hasBuffer && canCoyote && !isDashing)
        {
            lastJumpPressedTime = -999f;
            DoJump();
        }
    }

    private void DoJump()
    {
        if (!rb) return;

        Vector3 v = rb.linearVelocity; v.y = 0f;
        rb.linearVelocity = v;

        rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
        jumpedThisFrame = true;
    }

    // ===== Better Gravity (Server) =====
    private void ApplyBetterGravity()
    {
        if (!rb) return;

        if (isDashing)
        {
            Vector3 g = Physics.gravity * dashGravityFactor;
            rb.AddForce(g, ForceMode.Acceleration);

            if (maxFallSpeed > 0f && rb.linearVelocity.y < -maxFallSpeed)
            {
                Vector3 rbVel = rb.linearVelocity; rbVel.y = -maxFallSpeed; rb.linearVelocity = rbVel;
            }
            return;
        }

        float vy = rb.linearVelocity.y;
        float mult = gravityScale;

        if (vy > 0f) // aufwärts
            mult *= srvJumpHeld ? upGravityMultiplierHeld : upGravityMultiplierReleased;
        else if (vy < 0f) // abwärts
            mult *= downGravityMultiplier;

        if (Mathf.Abs(vy) < apexThreshold)
            mult *= apexBonusMultiplier;

        Vector3 extraG = Physics.gravity * (mult - 1f);
        if (extraG.sqrMagnitude > 0f)
            rb.AddForce(extraG, ForceMode.Acceleration);

        if (maxFallSpeed > 0f && rb.linearVelocity.y < -maxFallSpeed)
        {
            Vector3 rbVel2 = rb.linearVelocity; rbVel2.y = -maxFallSpeed; rb.linearVelocity = rbVel2;
        }
    }

    // ===== Dash (Server) – nur Physik/VFX =====
    [ServerRpc]
    private void DashServerRpc(Vector2 input, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        if (!rb) return;

        if (Time.time < nextDashAllowedTime) return;

        bool grounded = IsGrounded();
        if (!grounded)
        {
            if (airDashesLeft <= 0) return;
            airDashesLeft--;
        }

        dashDir = new Vector3(input.x, 0, input.y).normalized;
        if (dashDir == Vector3.zero) dashDir = transform.forward;

        isDashing = true;
        dashElapsed = 0f;
        invulnerable = true;
        nextDashAllowedTime = Time.time + dashCooldown;

        Vector3 rbVel = rb.linearVelocity;
        if (!keepVerticalVelocityOnDash) rbVel.y = 0f;
        rb.linearVelocity = rbVel;

        rb.AddForce(dashDir * (0.35f * dashForce), ForceMode.VelocityChange);
    }
}