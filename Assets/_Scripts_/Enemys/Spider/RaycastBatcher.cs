using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace MimicSpace
{
    /// <summary>
    /// Empfänger-Interface für batchweise Ray/Linecasts – vermeidet GC durch Delegates/Lambdas.
    /// </summary>
    public interface IRaycastReceiver
    {
        void OnRaycastResult(int requestId, bool hit, RaycastHit hitInfo);
    }

    /// <summary>
    /// Sammelt Ray-/Linecasts und führt sie einmal pro Frame parallel aus
    /// (RaycastCommand.ScheduleBatch). Aufruf: Enqueue(...) und später Flush().
    /// </summary>
    public static class RaycastBatcher
    {
        struct Req
        {
            public Vector3 from;
            public Vector3 to;
            public int mask;
            public IRaycastReceiver receiver;
            public int requestId;
        }

        static readonly List<Req> queue = new(256);

        /// <summary>Linecast als Batch-Anfrage (GC-frei).</summary>
        public static void Enqueue(
            Vector3 from,
            Vector3 to,
            int layerMask,
            IRaycastReceiver receiver,
            int requestId = 0)
        {
            queue.Add(new Req
            {
                from = from,
                to = to,
                mask = layerMask,
                receiver = receiver,
                requestId = requestId
            });
        }

        /// <summary>Alle gesammelten Anfragen parallel ausführen. Einmal pro Frame aufrufen (z.B. LateUpdate).</summary>
        public static void Flush()
        {
            int n = queue.Count;
            if (n == 0) return;

            var cmds = new NativeArray<RaycastCommand>(n, Allocator.TempJob);
            var hits = new NativeArray<RaycastHit>(n, Allocator.TempJob);

            for (int i = 0; i < n; i++)
            {
                var q = queue[i];
                Vector3 dir = q.to - q.from;
                float dist = dir.magnitude;
                if (dist <= 0.0001f) dist = 0.0001f;

                cmds[i] = new RaycastCommand(
                    from: q.from,
                    direction: dir / dist,
                    distance: dist,
                    layerMask: q.mask,
                    maxHits: 1
                );
            }

            JobHandle h = RaycastCommand.ScheduleBatch(cmds, hits, 32);
            h.Complete();

            for (int i = 0; i < n; i++)
            {
                bool hit = hits[i].collider != null;
                var q = queue[i];
                q.receiver?.OnRaycastResult(q.requestId, hit, hits[i]);
            }

            cmds.Dispose();
            hits.Dispose();
            queue.Clear();
        }
    }
}
