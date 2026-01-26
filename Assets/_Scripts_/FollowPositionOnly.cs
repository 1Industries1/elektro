using UnityEngine;

public class FollowPositionOnly : MonoBehaviour
{
    public Transform target;

    // wenn true: Rotation bleibt auf dem Stand beim Spawn (dreht nie mit)
    public bool keepInitialRotation = true;

    private Quaternion _fixedWorldRot;

    private void Awake()
    {
        _fixedWorldRot = transform.rotation;
    }

    private void LateUpdate()
    {
        if (!target) return;

        transform.position = target.position;

        if (keepInitialRotation)
            transform.rotation = _fixedWorldRot;
    }
}
