using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class SimpleNetworkUI : MonoBehaviour
{
    public InputField ipInputField;
    public TMP_Text statusText;

    private void OnEnable()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;
        nm.OnClientConnectedCallback  += OnClientConnected;
        nm.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void OnDisable()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;
        nm.OnClientConnectedCallback  -= OnClientConnected;
        nm.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    public void StartHost()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) { SetStatus("❌ Kein NetworkManager gefunden."); return; }

        if (nm.IsListening) { SetStatus("ℹ️ Host läuft bereits."); HideUI(); return; }

        bool ok = nm.StartHost();
        SetStatus(ok ? "✅ Host gestartet. Warte auf Clients..." : "❌ Host konnte nicht gestartet werden!");
        if (ok) HideUI();
    }

    public void StartClient()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) { SetStatus("❌ Kein NetworkManager gefunden."); return; }
        if (nm.IsListening && nm.IsClient) { SetStatus("ℹ️ Bereits verbunden."); HideUI(); return; }

        string ip = ipInputField != null && !string.IsNullOrWhiteSpace(ipInputField.text)
                    ? ipInputField.text
                    : "127.0.0.1";

        var transport = (UnityTransport)nm.NetworkConfig.NetworkTransport;
        transport.ConnectionData.Address = ip;

        bool ok = nm.StartClient();
        SetStatus(ok ? $"🔄 Verbinde zu {ip} ..." : "❌ Konnte Client nicht starten!");
    }

    private void OnClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton.LocalClientId == clientId)
        {
            SetStatus("✅ Verbindung erfolgreich hergestellt!");
            HideUI();
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (NetworkManager.Singleton.LocalClientId == clientId)
        {
            string reason = NetworkManager.Singleton.DisconnectReason;
            if (string.IsNullOrEmpty(reason)) reason = "Unbekannter Fehler (Host nicht erreichbar oder Verbindung verloren).";
            SetStatus("❌ Verbindung getrennt: " + reason);
            ShowUI();
        }
    }

    private void SetStatus(string msg) { Debug.Log(msg); if (statusText) statusText.text = msg; }
    private void HideUI() => gameObject.SetActive(false);
    private void ShowUI() => gameObject.SetActive(true);
}
