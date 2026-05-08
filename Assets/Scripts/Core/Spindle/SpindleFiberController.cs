using UnityEngine;

/// <summary>
/// Draws a spindle fiber line from this centriole to a target chromosome.
/// Active during Metaphase, Anaphase, and Telophase.
/// Replaces SpindleFiber and SpindleFiber_2 — the only difference was a
/// small Y offset on the start position, configurable here via positionOffset.
/// </summary>
public class SpindleFiberController : MonoBehaviour, IPhaseController
{
    [Header("References")]
    [SerializeField] private GameObject target;

    [Header("Settings")]
    public Vector3 positionOffset = Vector3.zero;

    private LineRenderer lineRenderer;
    private bool isActive = false;

    private static readonly Color fiberColor = new Color(0.7f, 1f, 0f, 1f);
    private const float fiberWidth = 0.015f;

    private void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer != null) lineRenderer.enabled = false;
    }

    // ── IPhaseController ──────────────────────────────────────────────────────

    public void OnPhaseEnter(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Metaphase ||
            phase == GameManager.GameState.Anaphase  ||
            phase == GameManager.GameState.Telophase)
        {
            isActive = true;
            if (lineRenderer != null) lineRenderer.enabled = true;
        }
    }

    public void OnPhaseExit(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Metaphase ||
            phase == GameManager.GameState.Anaphase  ||
            phase == GameManager.GameState.Telophase)
        {
            isActive = false;
            if (lineRenderer != null) lineRenderer.enabled = false;
        }
    }

    // ── Update ────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!isActive || lineRenderer == null || target == null) return;

        lineRenderer.material.color = fiberColor;
        lineRenderer.startWidth     = fiberWidth;
        lineRenderer.endWidth       = fiberWidth;
        lineRenderer.SetPosition(0, transform.position + positionOffset);
        lineRenderer.SetPosition(1, target.transform.position);
    }
}