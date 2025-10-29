using Unity.Netcode;
using TMPro;
using UnityEngine;
using System.Collections;

public class PlayerStats : NetworkBehaviour
{
    public NetworkVariable<int> CurrentKills = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private TextMeshProUGUI killCounterText;

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            UpdateKillCounterUI(CurrentKills.Value);
        }

        CurrentKills.OnValueChanged += OnKillsChanged;

        //Debug.Log($"[PlayerStats] Player {OwnerClientId} OnNetworkSpawn - CurrentKills={CurrentKills.Value}");
    }

    public override void OnNetworkDespawn()
    {
        CurrentKills.OnValueChanged -= OnKillsChanged;
        //Debug.Log($"[PlayerStats] Player {OwnerClientId} OnNetworkDespawn");
    }

    // 👇 Das hier einfach in die Klasse einfügen
    private IEnumerator Start()
    {
        if (!IsOwner) yield break;
        while (killCounterText == null)
        {
            killCounterText = GameObject.Find("KillCounterText")?.GetComponent<TextMeshProUGUI>();
            if (killCounterText == null) yield return null; // 1 Frame warten
        }
        UpdateKillCounterUI(CurrentKills.Value);
    }

    private void OnKillsChanged(int oldValue, int newValue)
    {
        //Debug.Log($"[PlayerStats] Player {OwnerClientId} Kills changed: {oldValue} -> {newValue}");

        if (IsOwner)
            UpdateKillCounterUI(newValue);
    }

    private void UpdateKillCounterUI(int kills)
    {
        if (killCounterText != null)
        {
            killCounterText.text = $"Kills: {kills}";
            //Debug.Log($"[PlayerStats] UI updated for Player {OwnerClientId}: {kills} Kills");
        }
    }

    public void AddKill()
    {
        if (!IsServer) return;

        CurrentKills.Value++;
        //Debug.Log($"[PlayerStats] Server added a kill for Player {OwnerClientId}, total={CurrentKills.Value}");
    }
}
