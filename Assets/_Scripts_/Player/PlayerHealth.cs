using UnityEngine;
using Unity.Netcode;

public class PlayerHealth : NetworkBehaviour
{
    [Header("HP")]
    public float maxHP = 100f;
    public float regenPerSecond = 0.5f;

    [Header("Trefferverhalten")]
    public float iFrames = 0.4f;                 // Unverwundbarkeit nach Hit (Sek.)
    [Range(0f, 1f)] public float rollDR = 0.3f;  // 30% Damage-Reduction während Rollen
    public float armorFlat = 0f;                 // optional, fester Abzug

    private readonly NetworkVariable<float> hp = new(
        readPerm: NetworkVariableReadPermission.Everyone,
        writePerm: NetworkVariableWritePermission.Server);

    private float lastHitTime = -999f;
    private PlayerMovement movement;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // falls schon ein Wert vorhanden ist, clampen, sonst auf maxHP setzen
            float init = (hp.Value > 0f) ? hp.Value : maxHP;
            hp.Value = Mathf.Clamp(init, 0f, maxHP);

            if (!movement) movement = GetComponent<PlayerMovement>();
        }

        if (IsOwner)
            hp.OnValueChanged += OnHpChangedOwner;
    }


    public override void OnNetworkDespawn()
    {
        if (IsOwner)
            hp.OnValueChanged -= OnHpChangedOwner;
    }

    private void Update()
    {
        if (!IsServer) return;

        // Regeneration
        if (hp.Value > 0f && hp.Value < maxHP && regenPerSecond > 0f)
        {
            hp.Value = Mathf.Min(maxHP, hp.Value + regenPerSecond * Time.deltaTime);
        }
    }

    // ===== Server-API =====
    public void Server_TakeDamage(float amount, ulong sourceClientId = ulong.MaxValue)
    {
        if (!IsServer || amount <= 0f || hp.Value <= 0f) return;

        // iFrames aktiv?
        if (Time.time - lastHitTime < iFrames) return;

        // Damage-Reduction beim Rollen
        if (!movement) movement = GetComponent<PlayerMovement>();
        float dr = (movement != null && movement.ServerRollHeld) ? rollDR : 0f;

        float reduced = amount * (1f - Mathf.Clamp01(dr));
        if (armorFlat > 0f) reduced = Mathf.Max(0f, reduced - armorFlat);

        if (reduced <= 0f) return;

        hp.Value = Mathf.Max(0f, hp.Value - reduced);
        lastHitTime = Time.time;

        // Feedback
        HitClientRpc(reduced, hp.Value, maxHP);

        if (hp.Value <= 0f)
            Die();
    }

    public void Server_Heal(float amount)
    {
        if (!IsServer || amount <= 0f || hp.Value <= 0f) return;
        hp.Value = Mathf.Min(maxHP, hp.Value + amount);
        HealClientRpc(amount, hp.Value, maxHP);
    }

    // ===== Server-API: Max-HP setzen (für Upgrades) =====
    public void Server_SetMaxHP(float newMax, bool keepRelativeRatio = true)
    {
        if (!IsServer) return;

        newMax = Mathf.Max(1f, newMax);

        float oldMax = maxHP;
        float cur    = hp.Value;
        float pct    = (oldMax > 0f) ? Mathf.Clamp01(cur / oldMax) : 1f;

        // neues Maximum setzen
        maxHP = newMax;

        // aktuellen HP-Wert je nach Option anpassen
        float newCur = keepRelativeRatio ? (newMax * pct) : Mathf.Min(cur, newMax);

        // networked Wert updaten (triggert OnValueChanged → HUD)
        hp.Value = Mathf.Clamp(newCur, 0f, newMax);
    }


    private void Die()
    {
        Debug.Log($"[Server] Player {OwnerClientId} died.");
        // TODO: Respawn / Disable input / Screen fade
        DiedClientRpc();
    }

    // ===== UI (Owner) =====
    private void OnHpChangedOwner(float oldValue, float newValue)
    {
        PlayerHUD.Instance?.SetHealth(newValue, maxHP); // Implementiere diese HUD-Methode
    }

    [ClientRpc] private void HitClientRpc(float dmg, float cur, float max)
    {
        if (IsOwner)
        {
            PlayerHUD.Instance?.OnLocalPlayerHitHP(dmg, cur, max);
            LocalBatteryFX.Instance?.SetBatteryState(cur, max, false); // optional: falls du vorhandene FX wiederverwenden willst
        }
    }

    [ClientRpc] private void HealClientRpc(float heal, float cur, float max)
    {
        if (IsOwner) PlayerHUD.Instance?.OnLocalPlayerHealHP(heal, cur, max);
    }

    [ClientRpc] private void DiedClientRpc()
    {
        if (IsOwner) PlayerHUD.Instance?.OnLocalPlayerDied();
    }

    // Getter
    public float GetHP() => hp.Value;
    public float GetMaxHP() => maxHP;
    public bool IsDead() => hp.Value <= 0f;
}
