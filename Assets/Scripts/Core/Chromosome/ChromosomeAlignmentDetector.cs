using UnityEngine;

/// <summary>
/// Placed on the metaphase target zone (P_Position_Meta).
/// Detects when the chromosome enters the zone and shows a loading circle.
/// On completion, triggers Anaphase.
/// </summary>
public class ChromosomeAlignmentDetector : MonoBehaviour, IPhaseController
{
    [Header("References")]
    public GameManager gameManager;

    [Header("Loading Circle")]
    public LoadingCircle loadingCircle;

    [Header("Visual")]
    public GameObject fairyDust;

    [Header("Timing")]
    public float startDelay = 3f;

    [Header("State")]
    public bool alignmentSuccess = false;

    private bool isActive = false;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        loadingCircle.Initialize();
    }

    private void OnEnable()  => GameManager.Register(this);
    private void OnDisable() => GameManager.Unregister(this);

    // ── IPhaseController ──────────────────────────────────────────────────────

    public void OnPhaseEnter(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Metaphase)
        {
            alignmentSuccess = false;
            isActive         = false;
            loadingCircle.Reset();
            loadingCircle.SetVisible(false);
            StartCoroutine(ActivateAfterDelay());
        }
    }

    public void OnPhaseExit(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Metaphase)
        {
            StopAllCoroutines();
            isActive = false;
            if (fairyDust != null) fairyDust.SetActive(false);
            loadingCircle.Reset();
            loadingCircle.SetVisible(false);
        }
    }

    // ── Trigger Detection ─────────────────────────────────────────────────────

    private void OnTriggerStay(Collider other)
    {
        if (!isActive || alignmentSuccess) return;
        if (!other.CompareTag("Player")) return;

        loadingCircle.SetVisible(true);
        loadingCircle.SetColor(new Color32(0, 255, 0, 155));

        if (loadingCircle.Tick(Time.deltaTime))
            CompleteAlignment(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && !alignmentSuccess)
            loadingCircle.Reset();
    }

    // ── Completion ────────────────────────────────────────────────────────────

    private void CompleteAlignment(Collider chromosomeCollider)
    {
        alignmentSuccess = true;
        loadingCircle.SetVisible(false);

        // Resolve the chromosome's display name for logging
        string chromosomeName = ColliderNameResolver.ResolveName(chromosomeCollider.transform);
        if (string.IsNullOrEmpty(chromosomeName))
            chromosomeName = "Chromosome";

        MainLogger.LogOtherEvent($"Action:Dropped:{chromosomeName}:MetaphaseCenter");
        MainLogger.LogOtherEvent("System:Chromosome:Aligned");

        Debug.Log("[ChromosomeAlignmentDetector] Chromosome aligned — transitioning to Anaphase.");
        StartCoroutine(TransitionNextFrame());
    }

    // ── Coroutines ────────────────────────────────────────────────────────────

    private System.Collections.IEnumerator ActivateAfterDelay()
    {
        if (fairyDust != null) fairyDust.SetActive(false);
        yield return new WaitForSeconds(startDelay);
        if (fairyDust != null) fairyDust.SetActive(true);
        isActive = true;
    }

    private System.Collections.IEnumerator TransitionNextFrame()
    {
        yield return null;
        if (gameManager != null) gameManager.Anaphase();
    }
}