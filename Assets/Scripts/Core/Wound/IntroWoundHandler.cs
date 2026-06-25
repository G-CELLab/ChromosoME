using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class IntroWoundHandler : MonoBehaviour, IWoundHandler
{
    [Header("References")]
    public GameManager gameManager;
    public FadeScreen fadeScreen;
    public FadeScreen agentFadeScreen;
    public NarrationLockController narrationLock;
    [Header("Loading Circle")]
    public LoadingCircle loadingCircle;
    [Header("HP Bar")]
    public Image hpBar;

    private bool isFinished = false;
    private HashSet<Collider> activeColliders = new HashSet<Collider>();

    private void Awake() => loadingCircle.Initialize();

    private void OnEnable()
    {
        isFinished = false;
        activeColliders.Clear();
        loadingCircle?.Reset();
        if (hpBar != null)
            hpBar.fillAmount = GameManager.HPtracking;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Left") && !other.CompareTag("Right")) return;
        if (narrationLock != null && narrationLock.IsLocked) return;
        if (activeColliders.Add(other))
            Debug.Log($"[IntroWoundHandler] Collider added: {other.name}. Total: {activeColliders.Count}");
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Left") && !other.CompareTag("Right")) return;
        if (activeColliders.Remove(other))
            Debug.Log($"[IntroWoundHandler] Collider removed: {other.name}. Remaining: {activeColliders.Count}");
        if (activeColliders.Count == 0 && !isFinished)
        {
            loadingCircle.Reset();
            Debug.Log("[IntroWoundHandler] Timer reset — no hand in trigger.");
        }
    }

    public void OnNarrationLock()
    {
        activeColliders.Clear();
        loadingCircle.Reset();
        Debug.Log("[IntroWoundHandler] Cleared on narration lock.");
    }

    private void Update()
    {
        if (isFinished || activeColliders.Count == 0) return;
        if (narrationLock != null && narrationLock.IsLocked) return;
        if (loadingCircle.Tick(Time.deltaTime))
            CompleteInteraction();
    }

    private void CompleteInteraction()
    {
        isFinished = true;
        loadingCircle.Reset();
        if (GameManager.eGameStatus == GameManager.GameState.GameOver) return;
        StartCoroutine(FadeAndTransition());
    }

    private IEnumerator FadeAndTransition()
    {
        fadeScreen.gameObject.SetActive(true);
        agentFadeScreen.gameObject.SetActive(true);
        StartCoroutine(agentFadeScreen.FadeToBlack());
        yield return StartCoroutine(fadeScreen.FadeToBlack());
        gameManager?.ResetForNextCycle();
        gameManager?.Interphase();
        fadeScreen.StartFadeToClear();
        agentFadeScreen.StartFadeToClear();
    }
}