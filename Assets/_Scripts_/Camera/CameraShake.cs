using UnityEngine;
using System.Collections;

public class CameraShake : MonoBehaviour
{
    private Vector3 originalPos;
    private Coroutine routine;

    private void Awake() => originalPos = transform.localPosition;

    public void Shake(float intensity, float duration)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(DoShake(intensity, duration));
    }

    private IEnumerator DoShake(float intensity, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            Vector3 offset = new Vector3(
                (Random.value - 0.5f),
                (Random.value - 0.5f),
                0f
            ) * intensity;
            transform.localPosition = originalPos + offset;
            yield return null;
        }
        transform.localPosition = originalPos;
        routine = null;
    }
}
