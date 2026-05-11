using UnityEngine;

/// <summary>
/// Detects when two centrioles have separated far enough apart for long enough
/// to trigger the transition from InterphasePart2 to Prophase.
/// Also provides hand proximity feedback during InterphasePart2.
/// </summary>
public class CentrioleSeparationDetector : MonoBehaviour, IPhaseController
{
    [Header("References")]
    public GameManager gameManager;
    public GameObject partner;
    public GameObject feedbackObject;

    [Header("Runtime Info")]
    public float distanceBetweenCentrioles;

    private float separationTimer  = 0f;
    private float feedbackTimer    = 0f;
    private bool prophaseTriggered = false;
    private bool feedbackShown     = false;
    private bool isActive          = false;

    private const float separationThreshold = 0.15f;
    private const float separationTime      = 3f;
    private const float feedbackDelay       = 3f;

    // ── IPhaseController ──────────────────────────────────────────────────────

    public void OnPhaseEnter(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.InterphasePart2)
        {
            isActive          = true;
            prophaseTriggered = false;
            separationTimer   = 0f;
        }
    }

    public void OnPhaseExit(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.InterphasePart2)
            isActive = false;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!isActive || prophaseTriggered) return;

        distanceBetweenCentrioles = Vector3.Distance(transform.position, partner.transform.position);

        if (distanceBetweenCentrioles >= separationThreshold)
        {
            separationTimer += Time.deltaTime;
            if (separationTimer >= separationTime)
            {
                prophaseTriggered = true;
                gameManager.Prophase();
                Debug.Log("[CentrioleSeparationDetector] Centrioles separated — transitioning to Prophase.");
            }
        }
        else
        {
            separationTimer = 0f;
        }
    }

    // ── Feedback ──────────────────────────────────────────────────────────────

    private void OnTriggerStay(Collider other)
    {
        if (feedbackShown) return;
        if (!other.CompareTag("Left") && !other.CompareTag("Right")) return;

        feedbackTimer += Time.deltaTime;
        if (feedbackTimer >= feedbackDelay)
        {
            if (feedbackObject != null) feedbackObject.SetActive(true);
            feedbackShown = true;
            Debug.Log("[CentrioleSeparationDetector] Feedback shown.");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Left") || other.CompareTag("Right"))
            feedbackTimer = 0f;
    }
}