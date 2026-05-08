using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Handles end-of-cycle logic when the player touches the wound after cell division.
/// Increments the healing cycle, updates HP, and either reloads the scene
/// for another cycle or ends the game if all cycles are complete.
/// </summary>
public class GameRestarter : MonoBehaviour
{
    [Header("References")]
    public Image hpBar;
    public GameManager gameManager;

    [Header("Loading Circle")]
    public LoadingCircle loadingCircle;

    private bool handDetected = false;

    private void Start()
    {
        if (hpBar != null)
            hpBar.fillAmount = ScoreManager.HPtracking;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Left") || other.CompareTag("Right"))
            handDetected = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Left") || other.CompareTag("Right"))
        {
            handDetected = false;
            loadingCircle.Reset();
        }
    }

    private void Update()
    {
        if (!handDetected) return;
        if (loadingCircle.Tick(Time.deltaTime))
            CompleteInteraction();
    }

    private void CompleteInteraction()
    {
        handDetected = false;
        loadingCircle.Reset();

        if (GameManager.eGameStatus == GameManager.GameState.GameOver)
        {
            Debug.Log("[GameRestarter] Game over — quitting.");
            Application.Quit();
            return;
        }

        GameManager.IncrementHealingCycle();
        ScoreManager.HPtracking = 0.3f + (GameManager.GetHealingCycleCount() * 0.3f);

        Debug.Log($"[GameRestarter] Cycle {GameManager.GetHealingCycleCount()}/{GameManager.MAX_HEALING_CYCLES} complete. HP: {ScoreManager.HPtracking}");

        if (GameManager.IsWoundHealed())
        {
            Debug.Log("[GameRestarter] Wound fully healed — ending game.");
            if (gameManager != null) gameManager.GameEnd();
        }
        else
        {
            Debug.Log("[GameRestarter] Reloading scene for next cycle.");
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}