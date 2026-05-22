using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Updates the Phase Indicator UI based on the current game phase.
/// </summary>
public class PhaseUIController : MonoBehaviour, IPhaseController
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI phaseText;
    [SerializeField] private Image background;

    [Header("Colors")]
    [SerializeField] private Color introColor = Color.gray;
    [SerializeField] private Color interphaseColor = new Color(0.2f, 0.6f, 1f); // Blue
    [SerializeField] private Color prophaseColor = new Color(1f, 0.8f, 0.2f);    // Yellow
    [SerializeField] private Color metaphaseColor = new Color(0.8f, 0.2f, 1f);   // Purple
    [SerializeField] private Color anaphaseColor = new Color(0.2f, 1f, 0.4f);    // Green
    [SerializeField] private Color telophaseColor = new Color(1f, 0.4f, 0.4f);   // Red
    [SerializeField] private Color defaultColor = Color.white;

    private void OnEnable() => GameManager.Register(this);
    private void OnDisable() => GameManager.Unregister(this);

    public void OnPhaseEnter(GameManager.GameState phase)
    {
        UpdateUI(phase);
    }

    public void OnPhaseExit(GameManager.GameState phase) { }

    private void UpdateUI(GameManager.GameState phase)
    {
        if (phaseText != null)
        {
            if (phase == GameManager.GameState.InterphasePart2)
            {
                phaseText.text = "Interphase";
            } else
            {
                phaseText.text = phase.ToString();
            }
        }

        if (background != null)
        {
            background.color = GetPhaseColor(phase);
        }
    }

    private Color GetPhaseColor(GameManager.GameState phase)
    {
        switch (phase)
        {
            case GameManager.GameState.Intro:           return introColor;
            case GameManager.GameState.Interphase:      return interphaseColor;
            case GameManager.GameState.InterphasePart2: return interphaseColor;
            case GameManager.GameState.Prophase:        return prophaseColor;
            case GameManager.GameState.Metaphase:       return metaphaseColor;
            case GameManager.GameState.Anaphase:        return anaphaseColor;
            case GameManager.GameState.Telophase:       return telophaseColor;
            case GameManager.GameState.GameOver:        return Color.black;
            default:                                    return defaultColor;
        }
    }
}
