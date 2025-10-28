using System.Collections;
using UnityEngine;

namespace MimicSpace
{
    public class Leg : MonoBehaviour
    {
        Mimic myMimic;

        public bool isDeployed = false;
        public Vector3 footPosition;
        public float maxLegDistance;
        public int legResolution;

        public LineRenderer legLine;
        public int handlesCount = 8; // 8 (7 control points + final foot)

        public float legMinHeight = 0.3f;
        public float legMaxHeight = 1.1f;
        float legHeight;

        public Vector3[] handles;         // Control points (size = handlesCount)
        public float handleOffsetMinRadius = 0.05f;
        public float handleOffsetMaxRadius = 0.25f;
        public Vector3[] handleOffsets;   // random offsets for nicer curvature (size = 6)

        public float finalFootDistance = 0.3f;

        public float growCoef = 5f;
        public float growTarget = 1;

        [Range(0, 1f)] public float progression;

        bool isRemoved = false;
        bool canDie = false;
        public float minDuration = 0.4f;

        [Header("Rotation")]
        public float rotationSpeed = 30f;
        public float minRotSpeed = 20f;
        public float maxRotSpeed = 50f;
        float rotationSign = 1;
        public float oscillationSpeed = 40f;
        public float minOscillationSpeed = 30f;
        public float maxOscillationSpeed = 60f;
        float oscillationProgress;

        public Color myColor;

        // ---------- Alloc-freie Puffer ----------
        Vector3[] workHandles;   // Arbeitskopie für de Casteljau (Größe = handlesCount)
        Vector3[] pointsArray;   // Ausgabepuffer für Kurvenpunkte (>= legResolution + 2)
        int pointsCount;         // tatsächlich gefüllte Punkte

        // ---------- Initialisierung ----------
        public void Initialize(Vector3 footPosition, int legResolution, float maxLegDistance, float growCoef, Mimic myMimic, float lifeTime)
        {
            this.footPosition = footPosition;
            this.legResolution = Mathf.Max(1, legResolution);
            this.maxLegDistance = maxLegDistance;
            this.growCoef = growCoef;
            this.myMimic = myMimic;

            legLine = GetComponent<LineRenderer>();
            if (legLine != null)
            {
                legLine.useWorldSpace = true;
                if (legLine.material == null)
                    legLine.material = new Material(Shader.Find("Sprites/Default")); // einmalig, nicht pro Frame
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
            RaycastHit hit;
            var start = footPosition + Vector3.up * 5f + new Vector3(footOffset.x, 0, footOffset.y);
            if (Physics.Raycast(start, Vector3.down, out hit, 20f, myMimic.groundMask, QueryTriggerInteraction.Ignore))
                handles[7] = hit.point;
            else
                handles[7] = footPosition;

            legHeight = Random.Range(legMinHeight, legMaxHeight);
            rotationSpeed = Random.Range(minRotSpeed, maxRotSpeed);
            rotationSign = 1;
            oscillationSpeed = Random.Range(minOscillationSpeed, maxOscillationSpeed);
            oscillationProgress = 0;

            myMimic.legCount++;
            growTarget = 1;

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
            // Arbeitskopie für de Casteljau
            if (workHandles == null || workHandles.Length != handlesCount)
                workHandles = new Vector3[handlesCount];

            // Ausgabepuffer: +2 (Endpunkt + Sicherheitsmarge)
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
            growTarget = 0;
        }

        // ---------- Laufzeit ----------
        void Update()
        {
            // Wenn sich die Auflösung zur Laufzeit ändert, Puffer anpassen
            EnsureBuffers();

            // 1) Abstands-/Sichtlinienlogik
            if (growTarget == 1)
            {
                float distXZ = Vector3.Distance(
                    new Vector3(myMimic.legPlacerOrigin.x, 0, myMimic.legPlacerOrigin.z),
                    new Vector3(footPosition.x, 0, footPosition.z));

                if (distXZ > maxLegDistance && canDie && myMimic.deployedLegs > myMimic.minimumAnchoredParts)
                {
                    growTarget = 0;
                }
                else
                {
                    RaycastHit hit;
                    if (Physics.Linecast(footPosition + Vector3.up * 0.05f,
                                         transform.position + Vector3.up * 0.2f,
                                         out hit, myMimic.groundMask, QueryTriggerInteraction.Ignore))
                    {
                        // Gelände zwischen Fuß und Körper -> einziehen
                        growTarget = 0;
                    }
                }
            }

            // Wachstum / Einziehen
            progression = Mathf.Lerp(progression, growTarget, growCoef * Time.deltaTime);

            // Deployment-Zählung
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

            // Recycle
            if (progression < 0.5f && growTarget == 0)
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

            // Kurven-Handles updaten & zeichnen (alloc-frei)
            Sethandles();
            GetSamplePointsNonAlloc(handles, legResolution, progression, pointsArray, workHandles, out pointsCount);

            if (legLine)
            {
                legLine.positionCount = pointsCount;
                // SetPositions(Vector3[]) würde das gesamte Array setzen; wir setzen nur die gefüllten Elemente:
                for (int i = 0; i < pointsCount; i++)
                    legLine.SetPosition(i, pointsArray[i]);
            }
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
            // Quelle in Arbeitskopie kopieren (8 Elemente → günstig)
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
