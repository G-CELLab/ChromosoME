using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles hand-over-wound detection for both the intro wound healing
/// and the end-of-cycle restart. Uses a HashSet to handle multiple
/// hand colliders independently.
/// </summary>
public class WoundInteractionHandler : MonoBehaviour
{
    [Header("References")]
    public GameManager gameManager;
    public FadeScreen fadeScreen;

    [Header("Loading Circle")]
    public LoadingCircle loadingCircle;

    [Header("HP Bar")]
    public Image hpBar;

    private bool isFinished = false;
    private HashSet<Collider> activeColliders = new HashSet<Collider>();

    private void Start()
    {
        if (hpBar != null)
            hpBar.fillAmount = ScoreManager.HPtracking;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Left") && !other.CompareTag("Right")) return;
        if (activeColliders.Add(other))
            Debug.Log($"[WoundInteractionHandler] Collider added: {other.name}. Total: {activeColliders.Count}");
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Left") && !other.CompareTag("Right")) return;
        if (activeColliders.Remove(other))
            Debug.Log($"[WoundInteractionHandler] Collider removed: {other.name}. Remaining: {activeColliders.Count}");
        if (activeColliders.Count == 0 && !isFinished)
        {
            loadingCircle.Reset();
            Debug.Log("[WoundInteractionHandler] Timer reset — no hand in trigger.");
        }
    }

    private void Update()
    {
        if (isFinished || activeColliders.Count == 0) return;
        if (loadingCircle.Tick(Time.deltaTime))
            CompleteInteraction();
    }

    private void CompleteInteraction()
    {
        isFinished = true;
        loadingCircle.Reset();

        // End of cycle (Telophase) — increment healing and continue
        if (GameManager.eGameStatus == GameManager.GameState.Telophase)
        {
            if (GameManager.IsWoundHealed())
            {
                Debug.Log("[WoundInteractionHandler] Wound fully healed — ending game.");
                gameManager?.GameEnd();
                return;
            }

            GameManager.IncrementHealingCycle();
            ScoreManager.HPtracking = 0.3f + (GameManager.GetHealingCycleCount() * 0.3f);
            Debug.Log($"[WoundInteractionHandler] Cycle {GameManager.GetHealingCycleCount()}/{GameManager.MAX_HEALING_CYCLES} complete. HP: {ScoreManager.HPtracking}");
        }

        // Both intro and end-of-cycle transition to Interphase
        Debug.Log("[WoundInteractionHandler] Transitioning to Interphase.");
        StartCoroutine(FadeAndTransition());
    }

    private IEnumerator FadeAndTransition()
    {
        if (fadeScreen != null)
            yield return StartCoroutine(fadeScreen.FadeToBlack());

        gameManager?.ResetForNextCycle();
        if (gameManager != null) gameManager.Interphase();

        if (fadeScreen != null) fadeScreen.StartFadeToClear();

        this.enabled = false;
    }
}