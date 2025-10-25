using System.Collections.Generic;
using UnityEngine;

namespace Tarodev
{
    public class Missile : MonoBehaviour
    {
        [Header("REFERENCES")]
        [SerializeField] private Rigidbody _rb;
        [SerializeField] private Target _target;
        [SerializeField] private GameObject _explosionPrefab;

        [Header("MOVEMENT")]
        [SerializeField] private float _speed = 15;
        [SerializeField] private float _rotateSpeed = 95;

        [Header("EXPLOSION SETTINGS")]
        [SerializeField] public float explosionRadius = 5f;
        [SerializeField] private float explosionForce = 700f;
        [SerializeField] public float missileDamage = 1f;

        [Header("PREDICTION")]
        [SerializeField] private float _maxDistancePredict = 100;
        [SerializeField] private float _minDistancePredict = 5;
        [SerializeField] private float _maxTimePrediction = 5;
        private Vector3 _standardPrediction, _deviatedPrediction;

        [Header("DEVIATION")]
        [SerializeField] private float _deviationAmount = 50;
        [SerializeField] private float _deviationSpeed = 2;

        [Header("SOUND")]
        [SerializeField] private AudioSource _explosionSound;

        // --- Airstrike Punkt-Fallback ---
        private bool _hasPointTarget;
        private Vector3 _pointTarget;

        // --- Dynamische Zielsuche ---
        [Header("DYNAMIC SEEK")]
        [SerializeField] private bool _dynamicSeek = true;
        [SerializeField] private float _seekEvery = 0.15f;       // wie oft neu suchen (Sek.)
        [SerializeField] private float _seekRange = 120f;        // Suchradius
        [SerializeField, Range(0f, 1f)] private float _minFrontDot = 0.1f; // >=0.1 => grob vor der Rakete
        [SerializeField] private float _angleWeight = 10f;       // Winkel-Bias im Score
        private float _seekTimer;

        // Optional: auf Korridor beschränken (vom Controller gesetzt)
        private bool _hasCorridor;
        private Vector3 _corridorOrigin;
        private Vector3 _corridorForward;
        private float _corridorHalfWidth;
        private float _corridorLength;

        public void SetCorridor(Vector3 origin, Vector3 forward, float width, float length)
        {
            _hasCorridor = true;
            _corridorOrigin = origin;
            _corridorForward = forward.normalized;
            _corridorHalfWidth = width * 0.5f;
            _corridorLength = length;
        }

        public void SetTarget(Target target)
        {
            _target = target;
            _hasPointTarget = false; // ggf. von ImpactPoint zurück auf Target
        }

        // Für Airstrike: Direkt auf Bodenpunkt zielen (Fallback, wenn gerade kein Target da ist)
        public void SetImpactPoint(Vector3 point)
        {
            _hasPointTarget = true;
            _pointTarget = point;
            _target = null;
        }

        private void FixedUpdate()
        {
            // Grundbewegung immer beibehalten
            _rb.linearVelocity = transform.forward * _speed;

            // 1) Dynamische Suche regelmäßig triggern
            if (_dynamicSeek)
            {
                _seekTimer -= Time.fixedDeltaTime;
                if (_seekTimer <= 0f)
                {
                    _seekTimer = _seekEvery;

                    // Ziel ungültig oder weg? -> neu suchen
                    if (!IsTargetValid(_target))
                    {
                        var newTarget = FindBestTarget();
                        if (newTarget != null)
                        {
                            _target = newTarget;
                            _hasPointTarget = false; // switch von Impact-Fallback zu echtem Target
                        }
                    }
                }
            }

            // 2) Steuern
            if (IsTargetValid(_target))
            {
                var tRb = _target.Rb; // könnte null sein, wenn der Target-Wrapper keinen Rigidbody hat
                Vector3 tgtPos = tRb ? tRb.position : _target.transform.position;

                var leadTimePercentage = Mathf.InverseLerp(_minDistancePredict, _maxDistancePredict, Vector3.Distance(transform.position, tgtPos));
                PredictMovement(leadTimePercentage, tgtPos, tRb ? tRb.linearVelocity : Vector3.zero);
                AddDeviation(leadTimePercentage);
                RotateTowards(_deviatedPrediction);
            }
            else if (_hasPointTarget)
            {
                RotateTowards(_pointTarget);
            }
            else
            {
                // Kein Ziel & kein Punkt -> geradeaus weiterfliegen (Rotation beibehalten)
            }
        }

        private bool IsTargetValid(Target t)
        {
            if (!t) return false; // Unity "falsy" bei zerstörten Objekten
            float dist = Vector3.Distance(transform.position, t.transform.position);
            if (dist > _seekRange) return false;

            // grob vor der Rakete?
            Vector3 dir = (t.transform.position - transform.position).normalized;
            if (Vector3.Dot(transform.forward, dir) < _minFrontDot) return false;

            // optional: Korridor-Check
            if (_hasCorridor && !IsInCorridor(t.transform.position)) return false;

            return true;
        }

        private bool IsInCorridor(Vector3 worldPos)
        {
            Vector3 to = worldPos - _corridorOrigin;
            float fwdDist = Vector3.Dot(to, _corridorForward);
            if (fwdDist < 0f || fwdDist > _corridorLength) return false;

            Vector3 proj = _corridorForward * fwdDist;
            Vector3 lateralVec = to - proj;
            // Right aus Corridor-Forward ableiten (planar)
            Vector3 right = Vector3.Cross(Vector3.up, _corridorForward).normalized;
            float lateral = Vector3.Dot(lateralVec, right);
            return Mathf.Abs(lateral) <= _corridorHalfWidth;
        }

        private Target FindBestTarget()
        {
            // Lokale Suche mit OverlapSphere (performanter als FindGameObjectsWithTag bei vielen Missiles)
            Collider[] cols = Physics.OverlapSphere(transform.position, _seekRange);
            Target best = null;
            float bestScore = float.PositiveInfinity;

            for (int i = 0; i < cols.Length; i++)
            {
                var c = cols[i];
                if (!c || !c.CompareTag("Enemy")) continue;

                Target t = c.GetComponentInParent<Target>() ?? c.GetComponent<Target>();
                if (!t) continue;

                Vector3 tp = t.transform.position;

                if (_hasCorridor && !IsInCorridor(tp)) continue;

                Vector3 to = tp - transform.position;
                float dist = to.magnitude;
                if (dist > _seekRange) continue;

                Vector3 dir = to / (dist > 0.0001f ? dist : 1f);
                float dot = Mathf.Clamp01((Vector3.Dot(transform.forward, dir) + 1f) * 0.5f); // 0..1
                if (Vector3.Dot(transform.forward, dir) < _minFrontDot) continue;

                // Score: Nähe + Winkel-Bias (kleiner ist besser)
                float score = dist + (1f - dot) * _angleWeight;

                if (score < bestScore)
                {
                    bestScore = score;
                    best = t;
                }
            }

            return best;
        }

        private void PredictMovement(float leadTimePercentage, Vector3 targetPos, Vector3 targetVel)
        {
            var predictionTime = Mathf.Lerp(0, _maxTimePrediction, leadTimePercentage);
            _standardPrediction = targetPos + targetVel * predictionTime;
        }

        private void AddDeviation(float leadTimePercentage)
        {
            var deviation = new Vector3(Mathf.Cos(Time.time * _deviationSpeed), 0, 0);
            var predictionOffset = transform.TransformDirection(deviation) * _deviationAmount * leadTimePercentage;
            _deviatedPrediction = _standardPrediction + predictionOffset;
        }

        private void RotateTowards(Vector3 worldPoint)
        {
            var heading = worldPoint - transform.position;
            if (heading.sqrMagnitude < 0.0001f) return;

            var rotation = Quaternion.LookRotation(heading);
            _rb.MoveRotation(Quaternion.RotateTowards(transform.rotation, rotation, _rotateSpeed * Time.deltaTime));
        }

        private void OnCollisionEnter(Collision collision) => Explode();

        private void Explode()
        {
            if (_explosionPrefab) Instantiate(_explosionPrefab, transform.position, Quaternion.identity);
            if (_explosionSound && _explosionSound.clip) AudioSource.PlayClipAtPoint(_explosionSound.clip, transform.position);

            Collider[] colliders = Physics.OverlapSphere(transform.position, explosionRadius);
            foreach (Collider nearbyObject in colliders)
            {
                Rigidbody rb = nearbyObject.GetComponent<Rigidbody>();
                if (rb != null) rb.AddExplosionForce(explosionForce, transform.position, explosionRadius);

                if (nearbyObject.CompareTag("Enemy"))
                {
                    EnemyMovement enemy = nearbyObject.GetComponent<EnemyMovement>();
                    if (enemy != null) enemy.TakeDamage(missileDamage, 0, enemy.transform.position);
                }
            }
            Destroy(gameObject);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.red; Gizmos.DrawLine(transform.position, _standardPrediction);
            Gizmos.color = Color.green; Gizmos.DrawLine(_standardPrediction, _deviatedPrediction);
        }
    }
}
