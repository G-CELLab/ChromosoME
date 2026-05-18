using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Draws spindle fiber lines from this centriole to multiple target chromosomes.
/// Each target gets its own LineRenderer, created automatically.
/// Active during Metaphase, Anaphase, and Telophase.
/// Switches to anaphase targets when Anaphase begins.
/// </summary>
public class SpindleFiberController : MonoBehaviour, IPhaseController
{
    [Header("References")]
    public List<GameObject> metaphaseTargets = new List<GameObject>();
    public List<GameObject> anaphaseTargets  = new List<GameObject>();

    [Header("Settings")]
    public Vector3 positionOffset = Vector3.zero;

    [Header("Fiber Appearance")]
    public Color    fiberColor    = new Color(0.7f, 1f, 0f, 1f);
    public float    fiberWidth    = 0.015f;
    public Material fiberMaterial;

    private List<LineRenderer> lineRenderers = new List<LineRenderer>();
    private List<GameObject>   activeTargets = new List<GameObject>();
    private bool isActive = false;

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        CreateFibers();
    }

    private void OnEnable()  => GameManager.Register(this);
    private void OnDisable() => GameManager.Unregister(this);

    // ── Fiber Creation ────────────────────────────────────────────────────────
    private void CreateFibers()
    {
        lineRenderers.Clear();

        int maxTargets = Mathf.Max(metaphaseTargets.Count, anaphaseTargets.Count);
        for (int i = 0; i < maxTargets; i++)
        {
            GameObject fiberObj = new GameObject($"_SpindleFiber_{i}");
            fiberObj.transform.SetParent(transform, false);
            LineRenderer lr = fiberObj.AddComponent<LineRenderer>();
            lr.positionCount  = 2;
            lr.startWidth     = fiberWidth;
            lr.endWidth       = fiberWidth;
            lr.material       = fiberMaterial != null
                                ? fiberMaterial
                                : new Material(Shader.Find("Sprites/Default"));
            lr.material.color = fiberColor;
            lr.useWorldSpace  = true;
            lr.enabled        = false;
            lineRenderers.Add(lr);
        }
    }

    // ── IPhaseController ──────────────────────────────────────────────────────
    public void OnPhaseEnter(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Metaphase)
        {
            // Recreate fibers if ResetManager destroyed them
            if (lineRenderers.Count == 0 || lineRenderers[0] == null)
                CreateFibers();

            activeTargets = metaphaseTargets;
            isActive      = true;
            SetFibersEnabled(true);
        }
        else if (phase == GameManager.GameState.Anaphase)
        {
            activeTargets = anaphaseTargets;
            isActive      = true;
            SetFibersEnabled(true);
        }
        else if (phase == GameManager.GameState.Telophase)
        {
            isActive = true;
            SetFibersEnabled(true);
        }
    }

    public void OnPhaseExit(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Metaphase ||
            phase == GameManager.GameState.Anaphase  ||
            phase == GameManager.GameState.Telophase)
        {
            isActive = false;
            SetFibersEnabled(false);
        }
    }

    // ── Update ────────────────────────────────────────────────────────────────
    private void Update()
    {
        if (!isActive) return;
        Vector3 startPos = transform.position + positionOffset;
        for (int i = 0; i < lineRenderers.Count; i++)
        {
            if (lineRenderers[i] == null) continue;
            if (i >= activeTargets.Count || activeTargets[i] == null)
            {
                lineRenderers[i].enabled = false;
                continue;
            }
            lineRenderers[i].enabled = true;
            lineRenderers[i].SetPosition(0, startPos);
            lineRenderers[i].SetPosition(1, activeTargets[i].transform.position);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private void SetFibersEnabled(bool enabled)
    {
        foreach (var lr in lineRenderers)
            if (lr != null) lr.enabled = enabled;
    }
}