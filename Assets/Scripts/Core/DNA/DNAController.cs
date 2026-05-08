using UnityEngine;

/// <summary>
/// Controls the AI (blue) DNA condensation animation during Prophase.
/// Automatically triggers after a short delay when Prophase begins.
/// </summary>
public class DNAAIController : MonoBehaviour, IPhaseController
{
    private Animator animator;
    private float timer  = 0f;
    private bool isDone  = false;
    private bool isActive = false;

    private const float condenseDelay = 3.5f;

    private void Start()
    {
        animator = GetComponent<Animator>();
    }

    // ── IPhaseController ──────────────────────────────────────────────────────

    public void OnPhaseEnter(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Prophase)
        {
            isActive = true;
            isDone   = false;
            timer    = 0f;
        }
    }

    public void OnPhaseExit(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Prophase)
            isActive = false;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!isActive || isDone) return;

        timer += Time.deltaTime;
        if (timer >= condenseDelay)
        {
            isDone = true;
            if (animator != null)
            {
                animator.SetBool("isOpened", true);
                animator.SetBool("isIdle",   false);
            }
            Debug.Log("[DNAAIController] AI DNA condensation triggered.");
        }
    }
}