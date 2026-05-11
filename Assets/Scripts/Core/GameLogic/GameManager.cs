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

    // ── Unity Events (inspector-wired scene object toggling) ──────────────────
    [Header("Phase Events")]
    public UnityEvent onIntro;
    public UnityEvent onInterphase;
    public UnityEvent onInterphasePart2;
    public UnityEvent onProphase;
    public UnityEvent onMetaphase;
    public UnityEvent onAnaphase;
    public UnityEvent onTelophase;
    public UnityEvent onGameOver;

    // ── Phase Controllers ─────────────────────────────────────────────────────
    [Header("Phase Controllers")]
    [Tooltip("Drag any MonoBehaviour that implements IPhaseController here.")]
    [SerializeField] private List<MonoBehaviour> phaseControllers;

    // ── Scene References ──────────────────────────────────────────────────────
    [Header("Scene References")]
    public TextToSpeechPlayer ttsPlayer;

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

    public static bool IsWoundHealed()         => healingCycleCount >= MAX_HEALING_CYCLES;
    public static int  GetHealingCycleCount()  => healingCycleCount;

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
        LogEventHelper.LogATPCharged();
        atpSliderImg.fillAmount += 0.3f;
        Debug.Log("[GameManager] ATP charged.");
    }

    public void CellDivided()
    {
        hpSliderImg.fillAmount += 0.3f;
        Debug.Log("[GameManager] HP charged.");
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Start()
    {
        Debug.Log($"[GameManager] Starting — Cycle: {healingCycleCount}/{MAX_HEALING_CYCLES}");

        if (healingCycleCount == 0)
            TransitionTo(GameState.Intro);
        else
            TransitionTo(GameState.Interphase);
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
        CleanupPreviousPhases();
        StartCoroutine(DelayedTelophaseAITrigger());
    }

    // ── Core Transition Logic ─────────────────────────────────────────────────

    private void TransitionTo(GameState newState)
    {
        GameState previousState = eGameStatus;
        eGameStatus = newState;

        NotifyControllersExit(previousState);
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

        NotifyAITutor();
        Debug.Log($"[GameManager] {previousState} → {newState}");
    }

    private void NotifyControllersEnter(GameState phase)
    {
        foreach (var mb in phaseControllers)
            if (mb is IPhaseController controller)
                controller.OnPhaseEnter(phase);
    }

    private void NotifyControllersExit(GameState phase)
    {
        foreach (var mb in phaseControllers)
            if (mb is IPhaseController controller)
                controller.OnPhaseExit(phase);
    }

    // ── AI Tutor ──────────────────────────────────────────────────────────────

    private static void NotifyAITutor()
    {
        var tutor = Object.FindAnyObjectByType<AITutor>();
        if (tutor != null)
            tutor.RefreshSceneState();
        else
            Debug.LogWarning("[GameManager] AITutor not found — scene state not refreshed.");
    }

    private IEnumerator DelayedTelophaseAITrigger()
    {
        yield return new WaitForSeconds(0.5f);

        if (ttsPlayer == null)
        {
            Debug.LogWarning("[GameManager] TextToSpeechPlayer not assigned.");
            yield break;
        }

        var tutor = Object.FindAnyObjectByType<AITutor>();
        if (tutor == null)
        {
            Debug.LogWarning("[GameManager] AITutor not found — cannot speak Telophase message.");
            yield break;
        }

        string message = tutor.GetSceneState().GetTelophaseMessage();
        if (!string.IsNullOrEmpty(message))
            ttsPlayer.Speak(message, () => Debug.Log("[GameManager] Telophase speech done."));
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    private void CleanupPreviousPhases()
    {
        string[] tagsToDisable = { "Centriole1", "Centriole2", "Meta", "Pro", "Finish" };
        foreach (string tag in tagsToDisable)
        {
            try
            {
                foreach (var obj in GameObject.FindGameObjectsWithTag(tag))
                    obj.SetActive(false);
            }
            catch (UnityException)
            {
                Debug.Log($"[GameManager] Tag '{tag}' not defined — skipping.");
            }
        }

        foreach (var obj in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (obj.name == "Player_DNA" || obj.name == "P_Chromotid_L" || obj.name == "P_Chromotid_R")
                if (obj.scene.name != null)
                    obj.SetActive(false);
        }
    }
}