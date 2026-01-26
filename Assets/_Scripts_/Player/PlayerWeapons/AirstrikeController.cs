using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Tarodev
{
    public class AirstrikeController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject _missilePrefab;
        [SerializeField] private Transform _firePoint;          // optional: für Auto-Feuer
        [SerializeField] private Transform _owner;              // Standard: wird auf this.transform gesetzt
        private Target _currentTarget;

        [Header("Timer Settings (optional: Auto-Feuer)")]
        [SerializeField] private bool _autoFireEnabled = false;
        [SerializeField] private float fireInterval = 5f;
        private float timeSinceLastFire = 0f;

        [Header("Strike Corridor")]
        [SerializeField] private KeyCode _airstrikeKey = KeyCode.Q;
        [SerializeField] private int missilesPerStrike = 15;
        [SerializeField] private float salvoDuration = 1.5f;    // über diese Zeit werden alle Raketen abgefeuert
        [SerializeField] private float spawnBehindDistance = 6f;// Abstand hinter dem Spieler, an dem gespawnt wird
        [SerializeField] private float spawnHeight = 1.2f;      // Höhe relativ zum Spieler (damit nichts im Boden spawnt)
        [SerializeField] private float corridorWidth = 12f;     // verstellbar mit Mausrad während Auswahl
        [SerializeField] private float minWidth = 2f;
        [SerializeField] private float maxWidth = 60f;
        [SerializeField] private float corridorLength = 80f;    // wie weit nach vorne der Korridor geht
        [SerializeField] private bool lockToOwnerForward = true;// Korridor immer an Spieler-Forward ausrichten

        [Header("Selection Lines")]
        [SerializeField] private LineRenderer leftLine;         // kann leer bleiben – wird dann zur Laufzeit erstellt
        [SerializeField] private LineRenderer rightLine;        // kann leer bleiben – wird dann zur Laufzeit erstellt
        [SerializeField] private float lineWidth = 0.06f;
        [SerializeField] private float lineYOffset = 0.05f;
        [SerializeField] private Material lineMaterial;         // optional (sonst Default-Sprite-Material)

        private bool _selecting;

        private void Awake()
        {
            if (_owner == null) _owner = transform;
        }

        private void Update()
        {
            // Q: Airstrike-Selektionsmodus starten
            if (Input.GetKeyDown(_airstrikeKey) && !_selecting)
                StartCoroutine(BeginAirstrikeSelection());

            // Auswahl aktiv: Linien positionieren & Breite anpassen
            if (_selecting)
            {
                UpdateSelectionLines();

                // Breite mit Mausrad ändern
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(scroll) > 0.0001f)
                {
                    corridorWidth -= scroll * 10f;
                    corridorWidth = Mathf.Clamp(corridorWidth, minWidth, maxWidth);
                }

                if (Input.GetMouseButtonDown(0)) // Linksklick bestätigt
                {
                    StartCoroutine(ExecuteAirstrikeCorridor());
                    EndAirstrikeSelection();
                }
                else if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)) // Abbruch
                {
                    EndAirstrikeSelection();
                }
            }

            // Optional: dein altes Auto-Feuern (deaktiviert während Auswahl)
            if (_autoFireEnabled && !_selecting)
            {
                timeSinceLastFire += Time.deltaTime;
                if (timeSinceLastFire >= fireInterval)
                {
                    FindClosestEnemy();
                    FireMissileAtClosestEnemy();
                    timeSinceLastFire = 0f;
                }
            }
        }

        #region Airstrike flow

        private IEnumerator BeginAirstrikeSelection()
        {
            _selecting = true;

            // Kamera-Verhalten wie gehabt (falls du CameraFollow hast)
            var cam = CameraFollow.Instance;
            if (cam != null)
            {
                cam.StoreOffset();
                cam.SetZoomLocked(true);

                //Vector3 tacticalOffset = cam.offset;

                // Hälfte vom erlaubten Höhen- und Distanzbereich nehmen
                //tacticalOffset.y = (cam.minY + cam.maxY) * 0.5f;
                //tacticalOffset.z = (cam.minZ + cam.maxZ) * 0.5f;

                //cam.ZoomTo(tacticalOffset, 0.2f);
            }

            EnsureSelectionLines(true);
            UpdateSelectionLines();
            yield return null;
        }

        private void EndAirstrikeSelection()
        {
            _selecting = false;
            EnsureSelectionLines(false);

            var cam = CameraFollow.Instance;
            if (cam != null)
            {
                cam.RestoreStoredOffset(0.35f);
                cam.SetZoomLocked(false);
            }
        }

        /// <summary>
        /// Spawnt Raketen hinter dem Spieler quer über die Korridorbreite
        /// und weist ihnen Ziele im Korridor zu (falls vorhanden).
        /// </summary>
        private IEnumerator ExecuteAirstrikeCorridor()
        {
            // Ziele im Korridor vorab sammeln (reduziert Suche pro Rakete)
            List<Target> corridorTargets = FindTargetsInCorridor();

            for (int i = 0; i < missilesPerStrike; i++)
            {
                // Zufällige seitliche Versetzung innerhalb der Breite
                float half = corridorWidth * 0.5f;
                float lateral = Random.Range(-half, half);

                // Basis-Transform
                Vector3 fwd = _owner.forward;
                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized * -1f; // Unity: up x fwd = right (prüfen Richtung), mit -1 drehen wir zur üblichen Transform.right
                // Alternativ: wenn _owner.right verfügbar/gewünscht:
                right = _owner.right;

                // Spawnposition: hinter dem Spieler + seitlich + leicht erhöht
                Vector3 spawnPos = _owner.position 
                                   - fwd * spawnBehindDistance 
                                   + right * lateral
                                   + Vector3.up * spawnHeight;

                Quaternion rot = Quaternion.LookRotation(fwd, Vector3.up);

                GameObject missileObj = Instantiate(_missilePrefab, spawnPos, rot);
                Missile missile = missileObj.GetComponent<Missile>();

                if (missile != null)
                {
                    // Korridor-Infos setzen, damit die Missile ihre Suche darauf beschränkt
                    missile.SetCorridor(_owner.position, _owner.forward, corridorWidth, corridorLength);

                    // Optional: initiales Target (nicht nötig, aber schnellerer Lock-On)
                    // var tgt = corridorTargets.Count > 0 ? corridorTargets[Random.Range(0, corridorTargets.Count)] : null;
                    // if (tgt != null) missile.SetTarget(tgt);
                    // else            missile.SetImpactPoint(_owner.position + fwd * corridorLength);

                    // Fallback: wenn du initial KEIN Target zuweist:
                    missile.SetImpactPoint(_owner.position + fwd * corridorLength);
                }

                // gleichmäßig über die Dauer verteilen
                if (missilesPerStrike > 1)
                    yield return new WaitForSeconds(salvoDuration / (missilesPerStrike - 1));
            }
        }

        /// <summary>
        /// Alle Gegner mit Tag "Enemy" sammeln, die innerhalb des Korridors vor dem Spieler liegen.
        /// </summary>
        private List<Target> FindTargetsInCorridor()
        {
            List<Target> result = new List<Target>();
            GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
            if (enemies == null || enemies.Length == 0) return result;

            Vector3 origin = _owner.position;
            Vector3 fwd = _owner.forward;
            float half = corridorWidth * 0.5f;

            foreach (var enemy in enemies)
            {
                var t = enemy.GetComponent<Target>();
                if (t == null) continue;

                Vector3 toEnemy = enemy.transform.position - origin;

                // Vor dem Spieler?
                float forwardDist = Vector3.Dot(toEnemy, fwd);
                if (forwardDist < 0f || forwardDist > corridorLength) continue;

                // Querabstand zur Center-Line
                Vector3 projOnFwd = fwd * forwardDist;
                Vector3 lateralVec = toEnemy - projOnFwd;
                float lateralAbs = Vector3.Dot(lateralVec, _owner.right); // signed lateral
                if (Mathf.Abs(lateralAbs) <= half)
                {
                    result.Add(t);
                }
            }

            return result;
        }

        #endregion

        #region Selection line helpers

        private void EnsureSelectionLines(bool enable)
        {
            if (leftLine == null)
            {
                leftLine = CreateLineRenderer("Airstrike_LeftLine");
            }
            if (rightLine == null)
            {
                rightLine = CreateLineRenderer("Airstrike_RightLine");
            }

            leftLine.gameObject.SetActive(enable);
            rightLine.gameObject.SetActive(enable);
        }

        private LineRenderer CreateLineRenderer(string name)
        {
            GameObject go = new GameObject(name);
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.loop = false;
            lr.useWorldSpace = true;
            lr.widthMultiplier = lineWidth;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.alignment = LineAlignment.View; // gut sichtbar
            lr.material = lineMaterial != null ? lineMaterial : new Material(Shader.Find("Sprites/Default"));
            lr.startColor = new Color(1, 1, 1, 0.9f);
            lr.endColor   = new Color(1, 1, 1, 0.9f);
            return lr;
        }

        private void UpdateSelectionLines()
        {
            if (!leftLine || !rightLine) return;

            Vector3 basePos = _owner.position + Vector3.up * lineYOffset;

            // Richtung des Korridors (optional mit Spieler-Forward locken)
            Vector3 fwd = (_owner != null && lockToOwnerForward) ? _owner.forward : transform.forward;

            // Quer-Vektor
            Vector3 right = _owner.right;

            float half = corridorWidth * 0.5f;

            // Linke & rechte Startpunkte (auf Höhe des Spielers)
            Vector3 leftStart  = basePos - right * half;
            Vector3 rightStart = basePos + right * half;

            // Endpunkte am Korridorende
            Vector3 leftEnd  = leftStart  + fwd * corridorLength;
            Vector3 rightEnd = rightStart + fwd * corridorLength;

            leftLine.positionCount = 2;
            rightLine.positionCount = 2;
            leftLine.SetPosition(0, leftStart);
            leftLine.SetPosition(1, leftEnd);
            rightLine.SetPosition(0, rightStart);
            rightLine.SetPosition(1, rightEnd);
        }

        #endregion

        #region Dein altes Zielsuchen/Auto-Feuer (optional)

        private void FireMissileAtClosestEnemy()
        {
            if (_missilePrefab == null || _firePoint == null) return;

            GameObject missileObj = Instantiate(_missilePrefab, _firePoint.position, _firePoint.rotation);
            Missile missile = missileObj.GetComponent<Missile>();

            if (missile != null)
            {
                GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
                if (enemies.Length > 0)
                {
                    GameObject closestEnemy = null;
                    float closestDistance = Mathf.Infinity;

                    foreach (GameObject enemy in enemies)
                    {
                        Target target = enemy.GetComponent<Target>();
                        if (target != null)
                        {
                            float distance = Vector3.Distance(missile.transform.position, enemy.transform.position);
                            if (distance < closestDistance)
                            {
                                closestDistance = distance;
                                closestEnemy = enemy;
                            }
                        }
                    }

                    if (closestEnemy != null)
                    {
                        Target closestTarget = closestEnemy.GetComponent<Target>();
                        missile.SetTarget(closestTarget);
                    }
                    else Debug.LogWarning("No valid target found!");
                }
            }
        }

        private void FindClosestEnemy()
        {
            GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
            float closestDistance = Mathf.Infinity;
            Target closestTarget = null;

            foreach (GameObject enemy in enemies)
            {
                float distance = Vector3.Distance(transform.position, enemy.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTarget = enemy.GetComponent<Target>();
                }
            }

            if (closestTarget != null) _currentTarget = closestTarget;
        }

        #endregion
    }
}
