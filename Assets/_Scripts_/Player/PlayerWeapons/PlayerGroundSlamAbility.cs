using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

[RequireComponent(typeof(PlayerMovement), typeof(Rigidbody))]
public class PlayerGroundSlamAbility : NetworkBehaviour
{
    [Header("Input")]
    public KeyCode superJumpKey = KeyCode.F;     // Boden: Superjump
    public KeyCode slamKey = KeyCode.Space;      // Luft: Slam

    [Header("Super Jump / Slam")]
    public float superJumpUpVelocity = 100f;
    public float slamDownVelocity = 100f;
    public float slamCooldown = 8f;

    [Header("Directional Slam")]
    public bool directionalSlam = true;
    [Range(0f, 89f)] public float slamAngleFromHorizontal = 45f;

    [Header("Impact")]
    public GameObject slamImpactVfx;
    public float slamImpactUpKick = 2f;
    public float slamImpactRadius = 6f;
    public float slamImpactDamage = 10f;
    public LayerMask enemyLayers;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip superJumpSfx;
    public AudioClip slamStartSfx;
    public AudioClip slamImpactSfx;
    [Range(0f, 1f)] public float slamSfxVolume = 1f;

    private PlayerMovement _movement;
    private Rigidbody _rb;

    private bool _isSlamming;
    private bool _wasGroundedLastFrame;
    private float _nextSlamAllowedTime;

    private Vector3 _slamDir = Vector3.down;

    private void Awake()
    {
        _movement = GetComponent<PlayerMovement>();
        _rb = GetComponent<Rigidbody>();
        EnsureAudioSource();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer && _movement != null)
            _wasGroundedLastFrame = _movement.ServerGrounded;
    }

    private void Update()
    {
        if (!IsOwner) return;

        // Boden-Fähigkeit: Superjump
        if (Input.GetKeyDown(superJumpKey))
            RequestSuperJumpServerRpc();

        // Luft-Fähigkeit: Slam
        if (Input.GetKeyDown(slamKey))
            RequestSlamServerRpc();
    }

    private void FixedUpdate()
    {
        if (!IsServer || _movement == null || _rb == null) return;

        bool groundedNow = _movement.ServerGrounded;

        // Impact genau beim Landen nach Slam
        if (_isSlamming && groundedNow && !_wasGroundedLastFrame)
            DoImpact();

        _wasGroundedLastFrame = groundedNow;

        // Slam "konstant schnell" entlang Slam-Richtung halten
        if (_isSlamming)
            MaintainMinSlamSpeed();
    }

    // =========================
    // RPCs
    // =========================

    [ServerRpc]
    private void RequestSuperJumpServerRpc(ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        if (_movement == null || _rb == null) return;

        // nur am Boden + nicht während Slam
        if (!_movement.ServerGrounded) return;
        if (_isSlamming) return;

        StartSuperJump();
    }

    [ServerRpc]
    private void RequestSlamServerRpc(ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        if (_movement == null || _rb == null) return;

        // nur in der Luft
        if (_movement.ServerGrounded) return;

        // nicht stapeln
        if (_isSlamming) return;

        // cooldown
        if (Time.time < _nextSlamAllowedTime) return;

        StartSlam();
    }

    // =========================
    // Server Actions
    // =========================

    private void StartSuperJump()
    {
        SetMovementLocks(block: true, fullAirControl: false);

        // Y reset + VelocityChange nach oben
        Vector3 v = _rb.linearVelocity;
        v.y = 0f;
        _rb.linearVelocity = v;
        _rb.AddForce(Vector3.up * superJumpUpVelocity, ForceMode.VelocityChange);

        PlaySuperJumpSfxClientRpc();
    }

    private void StartSlam()
    {
        _isSlamming = true;
        _slamDir = GetSlamDirection();

        SetMovementLocks(block: true, fullAirControl: true);

        // Slam startet mit definierter Geschwindigkeit
        _rb.linearVelocity = _slamDir * Mathf.Abs(slamDownVelocity);

        // cooldown startet beim Slam (nicht beim Superjump)
        _nextSlamAllowedTime = Time.time + slamCooldown;

        PlaySlamStartSfxClientRpc();
    }

    private void MaintainMinSlamSpeed()
    {
        Vector3 dir = (_slamDir.sqrMagnitude > 0.0001f) ? _slamDir.normalized : Vector3.down;
        Vector3 v = _rb.linearVelocity;

        float along = Vector3.Dot(v, dir);
        Vector3 tangent = v - along * dir;

        float minSpeed = Mathf.Abs(slamDownVelocity);
        if (along < minSpeed) along = minSpeed;

        _rb.linearVelocity = dir * along + tangent;
    }

    private void DoImpact()
    {
        _isSlamming = false;
        SetMovementLocks(block: false, fullAirControl: false);

        // kleiner Hop
        if (slamImpactUpKick != 0f)
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

        // AoE Damage (Server)
        if (slamImpactDamage > 0f && slamImpactRadius > 0f)
        {
            Vector3 center = transform.position;
            Collider[] hits = Physics.OverlapSphere(center, slamImpactRadius, enemyLayers, QueryTriggerInteraction.Ignore);

            var alreadyHit = new HashSet<IEnemy>();
            foreach (var col in hits)
            {
                if (!col) continue;
                IEnemy enemy = col.GetComponentInParent<IEnemy>();
                if (enemy == null || alreadyHit.Contains(enemy)) continue;

                alreadyHit.Add(enemy);
                Vector3 hitPoint = col.ClosestPoint(center);
                enemy.TakeDamage(slamImpactDamage, OwnerClientId, hitPoint);
            }
        }

        PlaySlamImpactSfxClientRpc();
    }

    private void SetMovementLocks(bool block, bool fullAirControl)
    {
        if (_movement == null) return;

        //_movement.externalBlockDashAndRoll = block;
        //_movement.externalBlockHover = block;
        //_movement.externalIgnoreMaxFallSpeed = block;
        //_movement.externalForceFullAirControl = fullAirControl;
    }

    // =========================
    // Slam Direction
    // =========================

    private Vector3 GetSlamDirection()
    {
        if (!directionalSlam) return Vector3.down;

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
    // Audio
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
        audioSource.PlayOneShot(clip, slamSfxVolume);
    }

    [ClientRpc] private void PlaySuperJumpSfxClientRpc() => PlayClipLocal(superJumpSfx);
    [ClientRpc] private void PlaySlamStartSfxClientRpc() => PlayClipLocal(slamStartSfx);
    [ClientRpc] private void PlaySlamImpactSfxClientRpc() => PlayClipLocal(slamImpactSfx);
}
