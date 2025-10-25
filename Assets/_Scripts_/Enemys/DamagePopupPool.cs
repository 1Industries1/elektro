// DamagePopupPool.cs
using System.Collections.Generic;
using UnityEngine;

public class DamagePopupPool : MonoBehaviour
{
    public static DamagePopupPool Instance { get; private set; }
    public DamagePopup popupPrefab;
    public int prewarm = 16;

    private readonly Queue<DamagePopup> _pool = new();

    void Awake()
    {
        if (Instance && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (popupPrefab == null)
            Debug.LogError("[DamagePopupPool] popupPrefab fehlt!");

        for (int i = 0; i < prewarm; i++)
            _pool.Enqueue(CreateNew());
    }

    DamagePopup CreateNew()
    {
        var p = Instantiate(popupPrefab, transform);
        p.gameObject.SetActive(false);
        return p;
    }

    public DamagePopup Get()
    {
        var p = _pool.Count > 0 ? _pool.Dequeue() : CreateNew();
        return p;
    }

    public void Release(DamagePopup popup)
    {
        popup.gameObject.SetActive(false);
        _pool.Enqueue(popup);
    }

    public void Spawn(Vector3 worldPos, float amount, bool isCrit)
    {
        var p = Get();
        p.Show(worldPos, amount, isCrit);
    }
}
