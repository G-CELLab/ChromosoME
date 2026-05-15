using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public enum GameState
    {
        Intro,
        Interphase,
        InterphasePart2,
        Prophase,
        Metaphase,
        Anaphase,
        Telophase,
        GameOver
    }

    public static GameState eGameStatus;
    public static GameManager instance;

    // ── Unity Events ──────────────────────────────────────────────────────────
    [Header("Phase Events")]
    public UnityEvent onIntro;
    public UnityEvent onInterphase;
    public UnityEvent onInterphasePart2;
    public UnityEvent onProphase;
    public UnityEvent onMetaphase;
    public UnityEvent onAnaphase;
    public UnityEvent onTelophase;
    public UnityEvent onGameOver;

    // ── Phase Controllers (auto-registered) ───────────────────────────────────
    private readonly List<IPhaseController> phaseControllers = new List<IPhaseController>();

    public static void Register(IPhaseController controller)
    {
        if (instance != null && !instance.phaseControllers.Contains(controller))
        {
            instance.phaseControllers.Add(controller);
            controller.OnPhaseEnter(eGameStatus);
        }
    }

    public static void Unregister(IPhaseController controller)
    {
        instance?.phaseControllers.Remove(controller);
    }

    // ── Scene References ──────────────────────────────────────────────────────
    [Header("Scene References")]
    [SerializeField] private AITutor aiTutor;
    public TextToSpeechPlayer ttsPlayer;

    [Header("Reset Manager")]
    [SerializeField] private ResetManager resetManager;

    [Header("Slider Components")]
    public Image atpSliderImg;
    public Image hpSliderImg;

    // ── Healing Cycle Tracking ────────────────────────────────────────────────
    [Header("Healing Cycle Tracking")]
    public static int healingCycleCount = 0;
    public const int MAX_HEALING_CYCLES = 3;

    public static void IncrementHealingCycle()
    {
        healingCycleCount++;
        Debug.Log($"[GameManager] Healing cycle: {healingCycleCount}/{MAX_HEALING_CYCLES}");
    }

    public static bool   IsWoundHealed()       => healingCycleCount >= MAX_HEALING_CYCLES;
    public static int    GetHealingCycleCount() => healingCycleCount;

    public static void ResetHealingCycles()
    {
        healingCycleCount = 0;
        Debug.Log("[GameManager] Healing cycles reset.");
    }

    public static string GetHealingStatusMessage()
    {
        return IsWoundHealed()
            ? "Wound fully healed! Touch the healed hand to complete the simulation."
            : $"Healing Progress: {healingCycleCount}/{MAX_HEALING_CYCLES} cycles completed, " +
              $"{MAX_HEALING_CYCLES - healingCycleCount} more needed.";
    }

    // ── Gameplay ──────────────────────────────────────────────────────────────

    public void FoodCollision()
    {
        try { LogEventHelper.LogATPCharged(); }
        catch (System.Exception ex) { Debug.LogWarning($"[GameManager] LogEventHelper failed: {ex.Message}"); }

        if (atpSliderImg != null)
            atpSliderImg.fillAmount = Mathf.Clamp01(atpSliderImg.fillAmount + 0.3f);

        Debug.Log("[GameManager] ATP charged.");
    }

    public void CellDivided()
    {
        if (hpSliderImg != null)
            hpSliderImg.fillAmount = Mathf.Clamp01(hpSliderImg.fillAmount + 0.3f);

        Debug.Log("[GameManager] HP charged.");
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        Debug.Log($"[GameManager] Starting — Cycle: {healingCycleCount}/{MAX_HEALING_CYCLES}");
        StartCoroutine(DelayedStart());
    }

    private IEnumerator DelayedStart()
    {
        yield return null;
        yield return null;
        TransitionTo(healingCycleCount == 0 ? GameState.Intro : GameState.Interphase);
    }

    // ── Phase Transition Entry Points ─────────────────────────────────────────

    public void Intro()           => TransitionTo(GameState.Intro);
    public void Interphase()      => TransitionTo(GameState.Interphase);
    public void InterphasePart2() => TransitionTo(GameState.InterphasePart2);
    public void Prophase()        => TransitionTo(GameState.Prophase);
    public void Metaphase()       => TransitionTo(GameState.Metaphase);
    public void Anaphase()        => TransitionTo(GameState.Anaphase);
    public void GameEnd()         => TransitionTo(GameState.GameOver);

    public void Telophase()
    {
        TransitionTo(GameState.Telophase);
        StartCoroutine(DelayedTelophaseAITrigger());
    }

    // ── Next Cycle ────────────────────────────────────────────────────────────

    public void ResetForNextCycle()
    {
        resetManager?.ResetAll();
        Debug.Log("[GameManager] Scene reset for next cycle.");
    }

    // ── Core Transition Logic ─────────────────────────────────────────────────

    private void TransitionTo(GameState newState)
    {
        GameState previous = eGameStatus;
        eGameStatus = newState;

        NotifyControllersExit(previous);
        NotifyControllersEnter(newState);

        switch (newState)
        {
            case GameState.Intro:           onIntro.Invoke();           break;
            case GameState.Interphase:      onInterphase.Invoke();      break;
            case GameState.InterphasePart2: onInterphasePart2.Invoke(); break;
            case GameState.Prophase:        onProphase.Invoke();        break;
            case GameState.Metaphase:       onMetaphase.Invoke();       break;
            case GameState.Anaphase:        onAnaphase.Invoke();        break;
            case GameState.Telophase:       onTelophase.Invoke();       break;
            case GameState.GameOver:        onGameOver.Invoke();        break;
        }

        aiTutor?.RefreshSceneState();
        Debug.Log($"[GameManager] {previous} → {newState}");
    }

    private void NotifyControllersEnter(GameState phase)
    {
        foreach (var c in new List<IPhaseController>(phaseControllers))
            c.OnPhaseEnter(phase);
    }

    private void NotifyControllersExit(GameState phase)
    {
        foreach (var c in new List<IPhaseController>(phaseControllers))
            c.OnPhaseExit(phase);
    }

    // ── AI Tutor ──────────────────────────────────────────────────────────────

    private IEnumerator DelayedTelophaseAITrigger()
    {
        yield return new WaitForSeconds(0.5f);

        if (ttsPlayer == null || aiTutor == null)
        {
            Debug.LogWarning("[GameManager] TTS or AITutor not assigned.");
            yield break;
        }

        string message = aiTutor.GetSceneState().GetTelophaseMessage();
        if (!string.IsNullOrEmpty(message))
            ttsPlayer.Speak(message, () => Debug.Log("[GameManager] Telophase speech done."));
    }
}