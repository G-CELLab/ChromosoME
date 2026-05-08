using UnityEngine;

/// <summary>
/// Detects when the player holds the chromosome in the metaphase plate zone
/// long enough to trigger Anaphase. Also enables and disables spindle fiber
/// line renderers when Metaphase begins and ends.
/// </summary>
public class MetaphaseAlignmentDetector : MonoBehaviour, IPhaseController
{
    [Header("References")]
    public GameManager gameManager;
    public GameObject rightLineRenderer;

    [Header("Loading Circle")]
    public LoadingCircle loadingCircle;

    [Header("State")]
    public bool alignmentSuccess = false;

    private bool isActive = false;

    // ── IPhaseController ──────────────────────────────────────────────────────

    public void OnPhaseEnter(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Metaphase)
        {
            isActive = true;
            EnableSpindleFibers(true);
        }
    }

    public void OnPhaseExit(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Metaphase)
        {
            isActive = false;
            if (!alignmentSuccess)
            {
                alignmentSuccess = false;
                loadingCircle.Reset();
                EnableSpindleFibers(false);
            }
        }
    }

    // ── Trigger Detection ─────────────────────────────────────────────────────

    private void OnTriggerStay(Collider other)
    {
        if (!isActive || alignmentSuccess) return;
        if (!other.CompareTag("Meta")) return;

        loadingCircle.SetVisible(true);
        loadingCircle.SetColor(new Color32(0, 255, 0, 155));

        if (loadingCircle.Tick(Time.deltaTime))
            CompleteAlignment();
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Meta") && !alignmentSuccess)
            loadingCircle.Reset();
    }

    // ── Completion ────────────────────────────────────────────────────────────

    private void CompleteAlignment()
    {
        alignmentSuccess = true;
        loadingCircle.SetVisible(false);
        Debug.Log("[MetaphaseAlignmentDetector] Chromosome aligned — transitioning to Anaphase.");
        if (gameManager != null) gameManager.Anaphase();
    }

    // ── Spindle Fibers ────────────────────────────────────────────────────────

    private void EnableSpindleFibers(bool enable)
    {
        LineRenderer leftLine = GetComponent<LineRenderer>();
        if (leftLine != null) leftLine.enabled = enable;

        if (rightLineRenderer != null)
        {
            rightLineRenderer.SetActive(enable);
            LineRenderer rightLine = rightLineRenderer.GetComponent<LineRenderer>();
            if (rightLine != null) rightLine.enabled = enable;
        }
    }
}