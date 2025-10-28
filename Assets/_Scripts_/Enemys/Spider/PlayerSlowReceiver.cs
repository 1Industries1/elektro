// PlayerSlowReceiver.cs
using UnityEngine;
using Unity.Netcode;
using System.Collections;

[DisallowMultipleComponent]
public class PlayerSlowReceiver : NetworkBehaviour
{
    private float slowMul = 1f;
    private float slowUntil = 0f;
    private Coroutine slowCo;

    public float CurrentMultiplier => slowMul;

    // Server-side API
    public void ApplyOrRefreshSlow(float mul, float duration)
    {
        if (!IsServer) return;
        mul = Mathf.Clamp(mul, 0.3f, 1f);
        duration = Mathf.Max(0.05f, duration);

        // stärkeren (kleineren) Mul bevorzugen & Dauer verlängern
        slowMul = Mathf.Min(slowMul, mul);
        slowUntil = Mathf.Max(slowUntil, Time.time + duration);

        if (slowCo == null) slowCo = StartCoroutine(SlowRoutine());
    }

    private IEnumerator SlowRoutine()
    {
        SetMultiplierClientRpc(slowMul);
        while (Time.time < slowUntil) yield return null;
        slowMul = 1f;
        SetMultiplierClientRpc(1f);
        slowCo = null;
    }

    [ClientRpc] private void SetMultiplierClientRpc(float mul){ slowMul = mul; }
}
