// PlayerSlowReceiver.cs
using UnityEngine;
using Unity.Netcode;
using System.Collections;

[DisallowMultipleComponent]
public class PlayerSlowReceiver : NetworkBehaviour
{
    private float slowMul = 1f;
    private Coroutine slowCo;

    public float CurrentMultiplier => slowMul;

    // Server-side API
    public void ApplySlow(float mul, float duration)
    {
        if (!IsServer) return;
        if (slowCo != null) StopCoroutine(slowCo);
        slowCo = StartCoroutine(SlowRoutine(Mathf.Clamp(mul, 0.3f, 1f), Mathf.Max(0.05f, duration)));
    }

    private IEnumerator SlowRoutine(float mul, float dur)
    {
        SetMultiplierClientRpc(mul);
        yield return new WaitForSeconds(dur);
        SetMultiplierClientRpc(1f);
        slowCo = null;
    }

    [ClientRpc]
    private void SetMultiplierClientRpc(float mul)
    {
        slowMul = mul;
        // TODO: hier optional UI/Icon/VFX toggeln
    }
}
