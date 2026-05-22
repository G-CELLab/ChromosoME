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

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        loadingCircle.Initialize();
    }

    private void OnEnable()
    {
        isFinished = false;
        activeColliders.Clear();
        loadingCircle?.Reset();
        if (hpBar != null)
            hpBar.fillAmount = ScoreManager.HPtracking;
    }

    // ── Trigger Detection ─────────────────────────────────────────────────────
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

    // ── Update ────────────────────────────────────────────────────────────────
    private void Update()
    {
        if (isFinished || activeColliders.Count == 0) return;
        if (loadingCircle.Tick(Time.deltaTime))
            CompleteInteraction();
    }

    // ── Completion ────────────────────────────────────────────────────────────
    private void CompleteInteraction()
    {
        isFinished = true;
        loadingCircle.Reset();

        if (GameManager.eGameStatus == GameManager.GameState.Telophase)
        {
            GameManager.IncrementHealingCycle(); // increment first

            if (GameManager.IsWoundHealed()) // then check
            {
                Debug.Log("[WoundInteractionHandler] Wound fully healed — ending game.");
                gameManager?.GameEnd();
                return;
            }

            
            Debug.Log($"[WoundInteractionHandler] Cycle {GameManager.GetHealingCycleCount()}/{GameManager.MAX_HEALING_CYCLES} complete. HP: {ScoreManager.HPtracking}");
        }

        Debug.Log("[WoundInteractionHandler] Transitioning to Interphase.");
        StartCoroutine(FadeAndTransition());
    }

    // ── Coroutines ────────────────────────────────────────────────────────────
    private IEnumerator FadeAndTransition()
    {
        if (fadeScreen != null)
            yield return StartCoroutine(fadeScreen.FadeToBlack());

        gameManager?.ResetForNextCycle();
        if (gameManager != null) gameManager.Interphase();

        if (fadeScreen != null) fadeScreen.StartFadeToClear();
    }
}