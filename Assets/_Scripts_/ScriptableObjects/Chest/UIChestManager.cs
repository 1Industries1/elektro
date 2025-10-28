using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIChestManager : MonoBehaviour
{
    public static UIChestManager instance;

    // Keine externen Wallet/Collector-Refs mehr
    private TreasureChest currentChest;
    private TreasureChestDropProfile dropProfile;

    [Header("Visual Elements")]
    public GameObject openingVFX;
    public GameObject beamVFX;
    public GameObject fireworks;
    public GameObject doneButton;
    public GameObject curvedBeams;
    public List<ItemDisplays> items = new List<ItemDisplays>();

    [Header("UI Elements")]
    public GameObject chestCover;
    public GameObject chestButton;

    [Header("UI Components")]
    public Image chestPanel;
    public TextMeshProUGUI coinText;

    // Audio
    private AudioSource audioSource;
    public AudioClip pickUpSound;

    // Internal states
    private readonly List<Sprite> icons = new List<Sprite>();
    private bool isAnimating = false;
    private Coroutine chestSequenceCoroutine;
    private float coins;
    private Color originalColor = new Color32(0x42, 0x41, 0x87, 255);

    [System.Serializable]
    public struct ItemDisplays
    {
        public GameObject beam;
        public Image spriteImage;
        public GameObject sprite;
        public GameObject weaponBeam;
    }

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (chestPanel != null)
            originalColor = chestPanel.color;

        if (instance != null && instance != this)
        {
            Debug.LogWarning("More than 1 UI Treasure Chest is found. It has been deleted.");
            Destroy(gameObject);
            return;
        }
        instance = this;
        gameObject.SetActive(false);
    }

    // Activate ohne Coins-Empfänger
    public static void Activate(TreasureChest chest)
    {
        if (!instance)
        {
            Debug.LogWarning("No treasure chest UI GameObject found.");
            return;
        }

        instance.currentChest = chest;
        instance.dropProfile = chest != null ? chest.GetCurrentDropProfile() : null;

        //GameManager.instance.ChangeState(GameManager.GameState.TreasureChest);
        instance.gameObject.SetActive(true);
    }

    public static void NotifyItemReceived(Sprite icon)
    {
        if (instance) instance.icons.Add(icon);
        else Debug.LogWarning("No instance of UIChestManager exists. Unable to update Treasure Chest UI.");
    }

    private IEnumerator FlashWhite(Image image, int times, float flashDuration = 0.2f)
    {
        if (image == null) yield break;

        var prev = image.color;
        for (int i = 0; i < times; i++)
        {
            image.color = Color.white;
            yield return new WaitForSecondsRealtime(flashDuration);

            image.color = prev;
            yield return new WaitForSecondsRealtime(0.2f);
        }
        originalColor = prev;
    }

    private IEnumerator ActivateCurvedBeams(float spawnTime)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, spawnTime));
        if (curvedBeams) curvedBeams.SetActive(true);
    }

    // Coins nur visuell
    private IEnumerator HandleCoinDisplay(float maxCoins)
    {
        if (coinText) coinText.gameObject.SetActive(true);
        float elapsedValue = 0f;
        coins = Mathf.Max(0f, maxCoins);

        while (elapsedValue < coins)
        {
            elapsedValue += Time.unscaledDeltaTime * 20f;
            if (coinText) coinText.text = string.Format("{0:F2}", Mathf.Min(elapsedValue, coins));
            yield return null;
        }

        yield return new WaitForSecondsRealtime(2f);
        if (doneButton) doneButton.SetActive(true);
    }

    private void SetupBeam(int index)
    {
        if (index < 0 || index >= items.Count) return;

        Sprite icon = (index < icons.Count) ? icons[index] : null;

        var id = items[index];
        if (id.weaponBeam) id.weaponBeam.SetActive(true);
        if (id.beam) id.beam.SetActive(true);
        if (id.spriteImage) id.spriteImage.sprite = icon;

        if (dropProfile != null && dropProfile.beamColors != null && index < dropProfile.beamColors.Length)
        {
            var color = dropProfile.beamColors[index];
            if (id.beam)
            {
                if (id.beam.TryGetComponent<Image>(out var img)) img.color = color;
                else if (id.beam.TryGetComponent<SpriteRenderer>(out var sr)) sr.color = color;
            }
        }
    }

    private IEnumerator ShowDelayedBeams(int startIndex, int endIndex)
    {
        if (dropProfile == null) yield break;
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, dropProfile.delayTime));
        for (int i = startIndex; i < endIndex; i++) SetupBeam(i);
    }

    public void DisplayerBeam(int noOfBeams)
    {
        if (dropProfile == null) return;
        int total = Mathf.Clamp(noOfBeams, 0, items.Count);

        int delayedStartIndex = Mathf.Max(0, total - Mathf.Max(0, dropProfile.delayedBeams));

        for (int i = 0; i < delayedStartIndex; i++) SetupBeam(i);

        if (dropProfile.delayedBeams > 0 && delayedStartIndex < total)
            StartCoroutine(ShowDelayedBeams(delayedStartIndex, total));

        StartCoroutine(DisplayItems(total));
    }

    private IEnumerator DisplayItems(int count)
    {
        if (dropProfile != null)
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, dropProfile.animDuration));

        if (count == 5)
        {
            ToggleReveal(0);
            yield return new WaitForSecondsRealtime(0.3f);
            ToggleReveal(1); ToggleReveal(2);
            yield return new WaitForSecondsRealtime(0.3f);
            ToggleReveal(3); ToggleReveal(4);
            yield return new WaitForSecondsRealtime(0.3f);
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                ToggleReveal(i);
                yield return new WaitForSecondsRealtime(0.3f);
            }
        }
    }

    private void ToggleReveal(int index)
    {
        if (index < 0 || index >= items.Count) return;
        var id = items[index];
        if (id.weaponBeam) id.weaponBeam.SetActive(false);
        if (id.sprite) id.sprite.SetActive(true);
    }

    public IEnumerator Open()
    {
        if (dropProfile == null)
        {
            Debug.LogWarning("No drop profile set.");
            yield break;
        }

        if (dropProfile.hasFireworks)
        {
            isAnimating = false;
            if (chestPanel) StartCoroutine(FlashWhite(chestPanel, 5));
            if (fireworks) fireworks.SetActive(true);
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, dropProfile.fireworksDelay));
        }

        isAnimating = true;

        if (dropProfile.hasCurvedBeams && curvedBeams)
            StartCoroutine(ActivateCurvedBeams(dropProfile.curveBeamsSpawnTime));

        float min = Mathf.Min(dropProfile.minCoins, dropProfile.maxCoins);
        float max = Mathf.Max(dropProfile.minCoins, dropProfile.maxCoins);
        StartCoroutine(HandleCoinDisplay(Random.Range(min, max)));
        DisplayerBeam(dropProfile.noOfItems);

        if (openingVFX) openingVFX.SetActive(true);
        if (beamVFX) beamVFX.SetActive(true);

        yield return new WaitForSecondsRealtime(Mathf.Max(0f, dropProfile.animDuration));
        if (openingVFX) openingVFX.SetActive(false);
    }

    public void Begin()
    {
        if (chestCover) chestCover.SetActive(false);
        if (chestButton) chestButton.SetActive(false);

        if (chestSequenceCoroutine != null) StopCoroutine(chestSequenceCoroutine);
        chestSequenceCoroutine = StartCoroutine(Open());

        if (audioSource)
        {
            audioSource.clip = dropProfile != null ? dropProfile.openingSound : null;
            if (audioSource.clip) audioSource.Play();
        }
    }

    private void SkipToRewards()
    {
        if (chestSequenceCoroutine != null)
        {
            StopCoroutine(chestSequenceCoroutine);
            chestSequenceCoroutine = null;
        }

        StopAllCoroutines();

        int count = Mathf.Min(icons.Count, items.Count);
        for (int i = 0; i < count; i++)
        {
            SetupBeam(i);
            ToggleReveal(i);
        }

        if (coinText)
        {
            coinText.gameObject.SetActive(true);
            coinText.text = coins.ToString("F2");
        }
        if (doneButton) doneButton.SetActive(true);
        if (openingVFX) openingVFX.SetActive(false);
        isAnimating = false;
        if (chestPanel) chestPanel.color = originalColor;

        if (audioSource != null && dropProfile != null && dropProfile.openingSound != null)
        {
            audioSource.clip = dropProfile.openingSound;
            float skipToTime = Mathf.Max(0f, audioSource.clip.length - 3.55f);
            audioSource.time = skipToTime;
            audioSource.Play();
        }
    }

    private void Update()
    {
        if (isAnimating && Input.GetButtonDown("Cancel"))
            SkipToRewards();

        if (Input.GetKeyDown(KeyCode.Return))
        {
            TryPressButton(chestButton);
            TryPressButton(doneButton);
        }
    }

    private void TryPressButton(GameObject buttonObj)
    {
        if (!buttonObj || !buttonObj.activeInHierarchy) return;
        if (buttonObj.TryGetComponent<Button>(out var btn) && btn.interactable)
            btn.onClick?.Invoke();
    }

    public void CloseUI()
    {
        // KEINE Gutschrift mehr

        // Reset UI & VFX
        if (chestCover) chestCover.SetActive(true);
        if (chestButton) chestButton.SetActive(true);
        icons.Clear();
        if (beamVFX) beamVFX.SetActive(false);
        if (coinText) coinText.gameObject.SetActive(false);
        if (doneButton) doneButton.SetActive(false);
        if (fireworks) fireworks.SetActive(false);
        if (curvedBeams) curvedBeams.SetActive(false);
        ResetDisplay();

        if (audioSource)
        {
            audioSource.clip = pickUpSound;
            audioSource.time = 0f;
            if (audioSource.clip) audioSource.Play();
        }

        isAnimating = false;

        // Chest informieren -> beendet SlowMo über SlowMoManager
        if (currentChest != null)
        {
            currentChest.OnChestUIClose();
            currentChest = null;
        }

        gameObject.SetActive(false);
    }

    private void ResetDisplay()
    {
        for (int i = 0; i < items.Count; i++)
        {
            var id = items[i];
            if (id.beam) id.beam.SetActive(false);
            if (id.sprite) id.sprite.SetActive(false);
            if (id.spriteImage) id.spriteImage.sprite = null;
        }
        dropProfile = null;
        icons.Clear();
    }
}
