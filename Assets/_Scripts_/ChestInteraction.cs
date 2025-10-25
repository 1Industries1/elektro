using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Collider))]
public class ChestInteraction : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Image holdCircle;
    [SerializeField] private string openTrigger = "Open";
    
    [Header("Settings")]
    [Tooltip("Sekunden, die gehalten werden müssen. 0 = Tap-to-open")]
    [SerializeField] private float holdDuration = 3f;
    [Tooltip("Fortschritt fällt zurück, wenn Taste losgelassen?")]
    [SerializeField] private bool resetOnRelease = true;
    [Tooltip("Kann die Truhe mehrmals geöffnet werden?")]
    [SerializeField] private bool singleUse = true;
    [Tooltip("Zeit nach Öffnen, bevor erneut interagiert werden darf (falls singleUse = false).")]
    [SerializeField] private float reopenCooldown = 2f;
    [SerializeField] private string playerTag = "Player";

    [Header("Events")]
    public UnityEvent OnOpened;
    public UnityEvent<float> OnProgress; // 0..1

    // --- intern ---
    private bool inRange;
    private bool opened;
    private float holdTimer;
    private float cooldownUntil;
    private int openTriggerHash;

#if ENABLE_INPUT_SYSTEM
    private static InputAction _interactAction;
#endif

    private void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        var col = GetComponent<Collider>();
        if (col) col.isTrigger = true;

        openTriggerHash = Animator.StringToHash(openTrigger);

        if (holdCircle)
        {
            holdCircle.fillAmount = 0f;
            holdCircle.gameObject.SetActive(false);
        }

#if ENABLE_INPUT_SYSTEM
        if (_interactAction == null)
        {
            _interactAction = new InputAction("Interact", binding: "<Keyboard>/e");
            _interactAction.AddBinding("<Gamepad>/buttonSouth"); // A / Cross
            _interactAction.Enable();
        }
#endif
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsBlocked() || !other.CompareTag(playerTag)) return;
        inRange = true;
        if (holdCircle) holdCircle.gameObject.SetActive(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        inRange = false;
        if (resetOnRelease) holdTimer = 0f;
        UpdateUI();
        if (holdCircle) holdCircle.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!inRange || IsBlocked()) return;

        bool isPressed = IsInteractHeld();

        // Tap-to-open
        if (holdDuration <= 0f)
        {
            if (WasInteractPressedThisFrame())
            {
                OpenChest();
            }
            return;
        }

        // Hold-to-open
        if (isPressed)
        {
            holdTimer += Time.deltaTime;
        }
        else
        {
            if (resetOnRelease) holdTimer = 0f;
            else holdTimer = Mathf.Max(0f, holdTimer - Time.deltaTime);
        }

        UpdateUI();

        if (holdTimer >= holdDuration)
        {
            OpenChest();
        }
    }

    private void UpdateUI()
    {
        float t = holdDuration <= 0f ? 0f : Mathf.Clamp01(holdTimer / holdDuration);
        if (holdCircle) holdCircle.fillAmount = t;
        OnProgress?.Invoke(t);
    }

    private void OpenChest()
    {
        holdTimer = 0f;
        UpdateUI();

        if (singleUse) opened = true;
        cooldownUntil = Time.time + reopenCooldown;

        if (holdCircle) holdCircle.gameObject.SetActive(false);

        if (animator && openTriggerHash != 0)
            animator.SetTrigger(openTriggerHash);

        // Collider aus, damit nicht mehrfach im selben Frame getriggert wird
        var col = GetComponent<Collider>();
        if (col && singleUse) col.enabled = false;

        OnOpened?.Invoke();
    }

    private bool IsBlocked()
    {
        if (singleUse && opened) return true;
        if (!singleUse && Time.time < cooldownUntil) return true;
        return false;
    }

    private bool IsInteractHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return _interactAction != null && _interactAction.IsPressed();
#else
        return Input.GetKey(KeyCode.E);
#endif
    }

    private bool WasInteractPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return _interactAction != null && _interactAction.WasPressedThisFrame();
#else
        return Input.GetKeyDown(KeyCode.E);
#endif
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!GetComponent<Collider>())
        {
            var col = gameObject.AddComponent<SphereCollider>();
            col.isTrigger = true;
            ((SphereCollider)col).radius = 1.2f;
        }
        if (holdCircle) holdCircle.type = Image.Type.Filled;
    }
#endif
}
