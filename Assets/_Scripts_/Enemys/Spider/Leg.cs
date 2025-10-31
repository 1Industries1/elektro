using System.Collections;
using UnityEngine;

namespace MimicSpace
{
    public class Leg : MonoBehaviour, IRaycastReceiver
    {
        Mimic myMimic;

        public bool isDeployed = false;
        public Vector3 footPosition;
        public float maxLegDistance;
        public int legResolution;

        public LineRenderer legLine;
        public int handlesCount = 8; // 0..6 + finaler Fuß (Index 7)

        public float legMinHeight = 0.3f;
        public float legMaxHeight = 1.1f;
        float legHeight;

        public Vector3[] handles;         // Größe = handlesCount
        public float handleOffsetMinRadius = 0.05f;
        public float handleOffsetMaxRadius = 0.25f;
        public Vector3[] handleOffsets;   // Größe = 6

        public float finalFootDistance = 0.3f;

        public float growCoef = 5f;
        public float growTarget = 1f;

        [Range(0, 1f)] public float progression;

        bool isRemoved = false;
        bool canDie = false;
        public float minDuration = 0.4f;

        [Header("Rotation")]
        public float rotationSpeed = 30f;
        public float minRotSpeed = 20f;
        public float maxRotSpeed = 50f;
        float rotationSign = 1f;
        public float oscillationSpeed = 40f;
        public float minOscillationSpeed = 30f;
        public float maxOscillationSpeed = 60f;
        float oscillationProgress;

        public Color myColor;

        // ---------- Alloc-freie Puffer ----------
        Vector3[] workHandles;   // Arbeitskopie für de Casteljau (Größe = handlesCount)
        Vector3[] pointsArray;   // Ausgabepuffer für Kurvenpunkte (>= legResolution + 2)
        int pointsCount;         // tatsächlich gefüllte Punkte

        // ---------- Throttling ----------
        float _nextLinecastTime;
        bool _obstructed; // Ergebnis des letzten (gedrosselten) Checks

        // Frame-Cadence: nur jedes N-te Frame updaten
        [SerializeField] int tickDivisor = 3;
        int _tickMask;
        static int s_nextMask;

        const int LOS_REQ_ID = 1;

        // ---------- Initialisierung ----------
        public void Initialize(Vector3 footPosition, int legResolution, float maxLegDistance, float growCoef, Mimic myMimic, float lifeTime)
        {
            this.footPosition = footPosition;
            this.legResolution = Mathf.Max(1, legResolution);
            this.maxLegDistance = maxLegDistance;
            this.growCoef = growCoef;
            this.myMimic = myMimic;

            // Cadence zuweisen (verteilt Arbeit über Frames)
            _tickMask = (s_nextMask++) % Mathf.Max(1, tickDivisor);

            legLine = GetComponent<LineRenderer>();
            if (legLine != null)
            {
                legLine.useWorldSpace = true;
                if (legLine.material == null)
                    legLine.material = new Material(Shader.Find("Sprites/Default")); // einmalig
                if (legLine.widthMultiplier <= 0f)
                    legLine.widthMultiplier = 0.06f;
            }

            // Control- und Offset-Arrays
            handles = new Vector3[handlesCount];
            handleOffsets = new Vector3[6];
            for (int i = 0; i < 6; i++)
                handleOffsets[i] = Random.onUnitSphere * Random.Range(handleOffsetMinRadius, handleOffsetMaxRadius);

            // Finalen Fuß via Raycast auf den Boden setzen
            Vector2 footOffset = Random.insideUnitCircle.normalized * finalFootDistance;
            var start = footPosition + Vector3.up * 5f + new Vector3(footOffset.x, 0, footOffset.y);
            if (Physics.Raycast(start, Vector3.down, out var hit, 20f, myMimic.groundMask, QueryTriggerInteraction.Ignore))
                handles[7] = hit.point;
            else
                handles[7] = footPosition;

            legHeight = Random.Range(legMinHeight, legMaxHeight);
            rotationSpeed = Random.Range(minRotSpeed, maxRotSpeed);
            rotationSign = 1f;
            oscillationSpeed = Random.Range(minOscillationSpeed, maxOscillationSpeed);
            oscillationProgress = 0f;

            myMimic.legCount++;
            growTarget = 1f;

            isRemoved = false;
            canDie = false;
            isDeployed = false;

            // Puffer anlegen
            EnsureBuffers();

            StartCoroutine(WaitToDie());
            StartCoroutine(WaitAndDie(lifeTime));
            Sethandles();
        }

        void EnsureBuffers()
        {
            if (workHandles == null || workHandles.Length != handlesCount)
                workHandles = new Vector3[handlesCount];

            int neededPoints = Mathf.Max(2, legResolution + 2);
            if (pointsArray == null || pointsArray.Length < neededPoints)
                pointsArray = new Vector3[neededPoints];
        }

        IEnumerator WaitToDie()
        {
            yield return new WaitForSeconds(minDuration);
            canDie = true;
        }

        IEnumerator WaitAndDie(float lifeTime)
        {
            yield return new WaitForSeconds(lifeTime);
            while (myMimic.deployedLegs < myMimic.minimumAnchoredParts)
                yield return null;
            growTarget = 0f;
        }

        // ---------- Laufzeit ----------
        void Update()
        {
            // nur jedes N-te Frame arbeiten
            if ((Time.frameCount % Mathf.Max(1, tickDivisor)) != _tickMask)
                return;

            EnsureBuffers();

            // 1) Abstands-/Sichtlinienlogik (mit Throttling + Batching)
            if (growTarget == 1f)
            {
                float distXZ = Vector3.Distance(
                    new Vector3(myMimic.legPlacerOrigin.x, 0, myMimic.legPlacerOrigin.z),
                    new Vector3(footPosition.x, 0, footPosition.z));

                if (distXZ > maxLegDistance && canDie && myMimic.deployedLegs > myMimic.minimumAnchoredParts)
                {
                    growTarget = 0f;
                }
                else
                {
                    if (Time.time >= _nextLinecastTime)
                    {
                        _nextLinecastTime = Time.time + 0.15f;

                        Vector3 from = footPosition + Vector3.up * 0.05f;
                        Vector3 to   = transform.position + Vector3.up * 0.2f;

                        RaycastBatcher.Enqueue(from, to, myMimic.groundMask, this, LOS_REQ_ID);
                    }
                }
            }

            // 2) Wachstum / Einziehen
            progression = Mathf.Lerp(progression, growTarget, growCoef * Time.deltaTime);

            // 3) Deployment-Zählung
            if (!isDeployed && progression > 0.9f)
            {
                myMimic.deployedLegs++;
                isDeployed = true;
            }
            else if (isDeployed && progression < 0.9f)
            {
                myMimic.deployedLegs--;
                isDeployed = false;
            }

            // 4) Recycle
            if (progression < 0.5f && growTarget == 0f)
            {
                if (!isRemoved)
                {
                    GetComponentInParent<Mimic>().legCount--;
                    isRemoved = true;
                }
                if (progression < 0.05f)
                {
                    if (legLine) legLine.positionCount = 0;
                    myMimic.RecycleLeg(gameObject);
                    return;
                }
            }

            // 5) Kurven-Handles + Rendering (alloc-frei)
            Sethandles();
            GetSamplePointsNonAlloc(handles, legResolution, progression, pointsArray, workHandles, out pointsCount);

            if (legLine)
            {
                legLine.positionCount = pointsCount;
                for (int i = 0; i < pointsCount; i++)
                    legLine.SetPosition(i, pointsArray[i]);
            }
        }

        // ---------- Raycast-Ergebnis ----------
        public void OnRaycastResult(int requestId, bool hit, RaycastHit hitInfo)
        {
            if (requestId != LOS_REQ_ID) return;
            _obstructed = hit;
            if (_obstructed) growTarget = 0f;
        }

        // ---------- Control-Points ----------
        void Sethandles()
        {
            handles[0] = transform.position;
            handles[6] = footPosition + Vector3.up * 0.05f;

            handles[2] = Vector3.Lerp(handles[0], handles[6], 0.4f);
            handles[2].y = handles[0].y + legHeight;
            handles[1] = Vector3.Lerp(handles[0], handles[2], 0.5f);
            handles[3] = Vector3.Lerp(handles[2], handles[6], 0.25f);
            handles[4] = Vector3.Lerp(handles[2], handles[6], 0.5f);
            handles[5] = Vector3.Lerp(handles[2], handles[6], 0.75f);

            RotateHandleOffset();

            handles[1] += handleOffsets[0];
            handles[2] += handleOffsets[1];
            handles[3] += handleOffsets[2];
            handles[4] += handleOffsets[3] / 2f;
            handles[5] += handleOffsets[4] / 4f;
        }

        void RotateHandleOffset()
        {
            oscillationProgress = Mathf.Repeat(oscillationProgress + Time.deltaTime * oscillationSpeed, 360f);
            float newAngle = rotationSpeed * Time.deltaTime * Mathf.Cos(oscillationProgress * Mathf.Deg2Rad) + 1f;

            for (int i = 1; i < 6; i++)
            {
                Vector3 axisRotation = (handles[i + 1] - handles[i - 1]) * 0.5f;
                handleOffsets[i - 1] = Quaternion.AngleAxis(newAngle, rotationSign * axisRotation) * handleOffsets[i - 1];
            }
        }

        // ---------- Alloc-freies Sampling ----------
        void GetSamplePointsNonAlloc(
            Vector3[] sourceHandles,
            int resolution,
            float tEnd,
            Vector3[] outPoints,
            Vector3[] tempHandles,
            out int outCount)
        {
            float step = 1f / Mathf.Max(1, resolution);
            int idx = 0;

            float t = 0f;
            while (t <= tEnd && idx < outPoints.Length)
            {
                outPoints[idx++] = EvalCurveNonAlloc(sourceHandles, tempHandles, t);
                t += step;
            }

            // Endpunkt exakt hinzufügen
            if (idx < outPoints.Length)
                outPoints[idx++] = EvalCurveNonAlloc(sourceHandles, tempHandles, tEnd);

            outCount = idx;
        }

        Vector3 EvalCurveNonAlloc(Vector3[] source, Vector3[] work, float t)
        {
            System.Array.Copy(source, work, source.Length);
            int n = source.Length;
            while (n > 1)
            {
                int last = n - 1;
                for (int i = 0; i < last; i++)
                    work[i] = Vector3.Lerp(work[i], work[i + 1], t);
                n--;
            }
            return work[0];
        }
    }
}
