using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MimicSpace
{
    public class Mimic : MonoBehaviour
    {
        [Header("Animation")]
        public GameObject legPrefab;

        [Range(2, 20)] public int numberOfLegs = 5;
        [Range(1, 10), Tooltip("The number of splines per leg")] public int partsPerLeg = 4;
        int maxLegs;

        [Header("Runtime")]
        public int legCount;
        public int deployedLegs;
        [Range(0, 19)] public int minimumAnchoredLegs = 2;
        public int minimumAnchoredParts;

        [Header("Timing")]
        [Tooltip("Minimum duration before leg is replaced")] public float minLegLifetime = 5;
        [Tooltip("Maximum duration before leg is replaced")] public float maxLegLifetime = 15;
        [Tooltip("Minimum duration before a new leg can be placed")] public float newLegCooldown = 0.3f;

        [Header("Placement")]
        public Vector3 legPlacerOrigin = Vector3.zero;
        [Tooltip("Leg placement radius offset")] public float newLegRadius = 3;
        public float minLegDistance = 4.5f;
        public float maxLegDistance = 6.3f;

        [Header("Curve")]
        [Range(2, 50), Tooltip("Number of spline samples per legpart")] public int legResolution = 40;
        [Tooltip("Minimum lerp coef for leg growth smoothing")] public float minGrowCoef = 4.5f;
        [Tooltip("Maximum lerp coef for leg growth smoothing")] public float maxGrowCoef = 6.5f;

        [Header("Physics")]
        [Tooltip("Ray/Linecasts only hit these layers (e.g. Default, Ground, Terrain)")]
        public LayerMask groundMask = ~0;

        [Header("Motion feed")]
        [Tooltip("Must be updated (x,0,z) to place legs forward")]
        public Vector3 velocity;

        bool canCreateLeg = true;
        readonly List<GameObject> availableLegPool = new List<GameObject>();

        void Start()
        {
            ResetMimic();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying)
                ResetMimic();
        }
#endif

        private void ResetMimic()
        {
            // Nur eigene Legs löschen (nicht global!)
            foreach (var leg in GetComponentsInChildren<Leg>(true))
                Destroy(leg.gameObject);

            legCount = 0;
            deployedLegs = 0;

            maxLegs = numberOfLegs * partsPerLeg;
            Vector2 randV = Random.insideUnitCircle;
            velocity = new Vector3(randV.x, 0, randV.y);
            minimumAnchoredParts = minimumAnchoredLegs * partsPerLeg;
            maxLegDistance = newLegRadius * 2.1f;
        }

        IEnumerator NewLegCooldown()
        {
            canCreateLeg = false;
            yield return new WaitForSeconds(newLegCooldown);
            canCreateLeg = true;
        }

        void Update()
        {
            if (!canCreateLeg) return;

            // vor die Lauf-Richtung
            var v = velocity;
            v.y = 0;
            var vNorm = v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
            legPlacerOrigin = transform.position + vNorm * newLegRadius;

            if (legCount <= maxLegs - partsPerLeg)
            {
                // Kandidatenpunkt
                Vector2 offset = Random.insideUnitCircle * newLegRadius;
                Vector3 newLegPosition = legPlacerOrigin + new Vector3(offset.x, 0, offset.y);

                // Wenn hinter uns, spiegeln
                if (velocity.magnitude > 1f)
                {
                    float newLegAngle = Vector3.Angle(velocity, newLegPosition - transform.position);
                    if (Mathf.Abs(newLegAngle) > 90)
                        newLegPosition = transform.position - (newLegPosition - transform.position);
                }

                // Mindestabstand
                var bodyXZ = new Vector3(transform.position.x, 0, transform.position.z);
                var placerXZ = new Vector3(legPlacerOrigin.x, 0, legPlacerOrigin.z);
                if (Vector3.Distance(bodyXZ, placerXZ) < minLegDistance)
                    newLegPosition = ((newLegPosition - transform.position).normalized * minLegDistance) + transform.position;

                // Winkel begrenzen
                if (Vector3.Angle(velocity, newLegPosition - transform.position) > 45)
                    newLegPosition = transform.position + ((newLegPosition - transform.position) + vNorm * (newLegPosition - transform.position).magnitude) / 2f;

                // ROBUSTER BODEN-TREFFER
                if (!Physics.Raycast(newLegPosition + Vector3.up * 20f, Vector3.down, out var hit, 60f, groundMask, QueryTriggerInteraction.Ignore))
                    return;

                Vector3 myHit = hit.point;

                // Optionale Sichtlinie zum Fußpunkt (nur Boden)
                if (Physics.Linecast(transform.position + Vector3.up * 0.2f, myHit, out var hit2, groundMask, QueryTriggerInteraction.Ignore))
                    myHit = hit2.point;

                float lifeTime = Random.Range(minLegLifetime, maxLegLifetime);

                StartCoroutine(NewLegCooldown());
                for (int i = 0; i < partsPerLeg; i++)
                {
                    RequestLeg(myHit, legResolution, maxLegDistance, Random.Range(minGrowCoef, maxGrowCoef), this, lifeTime);
                    if (legCount >= maxLegs) return;
                }
            }
        }

        void RequestLeg(Vector3 footPosition, int legResolution, float maxLegDistance, float growCoef, Mimic myMimic, float lifeTime)
        {
            GameObject newLeg;
            if (availableLegPool.Count > 0)
            {
                newLeg = availableLegPool[availableLegPool.Count - 1];
                availableLegPool.RemoveAt(availableLegPool.Count - 1);
            }
            else
            {
                newLeg = Instantiate(legPrefab, transform.position, Quaternion.identity);
            }

            newLeg.SetActive(true);
            newLeg.GetComponent<Leg>().Initialize(footPosition, legResolution, maxLegDistance, growCoef, myMimic, lifeTime);
            newLeg.transform.SetParent(myMimic.transform);
        }

        public void RecycleLeg(GameObject leg)
        {
            availableLegPool.Add(leg);
            leg.SetActive(false);
        }
    }
}
