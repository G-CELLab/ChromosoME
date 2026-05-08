using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the HP bar display and tracks healing progress across cycles.
/// </summary>
public class ScoreManager : MonoBehaviour, IPhaseController
{
    public static float HPtracking = 0.3f;

    [Header("References")]
    public Image hpBar;
    public GameObject hpText;

    private void Start()
    {
        if (hpBar == null)
        {
            Debug.LogError("[ScoreManager] hpBar Image is not assigned in the Inspector!");
            return;
        }
        
        hpBar.fillAmount = HPtracking;
        Debug.Log($"[ScoreManager] Scene started — HP: {HPtracking}, Cycle: {GameManager.GetHealingCycleCount()}/{GameManager.MAX_HEALING_CYCLES}");
    }

    // ── IPhaseController ──────────────────────────────────────────────────────

    public void OnPhaseEnter(GameManager.GameState phase)
    {
        switch (phase)
        {
            case GameManager.GameState.Intro:
                GetComponent<Canvas>().enabled     = false;
                if (hpBar != null) hpBar.GetComponent<Image>().enabled = false;
                if (hpText != null) hpText.SetActive(false);
                break;

            case GameManager.GameState.Interphase:
                GetComponent<Canvas>().enabled     = true;
                if (hpBar != null) hpBar.GetComponent<Image>().enabled = true;
                if (hpText != null) hpText.SetActive(true);
                if (hpBar != null) hpBar.fillAmount = HPtracking;
                break;
        }
    }

    public void OnPhaseExit(GameManager.GameState phase) { }
}