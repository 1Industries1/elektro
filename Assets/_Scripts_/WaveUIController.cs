using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class WaveUIController : MonoBehaviour
{
    [Header("Refs")]
    public EnemySpawner spawner;
    public TMP_Text waveText;
    public TMP_Text enemiesText;


    private void OnEnable()
    {
        if (spawner == null) return;

        spawner.CurrentWave.OnValueChanged += OnWaveChanged;
        spawner.IsWaveActive.OnValueChanged += OnActiveChanged;
        spawner.EnemiesAlive.OnValueChanged += OnEnemiesChanged;

        // Initiale UI
        OnWaveChanged(0, spawner.CurrentWave.Value);
        OnActiveChanged(false, spawner.IsWaveActive.Value);
        OnEnemiesChanged(0, spawner.EnemiesAlive.Value);
    }

    private void OnDisable()
    {
        if (spawner == null) return;

        spawner.CurrentWave.OnValueChanged -= OnWaveChanged;
        spawner.IsWaveActive.OnValueChanged -= OnActiveChanged;
        spawner.EnemiesAlive.OnValueChanged -= OnEnemiesChanged;
    }

    private void OnWaveChanged(int oldVal, int newVal)
    {
        if (waveText)
            waveText.text = $"Wave: {newVal}";
    }

    private void OnActiveChanged(bool oldVal, bool newVal)
    {
    }

    private void OnEnemiesChanged(int oldVal, int newVal)
    {
        if (enemiesText)
            enemiesText.text = $"Enemy left: {newVal}";
    }

}
