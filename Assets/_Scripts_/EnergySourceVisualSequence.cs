using UnityEngine;
using System.Collections.Generic;

public class EnergySourceVisualSequence : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float cubeRevealInterval = 30f;
    [SerializeField] private List<GameObject> cubes = new();

    private int revealedCount = 0;
    private float timer;

    public int RevealedCount => revealedCount;
    public List<GameObject> Cubes => cubes; // Zugriff für Charger

    private void Start()
    {
        foreach (var cube in cubes)
        {
            if (cube != null) cube.SetActive(false);
        }

        timer = cubeRevealInterval;
    }

    private void Update()
    {
        if (revealedCount >= cubes.Count) return;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            RevealNextCube();
            timer = cubeRevealInterval;
        }
    }

    private void RevealNextCube()
    {
        if (revealedCount < cubes.Count && cubes[revealedCount] != null)
        {
            GameObject cube = cubes[revealedCount];
            cube.SetActive(true);

            StartCoroutine(PopInEffect(cube.transform));
            revealedCount++;
        }
    }

    private System.Collections.IEnumerator PopInEffect(Transform cube)
    {
        float t = 0f;
        Vector3 targetScale = Vector3.one;
        cube.localScale = Vector3.zero;

        while (t < 1f)
        {
            t += Time.deltaTime * 3f;
            cube.localScale = Vector3.Lerp(Vector3.zero, targetScale, t);
            yield return null;
        }

        cube.localScale = targetScale;
    }

    // ⚡ Helfer: Deaktiviert den ältesten aktiven Cube
    public bool ConsumeOneCube()
    {
        for (int i = 0; i < cubes.Count; i++)
        {
            if (cubes[i] && cubes[i].activeSelf)
            {
                cubes[i].SetActive(false);
                return true;
            }
        }
        return false;
    }
}
