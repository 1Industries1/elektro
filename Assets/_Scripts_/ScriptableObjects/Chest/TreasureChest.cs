using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[RequireComponent(typeof(Collider))]
public class TreasureChest : MonoBehaviour
{
    // ==========================
    // --- ChestInteraction-Teil
    // ==========================
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Image holdCircle;
    [SerializeField] private string openTrigger = "Open";

    [Header("Settings")]
    [Tooltip("Sekunden, die im Trigger gewartet werden müssen.")]
    [SerializeField] private float holdDuration = 3f;
    [Tooltip("Fortschritt fällt zurück, wenn Spieler den Trigger verlässt?")]
    [SerializeField] private bool resetOnExit = true;
    [Tooltip("Kann die Truhe nur einmal geöffnet werden?")]
    [SerializeField] private bool singleUse = true;
    [Tooltip("Zeit nach Öffnen, bevor erneut interagiert werden darf (falls singleUse = false).")]
    [SerializeField] private float reopenCooldown = 2f;
    [SerializeField] private string playerTag = "Player";

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;  // Quelle am selben GameObject
    [SerializeField] private AudioClip holdLoopClip;   // Sound, der während des Haltens looped
    [SerializeField] private AudioClip openClip;       // Einmaliger Sound beim Öffnen
    [SerializeField][Range(0f, 1f)] private float holdLoopBaseVolume = 0.25f;
    [SerializeField] private bool modulateHoldByProgress = true; // Lautstärke/Pitch nach Fortschritt


    [Header("Slow Motion (Chest)")]
    public bool chestSlowMo = true;
    [Range(0.05f, 1f)] public float chestSlowScale = 0.05f;
    public float chestSlowFadeIn = 0.35f;
    public float chestSlowFadeOut = 1.2f;
    public float chestSlowDelay = 2f;

    private int _chestSlowHandle = 0;
    private Coroutine _slowDelayCo;

    [Header("Events")]
    public UnityEvent OnOpened;
    public UnityEvent<float> OnProgress; // 0..1

    // intern
    private bool inRange;
    private bool opened;
    private float holdTimer;
    private float cooldownUntil;
    private int openTriggerHash;

    // wer steht gerade im Trigger
    private PlayerInventory recipient;

    // ==========================
    // --- Drop-Logik (bestehend)
    // ==========================
    [System.Flags]
    public enum DropType
    {
        NewPassive = 1, NewWeapon = 2, UpgradePassive = 4,
        UpgradeWeapon = 8, Evolution = 16
    }

    public DropType possibleDrops = (DropType)~0;

    public enum DropCountType { sequential, random }
    public DropCountType dropCountType = DropCountType.sequential;

    public TreasureChestDropProfile[] dropProfiles;

    public static int totalPickups = 0;
    private int currentDropProfileIndex = 0;

    private void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();

        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;

        openTriggerHash = Animator.StringToHash(openTrigger);

        if (holdCircle)
        {
            holdCircle.type = Image.Type.Filled;
            holdCircle.fillAmount = 0f;
            holdCircle.gameObject.SetActive(false);
        }

        if (audioSource == null && (holdLoopClip != null || openClip != null))
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsBlocked() || !other.CompareTag(playerTag)) return;

        inRange = true;

        // Versuche PlayerInventory zu merken (für Drops)
        if (other.TryGetComponent(out PlayerInventory p))
            recipient = p;

        if (holdCircle) holdCircle.gameObject.SetActive(true);

        TryStartHoldLoop();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        inRange = false;

        if (resetOnExit) holdTimer = 0f;
        UpdateUI();

        if (holdCircle) holdCircle.gameObject.SetActive(false);

        // Spieler verlässt Bereich -> Empfänger vergessen
        if (other.TryGetComponent(out PlayerInventory p) && p == recipient)
            recipient = null;

        StopHoldLoop();
    }

    private void Update()
    {
        if (!inRange || IsBlocked()) return;

        // Automatischer Hold-Progress
        holdTimer += Time.deltaTime;
        UpdateUI();

        if (holdTimer >= holdDuration)
        {
            OpenChest();
        }
    }

    private void UpdateUI()
    {
        float t = holdDuration <= 0f ? 1f : Mathf.Clamp01(holdTimer / Mathf.Max(0.0001f, holdDuration));
        if (holdCircle) holdCircle.fillAmount = t;
        OnProgress?.Invoke(t);

        // Audio-Feedback an Fortschritt koppeln
        if (modulateHoldByProgress && audioSource != null && audioSource.clip == holdLoopClip && audioSource.isPlaying)
        {
            audioSource.volume = holdLoopBaseVolume * Mathf.Lerp(0.6f, 1f, t);
            audioSource.pitch = Mathf.Lerp(0.9f, 1.1f, t);
        }
    }


    private void OpenChest()
    {
        // Reset/Blockaden
        holdTimer = 0f;
        UpdateUI();

        if (singleUse) opened = true;
        cooldownUntil = Time.time + reopenCooldown;

        if (holdCircle) holdCircle.gameObject.SetActive(false);

        // Animation wie in ChestInteraction
        if (animator && openTriggerHash != 0)
            animator.SetTrigger(openTriggerHash);

        // Optional: Collider deaktivieren, wenn nur einmal nutzbar
        //var col = GetComponent<Collider>();
        //if (col && singleUse) col.enabled = false;

        // --- AUDIO: Hold-Loop aus, Open-SFX an
        StopHoldLoop();
        if (audioSource != null && openClip != null)
            audioSource.PlayOneShot(openClip);

        // Drops vergeben
        GiveRewards(recipient);

        // UI wie zuvor, aber Objekt NICHT deaktivieren/zerstören
        UIChestManager.Activate(this);

        // SlowMo NACH Delay starten, bis Close
        if (chestSlowMo && SlowMoManager.Instance != null && _chestSlowHandle == 0)
        {
            if (_slowDelayCo != null) StopCoroutine(_slowDelayCo);
            _slowDelayCo = StartCoroutine(StartChestSlowMoDelayed());
        }

        OnOpened?.Invoke();

        // Fortschrittszähler für Profile (nur wenn Profile existieren)
        if (dropProfiles != null && dropProfiles.Length > 0)
            totalPickups = (totalPickups + 1) % dropProfiles.Length;
    }

    private bool IsBlocked()
    {
        if (singleUse && opened) return true;
        if (!singleUse && Time.time < cooldownUntil) return true;
        return false;
    }

    private IEnumerator StartChestSlowMoDelayed()
    {
        float d = Mathf.Max(0f, chestSlowDelay);
        if (d > 0f)
            yield return new WaitForSecondsRealtime(d);

        // Falls während des Delays schon geschlossen/abgebrochen wurde:
        if (!chestSlowMo || SlowMoManager.Instance == null || _chestSlowHandle != 0)
        {
            _slowDelayCo = null;
            yield break;
        }

        _chestSlowHandle = SlowMoManager.Instance.BeginHold(chestSlowScale, chestSlowFadeIn);
        _slowDelayCo = null;
    }

    // ==========================
    // --- Drop-Helfer
    // ==========================

    private void TryStartHoldLoop()
    {
        if (audioSource == null || holdLoopClip == null) return;

        // Nur starten, wenn noch nicht läuft bzw. anderer Clip aktiv ist
        if (!audioSource.isPlaying || audioSource.clip != holdLoopClip)
        {
            audioSource.clip = holdLoopClip;
            audioSource.loop = true;
            audioSource.volume = holdLoopBaseVolume;
            audioSource.pitch = 1f;
            audioSource.Play();
        }
    }

    private void StopHoldLoop()
    {
        if (audioSource == null) return;
        if (audioSource.clip == holdLoopClip)
            audioSource.Stop();
    }


    public TreasureChestDropProfile GetCurrentDropProfile()
    {
        if (dropProfiles == null || dropProfiles.Length == 0) return null;
        currentDropProfileIndex = Mathf.Clamp(currentDropProfileIndex, 0, dropProfiles.Length - 1);
        return dropProfiles[currentDropProfileIndex];
    }

    public TreasureChestDropProfile GetNextDropProfile()
    {
        if (dropProfiles == null || dropProfiles.Length == 0)
        {
            Debug.LogWarning("Drop profiles not set.");
            return null;
        }

        switch (dropCountType)
        {
            case DropCountType.sequential:
                currentDropProfileIndex = Mathf.Clamp(totalPickups, 0, dropProfiles.Length - 1);
                return dropProfiles[currentDropProfileIndex];

            case DropCountType.random:
                float playerLuck = 1f;
                if (recipient)
                {
                    // Optional: Luck aus Stats ziehen
                    // var stats = recipient.GetComponentInChildren<PlayerStats>();
                    // if (stats != null) playerLuck = Mathf.Max(0f, stats.Actual.luck);
                }

                var weightedProfiles = new List<(int index, TreasureChestDropProfile profile, float weight)>();
                for (int i = 0; i < dropProfiles.Length; i++)
                {
                    var p = dropProfiles[i];
                    if (p == null) continue;
                    float weight = Mathf.Max(0f, p.baseDropChance * (1f + p.luckScaling * (playerLuck - 1f)));
                    weightedProfiles.Add((i, p, weight));
                }

                if (weightedProfiles.Count == 0) return GetCurrentDropProfile();

                float totalWeight = 0f;
                foreach (var entry in weightedProfiles) totalWeight += entry.weight;
                if (totalWeight <= 0f) return GetCurrentDropProfile();

                float r = Random.Range(0f, totalWeight);
                float cumulative = 0f;
                foreach (var entry in weightedProfiles)
                {
                    cumulative += entry.weight;
                    if (r < cumulative)
                    {
                        currentDropProfileIndex = entry.index;
                        return entry.profile;
                    }
                }
                currentDropProfileIndex = weightedProfiles[^1].index;
                return weightedProfiles[^1].profile;
        }

        return GetCurrentDropProfile();
    }

    private int GetRewardCount()
    {
        TreasureChestDropProfile dp = GetNextDropProfile();
        return dp ? Mathf.Max(1, dp.noOfItems) : 1;
    }

    // Stubs, bis Items angebunden sind
    private bool TryEvolve<T>(PlayerInventory inventory) where T : class { return false; }
    private bool TryUpgrade<T>(PlayerInventory inventory) where T : class { return false; }
    private bool TryGive<T>(PlayerInventory inventory) where T : class { return false; }

    private void GiveRewards(PlayerInventory inventory)
    {
        if (inventory == null) return;

        int rewardCount = GetRewardCount();
        for (int i = 0; i < rewardCount; i++)
        {
            if (possibleDrops.HasFlag(DropType.Evolution) && TryEvolve<object>(inventory)) continue;
            if (possibleDrops.HasFlag(DropType.UpgradeWeapon) && TryUpgrade<object>(inventory)) continue;
            if (possibleDrops.HasFlag(DropType.UpgradePassive) && TryUpgrade<object>(inventory)) continue;
            if (possibleDrops.HasFlag(DropType.NewWeapon) && TryGive<object>(inventory)) continue;
            if (possibleDrops.HasFlag(DropType.NewPassive)) TryGive<object>(inventory);
        }
    }

    public void OnChestUIClose()
    {
        if (_slowDelayCo != null)
        {
            StopCoroutine(_slowDelayCo);
            _slowDelayCo = null;
        }

        if (_chestSlowHandle != 0 && SlowMoManager.Instance != null)
        {
            SlowMoManager.Instance.EndHold(_chestSlowHandle, chestSlowFadeOut);
            _chestSlowHandle = 0;
        }
    }

}
