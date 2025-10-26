using System.Collections;
using System.Collections.Generic;
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
        public int handlesCount = 8; // 8 (7 legs + 1 finalfoot)

        public float legMinHeight = 0.3f;
        public float legMaxHeight = 1.1f;
        float legHeight;
        public Vector3[] handles;
        public float handleOffsetMinRadius = 0.05f;
        public float handleOffsetMaxRadius = 0.25f;
        public Vector3[] handleOffsets;
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

        public void Initialize(Vector3 footPosition, int legResolution, float maxLegDistance, float growCoef, Mimic myMimic, float lifeTime)
        {
            this.footPosition = footPosition;
            this.legResolution = legResolution;
            this.maxLegDistance = maxLegDistance;
            this.growCoef = growCoef;
            this.myMimic = myMimic;

            this.legLine = GetComponent<LineRenderer>();
            if (legLine != null)
            {
                legLine.useWorldSpace = true;
                if (legLine.material == null)
                    legLine.material = new Material(Shader.Find("Sprites/Default")); // Fallback sichtbar
                if (legLine.widthMultiplier <= 0f)
                    legLine.widthMultiplier = 0.06f;
            }

            handles = new Vector3[handlesCount];
            handleOffsets = new Vector3[6];
            for (int i = 0; i < 6; i++)
                handleOffsets[i] = Random.onUnitSphere * Random.Range(handleOffsetMinRadius, handleOffsetMaxRadius);

            // Zeh leicht versetzen, Raycast nur auf Boden
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
            StartCoroutine(WaitToDie());
            StartCoroutine(WaitAndDie(lifeTime));
            Sethandles();
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

        private void Update()
        {
            // 1) Abstand prüfen (zu weit weg -> einziehen, aber erst nach Mindestzeit)
            if (growTarget == 1)
            {
                float distXZ = Vector3.Distance(
                    new Vector3(myMimic.legPlacerOrigin.x, 0, myMimic.legPlacerOrigin.z),
                    new Vector3(footPosition.x, 0, footPosition.z));

                if (distXZ > maxLegDistance && canDie && myMimic.deployedLegs > myMimic.minimumAnchoredParts)
                    growTarget = 0;
                else
                {
                    // 2) Sichtlinie nur gegen Boden prüfen (kein Self-Hit)
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

            // Growth / Retract
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

            // Kurven-Handles updaten & zeichnen
            Sethandles();
            Vector3[] points = GetSamplePoints((Vector3[])handles.Clone(), legResolution, progression);
            if (legLine)
            {
                legLine.positionCount = points.Length;
                legLine.SetPositions(points);
            }
        }

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
            oscillationProgress += Time.deltaTime * oscillationSpeed;
            if (oscillationProgress >= 360f)
                oscillationProgress -= 360f;

            float newAngle = rotationSpeed * Time.deltaTime * Mathf.Cos(oscillationProgress * Mathf.Deg2Rad) + 1f;

            for (int i = 1; i < 6; i++)
            {
                Vector3 axisRotation = (handles[i + 1] - handles[i - 1]) / 2f;
                handleOffsets[i - 1] = Quaternion.AngleAxis(newAngle, rotationSign * axisRotation) * handleOffsets[i - 1];
            }
        }

        Vector3[] GetSamplePoints(Vector3[] curveHandles, int resolution, float t)
        {
            List<Vector3> segmentPos = new List<Vector3>();
            float segmentLength = 1f / resolution;

            for (float _t = 0; _t <= t; _t += segmentLength)
                segmentPos.Add(GetPointOnCurve((Vector3[])curveHandles.Clone(), _t));
            segmentPos.Add(GetPointOnCurve(curveHandles, t));
            return segmentPos.ToArray();
        }

        Vector3 GetPointOnCurve(Vector3[] curveHandles, float t)
        {
            int currentPoints = curveHandles.Length;
            while (currentPoints > 1)
            {
                for (int i = 0; i < currentPoints - 1; i++)
                    curveHandles[i] = Vector3.Lerp(curveHandles[i], curveHandles[i + 1], t);
                currentPoints--;
            }
            return curveHandles[0];
        }
    }
}
