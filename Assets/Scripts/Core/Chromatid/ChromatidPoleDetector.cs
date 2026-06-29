using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Detects when the player holds a chromatid in the correct pole zone during Anaphase.
/// Place one on each pole zone — set Side to Left or Right in the inspector.
/// Both sides must succeed to trigger Telophase.
/// </summary>
public class ChromatidPoleDetector : MonoBehaviour, IPhaseController
{
    public enum Side { Left, Right }

    [Header("Configuration")]
    public Side side;

    [Header("References")]
    public GameManager gameManager;

    [Header("Loading Circle")]
    public LoadingCircle loadingCircle;

    [Header("On Success")]
    public Sprite checkedSprite;

    [Header("State (Read Only)")]
    [SerializeField] private bool success = false;

    private bool isActive     = false;
    private bool isCompleting = false;
    private string chromatidTag;
    private ChromatidPoleDetector otherSide;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        chromatidTag = side == Side.Left ? "Chromatid1" : "Chromatid2";

        loadingCircle.Initialize();

        foreach (var detector in FindObjectsByType<ChromatidPoleDetector>())
        {
            if (detector != this && detector.side != this.side)
            {
                otherSide = detector;
                break;
            }
        }

        Debug.Assert(otherSide    != null, $"[ChromatidPoleDetector] {side}: could not find the opposite detector!", this);
        Debug.Assert(gameManager  != null, $"[ChromatidPoleDetector] {side}: gameManager is not assigned!", this);
        Debug.Assert(loadingCircle.image != null, $"[ChromatidPoleDetector] {side}: loadingCircle is not assigned!", this);
    }

    private void OnEnable()  => GameManager.Register(this);
    private void OnDisable() => GameManager.Unregister(this);

    // ── IPhaseController ──────────────────────────────────────────────────────

    public void OnPhaseEnter(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Anaphase)
        {
            isActive     = true;
            success      = false;
            isCompleting = false;
            loadingCircle.Reset();

            var chromatids = GameObject.FindGameObjectsWithTag(chromatidTag);
            foreach (var c in chromatids)
            {
                var grab = c.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                if (grab != null) grab.enabled = true;
            }

            Debug.Log($"[ChromatidPoleDetector] {side} reset for Anaphase.");
        }
    }

    public void OnPhaseExit(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Anaphase)
            isActive = false;
    }

    // ── Trigger Detection ─────────────────────────────────────────────────────

    private void OnTriggerStay(Collider other)
    {
        if (!isActive || success || isCompleting) return;
        if (!other.CompareTag(chromatidTag)) return;

        if (loadingCircle.Tick(Time.deltaTime))
            CompleteDetection(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!success && !isCompleting && other.CompareTag(chromatidTag))
            loadingCircle.Reset();
    }

    // ── Completion ────────────────────────────────────────────────────────────

    private void CompleteDetection(Collider chromatidCollider)
    {
        isCompleting = true;
        success      = true;

        loadingCircle.Complete();
        if (checkedSprite != null)
            loadingCircle.SetSprite(checkedSprite);

        // Resolve the chromatid's display name for logging
        string chromatidName = ColliderNameResolver.ResolveName(chromatidCollider.transform);
        string poleName      = side == Side.Left ? "LeftPole" : "RightPole";

        MainLogger.LogOtherEvent($"Action:Dropped:{chromatidName}:{poleName}");
        MainLogger.LogOtherEvent($"System:Chromatid:{side}Placed");

        var grab = chromatidCollider.GetComponent<XRGrabInteractable>();
        if (grab != null) grab.enabled = false;

        var rb = chromatidCollider.GetComponent<Rigidbody>();
        if (rb != null)
        {
            if (!rb.isKinematic)
                rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        Debug.Log($"[ChromatidPoleDetector] {side} side complete.");

        if (otherSide != null && otherSide.success)
        {
            Debug.Log("[ChromatidPoleDetector] Both sides complete — triggering Telophase.");
            gameManager?.Telophase();
        }
    }
}