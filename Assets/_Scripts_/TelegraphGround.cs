using UnityEngine;

public class TelegraphGround : MonoBehaviour
{
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float castHeight = 50f;
    [SerializeField] private float heightOffset = 0.03f;
    [SerializeField] private bool alignToNormal = true;

    void LateUpdate()
    {
        var p = transform.position;
        var origin = p + Vector3.up * castHeight;
        if (Physics.Raycast(origin, Vector3.down, out var hit, castHeight * 2f, groundMask, QueryTriggerInteraction.Ignore))
        {
            transform.position = hit.point + Vector3.up * heightOffset;
            if (alignToNormal)
                transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) * Quaternion.Euler(90f, 0f, 0f);
            else
                transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
        else
        {
            transform.position = new Vector3(p.x, p.y + heightOffset, p.z);
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }
    }
}
