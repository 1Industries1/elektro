using System.Collections.Generic;
using UnityEngine;

namespace MimicSpace
{
    [RequireComponent(typeof(LineRenderer))]
    [RequireComponent(typeof(MeshFilter))]
    public class CylinderMesh : MonoBehaviour
    {
        [Header("Tube Settings")]
        [Min(3)] public int verticeCount = 8;
        [Tooltip("Sekunden zwischen automatischen Rebuilds (0 = nur bei Änderung).")]
        [Min(0f)] public float updateInterval = 0.05f;
        [Tooltip("Endkappe am letzten Punkt bauen?")]
        public bool buildEndCap = false;

        LineRenderer lr;
        MeshFilter mf;
        Mesh mesh;

        // Arbeitsdaten (wiederverwendet)
        readonly List<Vector3> vertices = new List<Vector3>(2048);
        readonly List<int> triangles = new List<int>(4096);

        Vector3[] posBuf = new Vector3[64];
        Vector3[] prevPos = new Vector3[0];
        int prevCount = -1;
        float nextUpdateTime;

        void Awake()
        {
            lr = GetComponent<LineRenderer>();
            mf = GetComponent<MeshFilter>();
            mesh = new Mesh { name = "TubeMesh" };
            mesh.MarkDynamic();
            mf.sharedMesh = mesh;
        }

        void Update()
        {
            if (updateInterval > 0f && Time.time < nextUpdateTime)
                return;

            if (!FetchPositions(out int count, out bool changed))
                return;

            nextUpdateTime = Time.time + updateInterval;

            if (changed)
                BuildMesh(posBuf, count);
        }

        bool FetchPositions(out int count, out bool changed)
        {
            count = lr.positionCount;
            changed = false;
            if (count < 2) return false;

            // Buffergröße sichern
            if (posBuf.Length < count)
                posBuf = new Vector3[Mathf.NextPowerOfTwo(count)];
            if (prevPos.Length < count)
                prevPos = new Vector3[Mathf.NextPowerOfTwo(count)];

            lr.GetPositions(posBuf);

            if (count != prevCount)
            {
                changed = true;
            }
            else
            {
                // Positionsänderung heuristisch prüfen
                for (int i = 0; i < count; i++)
                {
                    if ((posBuf[i] - prevPos[i]).sqrMagnitude > 1e-6f)
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (changed)
            {
                // neuen Zustand merken
                for (int i = 0; i < count; i++) prevPos[i] = posBuf[i];
                prevCount = count;
            }

            return true;
        }

        void BuildMesh(Vector3[] points, int count)
        {
            mesh.Clear(false);
            vertices.Clear();
            triangles.Clear();

            if (verticeCount < 3) verticeCount = 3;

            // Vorberechnungen
            float angleRad = Mathf.PI * 2f / verticeCount;
            float cos = Mathf.Cos(angleRad);
            float sin = Mathf.Sin(angleRad);

            Matrix4x4 worldToLocal = transform.worldToLocalMatrix;

            // Für jede Segmentstrecke einen Ring erzeugen
            for (int i = 0; i < count - 1; i++)
            {
                Vector3 p0 = points[i];
                Vector3 p1 = points[i + 1];
                Vector3 segDir = (p1 - p0).normalized;

                // Basis: irgendein Vektor senkrecht zur Richtung
                Vector3 side = Vector3.Cross(segDir, Vector3.up);
                if (side.sqrMagnitude < 1e-6f) side = Vector3.Cross(segDir, Vector3.right);
                side.Normalize();

                // Radius entlang der WidthCurve
                float tCurve = (count <= 1) ? 0f : (float)i / (float)(count - 1);
                float radius = 0.5f * lr.widthMultiplier *
                               (lr.widthCurve != null ? lr.widthCurve.Evaluate(tCurve) : 1f);

                side *= radius;

                // zweites orthogonales Achsenstück für Rotationen um segDir
                // Rotation per 2D-Rot in der Ebene (side, side×dir) – schneller als Quaternion pro Vertex
                Vector3 up2 = Vector3.Cross(segDir, side).normalized * radius;

                for (int j = 0; j < verticeCount; j++)
                {
                    // Punkt der Kreis-Umrandung in Welt, dann in Local
                    Vector3 ringPointWorld = p0 + side;
                    vertices.Add(worldToLocal.MultiplyPoint3x4(ringPointWorld));

                    // side = rot(angle) * side  (in der Ebene aufgespannt von side/up2)
                    float sx = side.x, sy = side.y, sz = side.z;
                    float ux = up2.x,  uy = up2.y,  uz = up2.z;
                    // 2D-rotation in der Basis (side, up2):
                    side.Set(cos * sx + -sin * ux, cos * sy + -sin * uy, cos * sz + -sin * uz);
                    up2.Set( sin * sx + cos * ux,  sin * sy + cos * uy,  sin * sz + cos * uz);
                }
            }

            // TRIANGLES
            int rings = count - 1;
            for (int r = 0; r < rings - 1; r++)
            {
                int ringA = r * verticeCount;
                int ringB = (r + 1) * verticeCount;

                for (int j = 0; j < verticeCount; j++)
                {
                    int a = ringA + j;
                    int b = ringA + ((j + 1) % verticeCount);
                    int c = ringB + j;
                    int d = ringB + ((j + 1) % verticeCount);

                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    triangles.Add(c); triangles.Add(b); triangles.Add(d);
                }
            }

            // optionale Endkappe am letzten Punkt
            if (buildEndCap && rings > 0)
            {
                int lastRing = (rings - 1) * verticeCount;
                // Mittelpunkt (letzter LR-Punkt)
                int centerIndex = vertices.Count;
                vertices.Add(worldToLocal.MultiplyPoint3x4(points[count - 1]));

                for (int j = 0; j < verticeCount; j++)
                {
                    int a = lastRing + j;
                    int b = lastRing + ((j + 1) % verticeCount);
                    triangles.Add(a); triangles.Add(b); triangles.Add(centerIndex);
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateNormals();   // ggf. teuer – nur bei Änderung aufgerufen
            mesh.RecalculateBounds();
        }
    }
}
