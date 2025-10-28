using UnityEngine;

[CreateAssetMenu(fileName = "TreasureChestDropProfile", menuName = "Test/Treasure Chest Drop Profile")]
public class TreasureChestDropProfile : ScriptableObject
{
    [Header("General Settings")]
    public string profileName = "Drop Profile";
    [Range(0, 1)] public float luckScaling = 0f;   // used as a linear multiplier
    [Range(0, 100)] public float baseDropChance = 100f;
    public float animDuration = 0.5f;

    [Header("Item Display Settings")]
    public int noOfItems = 1;
    public Color[] beamColors = new Color[] { new Color(0, 0, 1, 0.6f) };

    [Range(0, 100f)] public float delayTime = 0f;
    public int delayedBeams = 0;

    public bool hasCurvedBeams = false;
    public float curveBeamsSpawnTime = 0f;

    [Header("Optional Fireworks")]
    public bool hasFireworks = false;          // FIX: added to match UIChestManager usage
    public float fireworksDelay = 1.0f;        // FIX: added to match UIChestManager usage

    [Header("Coins")]
    public float maxCoins = 0f;
    public float minCoins = 0f;

    [Header("Chest Sound Effects")]
    public AudioClip openingSound;
}
