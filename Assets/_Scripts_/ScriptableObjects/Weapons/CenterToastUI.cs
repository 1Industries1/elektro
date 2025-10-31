// CenterToastUI.cs
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Simple center-screen toast that fades in, holds, and fades out.
/// Uses unscaled time so it works during slow motion.
/// Drop this on a Canvas (Screen Space - Overlay), assign a CanvasGroup and a TMP Text.
/// Keep the GameObject active; alpha controls visibility.
/// </summary>
public class CenterToastUI : MonoBehaviour
{
    public static CenterToastUI Instance { get; private set; }

    [Header("References")]
    [Tooltip("CanvasGroup on the root for fading.")]
    public CanvasGroup group;

    [Tooltip("Centered TextMeshProUGUI to display the message.")]
    public TextMeshProUGUI message;

    [Header("Timing")]
    [Tooltip("Fade-in duration (seconds, unscaled).")]
    public float fadeIn = 0.12f;

    [Tooltip("Fade-out duration (seconds, unscaled).")]
    public float fadeOut = 0.20f;

    [Header("Appearance (optional)")]
    [Tooltip("Optional max width for the text rect (0 = no clamp).")]
    public float maxTextWidth = 0f;

    // Internal queue so multiple toasts don't overwrite each other
    private readonly Queue<(string text, float seconds)> _queue = new();
    private bool _running;
    private Coroutine _current;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // If you want this to persist across scenes, you can swap to DontDestroyOnLoad
            // and destroy duplicates; for now, prefer the first one found.
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (group != null) group.alpha = 0f;
        if (message != null)
        {
            message.text = "";
            if (maxTextWidth > 0f)
            {
                var rt = message.rectTransform;
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, maxTextWidth);
            }
        }
        // gameObject stays active; alpha handles visibility
    }

    /// <summary>
    /// Enqueue a new toast. If another is showing, this one will play after.
    /// </summary>
    /// <param name="text">Message to display (supports TMP rich text).</param>
    /// <param name="seconds">Hold duration (excluding fade times).</param>
    public void Show(string text, float seconds = 3f)
    {
        if (string.IsNullOrEmpty(text) || group == null || message == null) return;
        _queue.Enqueue((text, Mathf.Max(0f, seconds)));
        if (!_running) _current = StartCoroutine(RunQueue());
    }

    /// <summary>
    /// Immediately clears any active toast and pending queue.
    /// </summary>
    public void ClearAll()
    {
        _queue.Clear();
        if (_current != null) StopCoroutine(_current);
        _current = null;
        _running = false;
        if (group != null) group.alpha = 0f;
        if (message != null) message.text = "";
    }

    private IEnumerator RunQueue()
    {
        _running = true;
        while (_queue.Count > 0)
        {
            var (text, seconds) = _queue.Dequeue();
            yield return RunSingle(text, seconds);
        }
        _running = false;
        _current = null;
    }

    private IEnumerator RunSingle(string text, float seconds)
    {
        // Set content
        message.text = text;

        // Fade in (unscaled)
        float t = 0f;
        if (fadeIn <= 0f)
        {
            if (group != null) group.alpha = 1f;
        }
        else
        {
            while (t < fadeIn)
            {
                t += Time.unscaledDeltaTime;
                if (group != null) group.alpha = Mathf.Lerp(0f, 1f, t / fadeIn);
                yield return null;
            }
            if (group != null) group.alpha = 1f;
        }

        // Hold (unscaled)
        float e = 0f;
        while (e < seconds)
        {
            e += Time.unscaledDeltaTime;
            yield return null;
        }

        // Fade out (unscaled)
        t = 0f;
        if (fadeOut <= 0f)
        {
            if (group != null) group.alpha = 0f;
        }
        else
        {
            while (t < fadeOut)
            {
                t += Time.unscaledDeltaTime;
                if (group != null) group.alpha = Mathf.Lerp(1f, 0f, t / fadeOut);
                yield return null;
            }
            if (group != null) group.alpha = 0f;
        }

        // Cleanup content
        message.text = "";
    }
}
