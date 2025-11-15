using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(LineRenderer))]
public class WaspLaserVisuals : NetworkBehaviour
{
    [Header("Line Renderer Settings")]
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private float lineWidth = 0.1f;

    [Header("Colors")]
    [SerializeField] private Color windupColor = Color.yellow;
    [SerializeField] private Color sweepColor = Color.red;
    [SerializeField] private float fadeOutTime = 0.2f;

    private bool isWindup;
    private bool isSweeping;

    private float windupTimer;
    private float windupDuration;

    private float sweepTimer;
    private float sweepDuration;
    private float sweepTotalAngle;
    private float sweepAngleSign;
    private float sweepMaxDistance;

    private Vector3 sweepBaseDir;
    private float currentAngle;

    private float fadeTimer;
    private Color currentColor;

    private void Awake()
    {
        if (!lineRenderer)
            lineRenderer = GetComponent<LineRenderer>();

        lineRenderer.positionCount = 2;
        lineRenderer.enabled = false;
        lineRenderer.useWorldSpace = true;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;

        currentColor = Color.clear;
        ApplyColor();
    }

    private void Update()
    {
        if (!IsClient) return;

        if (isWindup)
        {
            windupTimer -= Time.deltaTime;
            if (windupTimer <= 0f)
            {
                isWindup = false;
                StartFade();
            }
            else
            {
                UpdateWindupBeam();
            }
        }
        else if (isSweeping)
        {
            sweepTimer -= Time.deltaTime;
            if (sweepTimer <= 0f)
            {
                isSweeping = false;
                StartFade();
            }
            else
            {
                UpdateSweepBeam();
            }
        }
        else
        {
            // Kein aktiver Beam -> ausfaden
            if (fadeTimer > 0f)
            {
                fadeTimer -= Time.deltaTime;
                float t = Mathf.Clamp01(fadeTimer / fadeOutTime);
                Color c = currentColor;
                c.a = t;
                SetLineColor(c);

                if (t <= 0f)
                {
                    lineRenderer.enabled = false;
                }
            }
        }
    }

    // ---------- RPCs ----------

    [ClientRpc]
    public void StartWindupClientRpc(Vector3 baseDirection, float maxDistance, float duration)
    {
        if (!IsClient) return;

        isSweeping = false;
        isWindup = true;

        sweepBaseDir = baseDirection.normalized;
        sweepMaxDistance = maxDistance;

        windupDuration = duration;
        windupTimer = duration;

        currentColor = windupColor;
        SetLineColor(currentColor);

        lineRenderer.enabled = true;
        UpdateWindupBeam();
    }

    [ClientRpc]
    public void StartSweepClientRpc(Vector3 baseDirection, float totalAngle, float duration, float angleSign, float maxDistance)
    {
        if (!IsClient) return;

        isWindup = false;
        isSweeping = true;

        sweepBaseDir = baseDirection.normalized;
        sweepTotalAngle = totalAngle;
        sweepDuration = duration;
        sweepTimer = duration;
        sweepAngleSign = angleSign;
        sweepMaxDistance = maxDistance;

        currentColor = sweepColor;
        SetLineColor(currentColor);

        lineRenderer.enabled = true;
        UpdateSweepBeam();
    }

    [ClientRpc]
    public void EndSweepClientRpc()
    {
        if (!IsClient) return;

        isWindup = false;
        isSweeping = false;
        StartFade();
    }

    // ---------- Beam-Update ----------

    private void UpdateWindupBeam()
    {
        Vector3 origin = transform.position;
        Vector3 dir = sweepBaseDir.sqrMagnitude > 0.001f ? sweepBaseDir : transform.forward;
        dir.Normalize();

        Vector3 end = origin + dir * sweepMaxDistance;

        lineRenderer.SetPosition(0, origin);
        lineRenderer.SetPosition(1, end);
    }

    private void UpdateSweepBeam()
    {
        Vector3 origin = transform.position;

        float t = 1f - (sweepTimer / sweepDuration);
        float halfAngle = sweepTotalAngle * 0.5f;
        float angleOffset = Mathf.Lerp(-halfAngle, halfAngle, t) * sweepAngleSign;

        currentAngle = angleOffset;

        Vector3 dir = Quaternion.AngleAxis(currentAngle, Vector3.up) * sweepBaseDir;
        if (dir.sqrMagnitude < 0.001f)
            dir = transform.forward;

        dir.Normalize();
        Vector3 end = origin + dir * sweepMaxDistance;

        lineRenderer.SetPosition(0, origin);
        lineRenderer.SetPosition(1, end);
    }

    // ---------- Helpers ----------

    private void StartFade()
    {
        fadeTimer = fadeOutTime;
        // aktuelle Farbe bleibt, Alpha wird runtergefahren
    }

    private void SetLineColor(Color c)
    {
        lineRenderer.startColor = c;
        lineRenderer.endColor = c;
    }

    private void ApplyColor()
    {
        SetLineColor(currentColor);
    }
}
