using UnityEngine;

/// <summary>
/// Moves a chromosome to its target position during Metaphase and/or Anaphase.
/// Assign end locations in the inspector. Each phase movement is independent.
/// Replaces ChromosomeMover and AnaphaseChromosomeMover.
/// </summary>
public class ChromosomeMover : MonoBehaviour, IPhaseController
{
    [Header("Metaphase Movement")]
    public Transform metaphaseEndLocation;
    public GameObject chromatidLeft;
    public GameObject chromatidRight;
    public GameObject rightLineRenderer;

    [Header("Anaphase Movement")]
    public Transform anaphaseEndLocation;

    private float timer    = 0f;
    private bool isDone    = false;
    private bool isActive  = false;

    private GameManager.GameState currentPhase;

    private const float startDelay      = 2f;
    private const float metaphaseDuration = 7f;
    private const float anaphaseDuration  = 10f;

    // ── IPhaseController ──────────────────────────────────────────────────────

    public void OnPhaseEnter(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Metaphase ||
            phase == GameManager.GameState.Anaphase)
        {
            currentPhase = phase;
            isActive     = true;
            isDone       = false;
            timer        = 0f;

            if (phase == GameManager.GameState.Metaphase)
            {
                var line = GetComponent<LineRenderer>();
                if (line != null) line.enabled = true;
                if (rightLineRenderer != null) rightLineRenderer.SetActive(true);
            }
        }
    }

    public void OnPhaseExit(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Metaphase ||
            phase == GameManager.GameState.Anaphase)
            isActive = false;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!isActive || isDone) return;

        timer += Time.deltaTime;
        if (timer <= startDelay) return;

        if (currentPhase == GameManager.GameState.Metaphase)
            MoveMetaphase();
        else if (currentPhase == GameManager.GameState.Anaphase)
            MoveAnaphase();
    }

    private void MoveMetaphase()
    {
        if (metaphaseEndLocation == null) return;

        transform.position = Vector3.Lerp(transform.position, metaphaseEndLocation.position, 0.01f);

        if (timer >= metaphaseDuration)
        {
            if (chromatidLeft  != null) chromatidLeft.SetActive(true);
            if (chromatidRight != null) chromatidRight.SetActive(true);
            gameObject.SetActive(false);
            isDone = true;
            Debug.Log("[ChromosomeMover] Metaphase movement complete.");
        }
    }

    private void MoveAnaphase()
    {
        if (anaphaseEndLocation == null) return;

        transform.position = Vector3.Lerp(transform.position, anaphaseEndLocation.position, 0.01f);

        if (timer >= anaphaseDuration)
        {
            timer  = 0f;
            isDone = true;
            Debug.Log("[ChromosomeMover] Anaphase movement complete.");
        }
    }
}