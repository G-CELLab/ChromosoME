using UnityEngine;

/// <summary>
/// Moves a cell half to its end position and shrinks it during Telophase,
/// simulating cell division. Replaces CellDivide_L and CellDivide_R.
/// The Left side also notifies GameManager when division is complete.
/// </summary>
public class CellDivider : MonoBehaviour, IPhaseController
{
    public enum Side { Left, Right }

    [Header("Configuration")]
    public Side side;

    [Header("References")]
    public GameManager gameManager;
    public Transform endLocation;

    private float timer = 0f;
    private bool isDone = false;
    private Vector3 scaleChange;
    private bool isActive = false;

    private const float startDelay = 3f;
    private const float duration   = 10f;
    private const float minScale   = 0.05f;

    private void Start()
    {
        scaleChange = new Vector3(-0.0001f, -0.0001f, -0.0001f);
    }

    // ── IPhaseController ──────────────────────────────────────────────────────

    public void OnPhaseEnter(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Telophase)
            isActive = true;
    }

    public void OnPhaseExit(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Telophase)
            isActive = false;
    }

    // ── Update ────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!isActive || isDone) return;

        timer += Time.deltaTime;
        if (timer <= startDelay) return;

        transform.position = Vector3.Lerp(transform.position, endLocation.position, 0.01f);
        transform.localScale += scaleChange;

        if (transform.localScale.y < minScale)
            scaleChange = Vector3.zero;

        if (timer >= duration)
        {
            timer  = 0f;
            isDone = true;

            // Only the left cell notifies GameManager to avoid double calls
            if (side == Side.Left && gameManager != null)
                gameManager.CellDivided();

            Debug.Log($"[CellDivider] {side} division complete.");
        }
    }
}