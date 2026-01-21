using System.Collections;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class PlayerMovement : NetworkBehaviour
{
    // =========================
    // TUNING
    // =========================
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float acceleration = 12f;
    public float deceleration = 10f;
    [Range(0f, 1f)] public float airControl = 0.5f;

    [Header("Drift / Weight")]
    public float groundBrake = 10f;                       // bremst bei 0 input
    [Range(0.05f, 1f)] public float rollBrakeMult = 0.25f; // beim Rollen weniger bremsen
    public float lateralGrip = 22f;                       // normaler Grip
    public float rollLateralGrip = 7f;                    // beim Rollen weniger Grip => drift
    public float airLateralGrip = 1.2f;                   // in der Luft minimal
    [Range(0.1f, 1f)] public float rollSteerAccelMult = 0.65f; // weniger "Lenkbiss" beim Rollen
    public float rollMassMultiplier = 1.6f;               // optional: massiger beim Rollen

    [Header("Roll")]
    public float rollSpeedMultiplier = 2f;
    public float rollStaminaCostPerSecond = 25f;
    public float minStaminaToStartRoll = 5f;
    public bool keepMomentumWhileRollAir = true;          // beim Rollen in der Luft nicht aktiv abbremsen
    public float minAirControlWhileRoll = 0.95f;

    [Header("Jump")]
    public float jumpForce = 6.5f;

    [Header("Gravity")]
    public float gravityScale = 1.0f;
    public float maxFallSpeed = 30f;

    [Header("Dash")]
    public float dashForce = 12f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 0.6f;
    public int maxAirDashes = 1;
    public AnimationCurve dashSpeedCurve = null;
    [Range(0f, 1f)] public float dashGravityFactor = 0.15f;
    [Range(0.5f, 1.0f)] public float dashEndHorizontalDamping = 0.85f;
    public bool keepVerticalVelocityOnDash = false;

    [Header("Stamina")]
    public float maxStamina = 100f;
    public float staminaRegenPerSecond = 20f;
    public float dashStaminaCost = 30f;

    [Header("Grounded")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float groundProbeExtra = 0.08f;
    [SerializeField] private float maxSlopeDeg = 65f;

    [Header("Turning")]
    public float turnSpeedDegPerSec = 720f;

    [Header("Animation")]
    public Animator anim; // Parameter: MoveSpeed(float), IsRolling(bool)
    private int moveSpeedHash;
    private int isRollingHash;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip[] footstepClips;
    public float baseStepInterval = 0.45f;
    [Range(0.7f, 1.3f)] public float footstepPitchMin = 0.92f;
    [Range(0.7f, 1.3f)] public float footstepPitchMax = 1.08f;
    public AudioClip rollStartClip;
    public AudioClip rollLoopClip;
    public float rollLoopVolume = 0.6f;

    [Header("Upgrades (optional)")]
    [SerializeField] private OverclockRuntime overclocks;

    // =========================
    // PUBLIC SERVER STATE
    // =========================
    public bool ServerGrounded { get; private set; }
    public bool ServerRollHeld => srvRollHeld;
    public float ServerHorizontalSpeed { get; private set; }

    // =========================
    // NETVAR
    // =========================
    private readonly NetworkVariable<float> stamina = new NetworkVariable<float>(
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Server);

    public float CurrentStamina => stamina.Value;

    // =========================
    // RUNTIME
    // =========================
    private Rigidbody rb;
    private SphereCollider sc;
    private PlayerSlowReceiver slowReceiver;
    private float baseMass;
    private bool inputEnabled = true;


    public void SetInputEnabled(bool enabled) => inputEnabled = enabled;


    // input (server)
    private Vector2 srvMoveInput;        // world-space xz
    private bool srvRollInputHeld;

    // roll (server)
    private bool srvRollHeld;

    // Air roll animation after jump
    private bool airRollAnimActive;
    private bool wasGroundedLastFrame;

    // look dir (server)
    private Vector3 desiredLookDir;

    // dash (server)
    private bool isDashing;
    private float dashElapsed;
    private float nextDashAllowedTime;
    private int airDashesLeft;
    private Vector3 dashDir;

    // audio (all)
    private Coroutine footstepRoutine;
    private bool footstepRunning;
    private Vector3 lastPosForSpeed;
    private AudioSource rollLoopSource;

    // anim cache (server)
    private bool srvWasWalking;
    private bool srvWasRolling;

    // =========================
    // UNITY
    // =========================
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        sc = GetComponent<SphereCollider>();
        slowReceiver = GetComponent<PlayerSlowReceiver>();

        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        baseMass = rb.mass;

        if (dashSpeedCurve == null || dashSpeedCurve.length == 0)
            dashSpeedCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

        if (anim == null)
            anim = GetComponentInChildren<Animator>(true);

        if (anim != null)
        {
            moveSpeedHash = Animator.StringToHash("MoveSpeed");
            isRollingHash = Animator.StringToHash("IsRolling");
        }

        desiredLookDir = transform.forward;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            stamina.Value = Mathf.Clamp(maxStamina, 0f, maxStamina);
            airDashesLeft = maxAirDashes;
        }

        if (IsOwner)
        {
            CameraFollow.Instance?.SetTarget(transform);
            stamina.OnValueChanged += OnStaminaChangedOwner;
            PlayerHUD.Instance?.SetStamina(stamina.Value, maxStamina);
        }
        else
        {
            var cam = GetComponentInChildren<Camera>();
            if (cam) cam.enabled = false;
            var listener = GetComponentInChildren<AudioListener>();
            if (listener) listener.enabled = false;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner) stamina.OnValueChanged -= OnStaminaChangedOwner;
    }

    private void Update()
    {
        if (!IsOwner) return;
        if (!inputEnabled) return;

        // ----- Move input (camera-relative -> world) -----
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");
        Vector2 raw = Vector2.ClampMagnitude(new Vector2(x, z), 1f);

        Vector3 moveWorld = Vector3.zero;
        if (raw.sqrMagnitude > 0.0001f)
        {
            if (Camera.main != null)
            {
                Transform cam = Camera.main.transform;
                Vector3 f = cam.forward; f.y = 0; f.Normalize();
                Vector3 r = cam.right;   r.y = 0; r.Normalize();
                moveWorld = (r * raw.x + f * raw.y);
                if (moveWorld.sqrMagnitude > 0.0001f) moveWorld.Normalize();
            }
            else
            {
                moveWorld = new Vector3(raw.x, 0f, raw.y).normalized;
            }

            // Lookdir = Movementdir (essentials)
            SetLookDirServerRpc(new Vector2(moveWorld.x, moveWorld.z));
        }

        SetMoveInputServerRpc(new Vector2(moveWorld.x, moveWorld.z));

        // Roll hold (LSHIFT)
        SetRollHeldServerRpc(Input.GetKey(KeyCode.LeftShift));

        // Jump (nur grounded, kein buffer/coyote)
        if (Input.GetKeyDown(KeyCode.Space))
            JumpServerRpc();

        // Dash (KeyDown LSHIFT)
        if (Input.GetKeyDown(KeyCode.LeftShift))
            DashServerRpc(new Vector2(moveWorld.x, moveWorld.z));

        // Optional: Toggle Open
        if (Input.GetKeyDown(KeyCode.LeftControl))
            ToggleOpenAnimServerRpc();
    }

    private void FixedUpdate()
    {
        if (!IsServer) return;

        ServerGrounded = IsGrounded();

        // Landing detection -> stop air roll anim
        if (ServerGrounded && !wasGroundedLastFrame)
            airRollAnimActive = false;

        wasGroundedLastFrame = ServerGrounded;

        // Air dash reset on land
        if (ServerGrounded)
            airDashesLeft = maxAirDashes;

        // Roll stamina + state
        UpdateStaminaAndRoll();

        // Mass feel during roll
        rb.mass = baseMass * (srvRollHeld ? Mathf.Max(1f, rollMassMultiplier) : 1f);

        // Dash or move
        if (isDashing) TickDash();
        else ApplyMovementWithDrift();

        // Turn to desired look
        ApplyTurning();

        // Gravity + fall clamp
        ApplyGravity();

        // Animator + audio
        UpdateAnimatorServer();

        Vector3 v = rb.linearVelocity;
        ServerHorizontalSpeed = new Vector3(v.x, 0f, v.z).magnitude;
    }

    // =========================
    // SERVER: Movement (mass + drift)
    // =========================
    private void ApplyMovementWithDrift()
    {
        Vector3 moveDir = new Vector3(srvMoveInput.x, 0f, srvMoveInput.y);
        float inputMag = Mathf.Clamp01(moveDir.magnitude);
        if (inputMag > 0.0001f) moveDir /= inputMag; else moveDir = Vector3.zero;

        bool grounded = ServerGrounded;

        float control = grounded ? 1f : airControl;
        if (!grounded && srvRollHeld)
            control = Mathf.Max(control, minAirControlWhileRoll);

        float speedMult = srvRollHeld ? rollSpeedMultiplier : 1f;

        float slow = slowReceiver != null ? slowReceiver.CurrentMultiplier : 1f;
        float ocMove = overclocks ? overclocks.GetMoveSpeedMult() : 1f;

        float targetSpeed = moveSpeed * speedMult * control * ocMove * slow;

        Vector3 v = rb.linearVelocity;
        Vector3 horiz = new Vector3(v.x, 0f, v.z);

        // Referenzrichtung für Side-Grip:
        // - bei Input: Inputrichtung
        // - sonst: aktuelle Bewegungsrichtung (damit Drift/Momentum nicht "weggegrippt" wird)
        // - erst wenn fast still: LookDir/Forward
        Vector3 refDir;
        if (moveDir != Vector3.zero)
        {
            refDir = moveDir;
        }
        else if (horiz.sqrMagnitude > 0.01f)
        {
            refDir = horiz.normalized;
        }
        else
        {
            refDir = (desiredLookDir.sqrMagnitude > 0.0001f) ? desiredLookDir : transform.forward;
        }


        // 1) Vortrieb nur entlang moveDir (Side-Slip bleibt möglich)
        if (moveDir != Vector3.zero)
        {
            float desiredForwardSpeed = targetSpeed * inputMag;
            float curForwardSpeed = Vector3.Dot(horiz, moveDir);

            // in air while roll: do not actively brake forward
            if (!grounded && srvRollHeld && keepMomentumWhileRollAir)
                desiredForwardSpeed = Mathf.Max(desiredForwardSpeed, curForwardSpeed);

            float diff = desiredForwardSpeed - curForwardSpeed;
            Vector3 forwardForceDir = moveDir * diff;

            float rate = diff >= 0f ? acceleration : deceleration;
            if (srvRollHeld) rate *= rollSteerAccelMult;

            // Force => mass wirkt
            rb.AddForce(forwardForceDir * rate, ForceMode.Force);
        }

        // 2) Bremsen nur am Boden ohne Input
        if (grounded && moveDir == Vector3.zero)
        {
            float brake = groundBrake * (srvRollHeld ? rollBrakeMult : 1f);
            rb.AddForce(-horiz * brake, ForceMode.Force);
        }

        // 3) Lateral grip (Drift)
        Vector3 forwardPart = Vector3.Project(horiz, refDir);
        Vector3 sidePart = horiz - forwardPart;

        float grip = grounded ? lateralGrip : airLateralGrip;
        if (srvRollHeld && grounded) grip = rollLateralGrip;

        rb.AddForce(-sidePart * grip, ForceMode.Force);
    }

    private void ApplyTurning()
    {
        if (desiredLookDir.sqrMagnitude <= 0.0001f) return;
        float targetY = Quaternion.LookRotation(desiredLookDir, Vector3.up).eulerAngles.y;
        Quaternion yOnly = Quaternion.Euler(0f, targetY, 0f);
        float step = turnSpeedDegPerSec * Time.fixedDeltaTime;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, yOnly, step);

        // keep upright
        Vector3 e = transform.rotation.eulerAngles;
        transform.rotation = Quaternion.Euler(0f, e.y, 0f);
    }

    private void ApplyGravity()
    {
        if (isDashing)
        {
            rb.AddForce(Physics.gravity * dashGravityFactor, ForceMode.Acceleration);
        }
        else
        {
            rb.AddForce(Physics.gravity * gravityScale, ForceMode.Acceleration);
        }

        if (maxFallSpeed > 0f && rb.linearVelocity.y < -maxFallSpeed)
        {
            Vector3 v = rb.linearVelocity;
            v.y = -maxFallSpeed;
            rb.linearVelocity = v;
        }
    }

    // =========================
    // SERVER: Roll stamina
    // =========================
    private void UpdateStaminaAndRoll()
    {
        float dt = Time.fixedDeltaTime;

        if (srvRollInputHeld)
        {
            if (!srvRollHeld && stamina.Value < minStaminaToStartRoll)
            {
                srvRollHeld = false;
                return;
            }

            stamina.Value = Mathf.Max(0f, stamina.Value - rollStaminaCostPerSecond * dt);
            srvRollHeld = stamina.Value > 0f;
        }
        else
        {
            srvRollHeld = false;

            if (staminaRegenPerSecond > 0f && stamina.Value < maxStamina)
                stamina.Value = Mathf.Min(maxStamina, stamina.Value + staminaRegenPerSecond * dt);
        }
    }

    // =========================
    // SERVER: Ground check
    // =========================
    private bool IsGrounded()
    {
        if (!sc) return false;

        int mask = groundLayer.value == 0 ? ~0 : groundLayer.value;
        mask &= ~(1 << gameObject.layer);

        float worldRadius = sc.radius * Mathf.Max(
            Mathf.Abs(sc.transform.lossyScale.x),
            Mathf.Abs(sc.transform.lossyScale.y),
            Mathf.Abs(sc.transform.lossyScale.z)
        );

        Vector3 worldCenter = sc.transform.TransformPoint(sc.center);

        float r = worldRadius * 0.98f;
        Vector3 feet = worldCenter + Vector3.down * (r - 0.005f);

        if (Physics.CheckSphere(feet, r * 0.98f, mask, QueryTriggerInteraction.Ignore))
            return true;

        float rayDist = r + Mathf.Max(0.04f, groundProbeExtra) + 0.06f;
        if (Physics.Raycast(worldCenter, Vector3.down, out var hit, rayDist, mask, QueryTriggerInteraction.Ignore))
        {
            float minY = Mathf.Cos(maxSlopeDeg * Mathf.Deg2Rad);
            return hit.normal.y >= minY;
        }

        return false;
    }

    // =========================
    // SERVER: Dash / Jump
    // =========================
    private void TickDash()
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

            Vector3 vel = rb.linearVelocity;
            Vector3 outHoriz = new Vector3(vel.x, 0f, vel.z) * dashEndHorizontalDamping;
            rb.linearVelocity = new Vector3(outHoriz.x, vel.y, outHoriz.z);
        }
    }

    private void DoJump()
    {
        airRollAnimActive = true; // play roll anim in air until landing

        Vector3 v = rb.linearVelocity;
        v.y = 0f;
        rb.linearVelocity = v;

        rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
    }

    // =========================
    // SERVER: Animator + Audio
    // =========================
    private void UpdateAnimatorServer()
    {
        if (!anim) return;

        Vector3 v = rb.linearVelocity;
        float horizSpeed = new Vector3(v.x, 0f, v.z).magnitude;

        anim.SetFloat(moveSpeedHash, ServerGrounded ? horizSpeed : 0f);

        // Immer wenn in der Luft
        bool rollingForAnim = srvRollHeld || !ServerGrounded;
        //bool rollingForAnim = srvRollHeld || airRollAnimActive;

        anim.SetBool(isRollingHash, rollingForAnim);

        //bool walking = ServerGrounded && horizSpeed > 0.15f && !srvRollHeld;
        bool walking = ServerGrounded && horizSpeed > 0.15f && !rollingForAnim;

        if (walking && !srvWasWalking) StartFootstepsClientRpc();
        else if (!walking && srvWasWalking) StopFootstepsClientRpc();

        if (srvRollHeld && !srvWasRolling)
        {
            StopFootstepsClientRpc();
            PlayRollStartClientRpc();
        }
        else if (!srvRollHeld && srvWasRolling)
        {
            StopRollLoopClientRpc();
        }

        srvWasWalking = walking;
        srvWasRolling = srvRollHeld;
    }

    private void EnsureAudioSources()
    {
        if (!audioSource)
            audioSource = GetComponentInChildren<AudioSource>();

        if (!rollLoopSource)
        {
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
            if (!IsGrounded())
            {
                yield return new WaitForSeconds(0.05f);
                continue;
            }

            Vector3 now = transform.position;
            Vector3 delta = now - lastPosForSpeed;
            lastPosForSpeed = now;

            float horizSpeed = new Vector3(delta.x, 0f, delta.z).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            if (horizSpeed < 0.5f)
            {
                yield return new WaitForSeconds(0.05f);
                continue;
            }

            float speed01 = Mathf.InverseLerp(0.5f, moveSpeed * rollSpeedMultiplier, horizSpeed);
            float interval = Mathf.Lerp(baseStepInterval * 1.2f, baseStepInterval * 0.55f, speed01);

            PlayRandomFootstep();

            float jitter = Random.Range(-0.04f, 0.04f);
            yield return new WaitForSeconds(Mathf.Max(0.08f, interval + jitter));
        }
    }

    private void PlayRandomFootstep()
    {
        if (!audioSource || footstepClips == null || footstepClips.Length == 0) return;

        var clip = footstepClips[Random.Range(0, footstepClips.Length)];
        audioSource.pitch = Random.Range(footstepPitchMin, footstepPitchMax);
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

    private void OnDisable()
    {
        StopFootstepsClient();
        StopRollLoopClient();
    }

    // =========================
    // HUD stamina (Owner)
    // =========================
    private void OnStaminaChangedOwner(float oldValue, float newValue)
    {
        PlayerHUD.Instance?.SetStamina(newValue, maxStamina);
    }

    // =========================
    // RPCs (minimal)
    // =========================
    [ServerRpc]
    public void ClearInputServerRpc(ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;

        srvMoveInput = Vector2.zero;
        srvRollInputHeld = false;
        srvRollHeld = false;

        // optional: dash abbrechen
        isDashing = false;
    }


    [ServerRpc]
    private void SetMoveInputServerRpc(Vector2 input, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        srvMoveInput = Vector2.ClampMagnitude(input, 1f);
    }

    [ServerRpc]
    private void SetRollHeldServerRpc(bool held, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        srvRollInputHeld = held;
    }

    [ServerRpc]
    public void SetLookDirServerRpc(Vector2 dir, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;

        Vector3 d = new Vector3(dir.x, 0f, dir.y);
        if (d.sqrMagnitude > 0.0001f)
            desiredLookDir = d.normalized;
    }

    [ServerRpc]
    private void JumpServerRpc(ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        if (isDashing) return;

        // sofortiger, simpler Jump: nur wenn grounded
        if (!IsGrounded()) return;

        DoJump();
    }

    [ServerRpc]
    private void DashServerRpc(Vector2 input, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        if (!rb) return;

        if (Time.time < nextDashAllowedTime) return;

        if (dashStaminaCost > 0f && stamina.Value < dashStaminaCost)
            return;

        bool grounded = ServerGrounded;
        if (!grounded)
        {
            if (airDashesLeft <= 0) return;
            airDashesLeft--;
        }

        dashDir = new Vector3(input.x, 0f, input.y);
        if (dashDir.sqrMagnitude > 0.0001f) dashDir.Normalize();
        else dashDir = transform.forward;

        isDashing = true;
        dashElapsed = 0f;
        nextDashAllowedTime = Time.time + dashCooldown;

        Vector3 v = rb.linearVelocity;
        if (!keepVerticalVelocityOnDash) v.y = 0f;
        rb.linearVelocity = v;

        rb.AddForce(dashDir * (0.35f * dashForce), ForceMode.VelocityChange);

        if (dashStaminaCost > 0f)
            stamina.Value = Mathf.Max(0f, stamina.Value - dashStaminaCost);
    }

    [ServerRpc]
    private void ToggleOpenAnimServerRpc(ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        if (!anim) return;

        bool current;
        try { current = anim.GetBool("Open_Anim"); }
        catch { return; }

        anim.SetBool("Open_Anim", !current);
    }

    [ClientRpc] private void StartFootstepsClientRpc() => StartFootstepsClient();
    [ClientRpc] private void StopFootstepsClientRpc() => StopFootstepsClient();
    [ClientRpc] private void PlayRollStartClientRpc() => PlayRollStartClient();
    [ClientRpc] private void StopRollLoopClientRpc() => StopRollLoopClient();
}
