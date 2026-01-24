using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public class AbilityInput : NetworkBehaviour
{
    private PlayerAbilities ab;

    private void Start()
    {
        ab = GetComponent<PlayerAbilities>() ?? GetComponentInParent<PlayerAbilities>();
    }

    private void Update()
    {
        if (!IsOwner || ab == null) return;

        if (Input.GetKeyDown(KeyCode.Q)) ab.RequestUseAbilityServerRpc(0);
        if (Input.GetKeyDown(KeyCode.Y)) ab.RequestUseAbilityServerRpc(1);
    }
}
