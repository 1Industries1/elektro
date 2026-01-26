using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PixPlays.ElementalVFX
{
    public class EarthShield : Shield
    {
        [SerializeField] List<Rigidbody> _ShardRigidbodies;
        [SerializeField] float _AnimSpeed = 1f;
        [SerializeField] AnimationCurve _AnimCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] ParticleSystem _SpawnExplosionEffectPrefab;

        [SerializeField] Transform _RotationObjectOuter;
        [SerializeField] Transform _RotationObjectInner;
        [SerializeField] float _RotationSpeedOuter = 60f;
        [SerializeField] float _RotationSpeedInner = 90f;

        [SerializeField] Vector2 _ShardRadiusSpawn = new Vector2(1.5f, 2.5f);
        [SerializeField] ParticleSystem _HitEffectPrefab;
        [SerializeField] float _ShardScale = 1f;

        private List<MeshCollider> _meshColliders;
        private List<Vector3> _sourcePositions;
        private List<Quaternion> _sourceRotations;

        // Public Wrapper (falls Shield-Base keine public Play/Stop APIs hat)
        public void PlayShield() => PlayImplementation();
        public void StopShield() => StopImplemenation();
        public void HitShield(Vector3 point, Vector3 normal) => HitImplementation(point, normal);

        IEnumerator Coroutine_Animate()
        {
            StartCoroutine(Coroutine_Rotate());

            if (_sourcePositions == null)
            {
                _sourcePositions = new List<Vector3>(_ShardRigidbodies.Count);
                _sourceRotations = new List<Quaternion>(_ShardRigidbodies.Count);
                foreach (var rb in _ShardRigidbodies)
                {
                    _sourcePositions.Add(rb.transform.localPosition);
                    _sourceRotations.Add(rb.transform.localRotation);
                }
            }

            if (_meshColliders == null)
            {
                _meshColliders = new List<MeshCollider>();
                foreach (var rb in _ShardRigidbodies)
                {
                    if (rb != null && rb.TryGetComponent<MeshCollider>(out var col))
                    {
                        _meshColliders.Add(col);
                        col.enabled = false;
                    }
                }
            }

            foreach (var col in _meshColliders)
                if (col) col.enabled = false;

            // LOCAL spawn positions/rotations (damit es sauber am Player "klebt")
            var shardLocalSpawnPos = new List<Vector3>(_ShardRigidbodies.Count);
            var shardSpawnRot = new List<Quaternion>(_ShardRigidbodies.Count);

            for (int i = 0; i < _ShardRigidbodies.Count; i++)
            {
                var rb = _ShardRigidbodies[i];
                if (!rb) { shardLocalSpawnPos.Add(Vector3.zero); shardSpawnRot.Add(Quaternion.identity); continue; }

                rb.gameObject.SetActive(true);
                rb.isKinematic = true;
                rb.transform.localScale = Vector3.one * _ShardScale;

                Vector3 dir = rb.transform.localPosition;
                dir.y = 0f;

                if (dir.sqrMagnitude < 0.0001f)
                    dir = Random.onUnitSphere; // fallback falls eine Shard in (0,0,0) liegt
                dir.y = 0f;

                float distance = Random.Range(_ShardRadiusSpawn.x, _ShardRadiusSpawn.y);
                Vector3 localPos = dir.normalized * distance;

                Quaternion randomRot = Quaternion.Euler(
                    Random.Range(0, 360f),
                    Random.Range(0, 360f),
                    Random.Range(0, 360f)
                );

                shardLocalSpawnPos.Add(localPos);
                shardSpawnRot.Add(randomRot);

                if (_SpawnExplosionEffectPrefab)
                {
                    Vector3 worldPos = transform.TransformPoint(localPos);
                    var fx = Instantiate(_SpawnExplosionEffectPrefab, worldPos, Quaternion.identity);
                    fx.Play();
                }
            }

            float lerp = 0f;
            while (lerp < 1f)
            {
                float t = _AnimCurve.Evaluate(lerp);

                for (int i = 0; i < _ShardRigidbodies.Count; i++)
                {
                    var rb = _ShardRigidbodies[i];
                    if (!rb) continue;

                    rb.transform.localPosition = Vector3.Lerp(shardLocalSpawnPos[i], _sourcePositions[i], t);
                    rb.transform.localRotation = Quaternion.Lerp(shardSpawnRot[i], _sourceRotations[i], t);
                }

                lerp += Time.deltaTime * _AnimSpeed;
                yield return null;
            }

            for (int i = 0; i < _ShardRigidbodies.Count; i++)
            {
                var rb = _ShardRigidbodies[i];
                if (!rb) continue;

                rb.transform.localPosition = _sourcePositions[i];
                rb.transform.localRotation = _sourceRotations[i];
            }
        }

        IEnumerator Coroutine_Rotate()
        {
            while (true)
            {
                if (_RotationObjectInner) _RotationObjectInner.Rotate(0, _RotationSpeedInner * Time.deltaTime, 0);
                if (_RotationObjectOuter) _RotationObjectOuter.Rotate(0, _RotationSpeedOuter * Time.deltaTime, 0);
                yield return null;
            }
        }

        IEnumerator Coroutine_StopAnimation()
        {
            foreach (var rb in _ShardRigidbodies)
                if (rb) rb.isKinematic = false;

            foreach (var col in _meshColliders)
                if (col) col.enabled = true;

            float lerp = 0;
            Vector3 startScale = Vector3.one * _ShardScale;
            Vector3 endScale = Vector3.zero;

            while (lerp < 1)
            {
                for (int i = 0; i < _ShardRigidbodies.Count; i++)
                {
                    var rb = _ShardRigidbodies[i];
                    if (!rb) continue;

                    rb.transform.localScale = Vector3.Lerp(startScale, endScale, lerp);
                }

                lerp += Time.deltaTime * _AnimSpeed;
                yield return null;
            }

            for (int i = 0; i < _ShardRigidbodies.Count; i++)
            {
                var rb = _ShardRigidbodies[i];
                if (!rb) continue;

                rb.gameObject.SetActive(false);
            }
        }

        protected override void PlayImplementation()
        {
            StopAllCoroutines();
            StartCoroutine(Coroutine_Animate());
        }

        protected override void StopImplemenation()
        {
            StopAllCoroutines();
            StartCoroutine(Coroutine_StopAnimation());
        }

        protected override void HitImplementation(Vector3 point, Vector3 normal)
        {
            if (!_HitEffectPrefab) return;
            var fx = Instantiate(_HitEffectPrefab, point, Quaternion.identity);
            fx.transform.forward = normal;
            fx.Play();
        }
    }
}
