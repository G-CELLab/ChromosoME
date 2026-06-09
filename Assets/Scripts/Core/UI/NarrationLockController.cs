using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Controls the narration lock state — darkens the scene via post-processing,
/// disables hand interaction and colliders, dims world space UI, and pauses
/// the active ghost hand hint.
/// Called by AITutor at the start and end of narration.
/// </summary>
public class NarrationLockController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Volume      globalVolume;
    [SerializeField] private HandManager leftHand;
    [SerializeField] private HandManager rightHand;

    [Header("Hand Colliders")]
    [SerializeField] private GameObject leftHandRoot;
    [SerializeField] private GameObject rightHandRoot;

    [Header("Darkening Settings")]
    [SerializeField] private float targetExposure = -1.5f;
    [SerializeField] private float targetVignette = 0.45f;
    [SerializeField] private float fadeDuration   = 0.8f;

    [Header("World Space UI")]
    [Tooltip("Add a CanvasGroup component to each World Space Canvas, then assign them here.")]
    [SerializeField] private CanvasGroup[] worldSpaceUI;
    [SerializeField] private float uiDimmedAlpha = 0.25f;

    // ── Post Processing ───────────────────────────────────────────────────────

    private ColorAdjustments _colorAdjustments;
    private Vignette         _vignette;
    private Coroutine        _fadeCoroutine;

    // ── Hand Colliders ────────────────────────────────────────────────────────

    private Collider[] _leftColliders;
    private Collider[] _rightColliders;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (globalVolume == null)
            globalVolume = FindAnyObjectByType<Volume>();

        if (globalVolume != null && globalVolume.profile != null)
        {
            globalVolume.profile.TryGet(out _colorAdjustments);
            globalVolume.profile.TryGet(out _vignette);
        }

        if (_colorAdjustments == null)
            Debug.LogWarning("[NarrationLockController] ColorAdjustments override not found on Volume Profile.");
        if (_vignette == null)
            Debug.LogWarning("[NarrationLockController] Vignette override not found on Volume Profile.");

        if (leftHandRoot != null)
            _leftColliders = leftHandRoot.GetComponentsInChildren<Collider>();
        if (rightHandRoot != null)
            _rightColliders = rightHandRoot.GetComponentsInChildren<Collider>();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Lock()
    {
        IsLocked = true;
        DisableHands();
        SetHandCollidersEnabled(false);
        PauseActiveHint();

        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeAll(targetExposure, targetVignette, uiDimmedAlpha));

        Debug.Log("[NarrationLockController] 🔒 Locked.");
    }

    public void Unlock()
    {
        IsLocked = false;
        EnableHands();
        SetHandCollidersEnabled(true);
        ResumeActiveHint();

        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeAll(0f, 0f, 1f));

        Debug.Log("[NarrationLockController] 🔓 Unlocked.");
    }

    public bool IsLocked { get; private set; } = false;

    // ── Hands ─────────────────────────────────────────────────────────────────

    private void DisableHands()
    {
        leftHand?.SetInteractionEnabled(false);
        rightHand?.SetInteractionEnabled(false);
    }

    private void EnableHands()
    {
        leftHand?.SetInteractionEnabled(true);
        rightHand?.SetInteractionEnabled(true);
    }

    private void SetHandCollidersEnabled(bool enabled)
    {
        if (_leftColliders != null)
            foreach (var c in _leftColliders) c.enabled = enabled;
        if (_rightColliders != null)
            foreach (var c in _rightColliders) c.enabled = enabled;
    }

    // ── Ghost Hand Hint ───────────────────────────────────────────────────────

    private void PauseActiveHint()
    {
        var hint = FindAnyObjectByType<GhostHandHint>();
        hint?.PauseHint();
    }

    private void ResumeActiveHint()
    {
        var hint = FindAnyObjectByType<GhostHandHint>();
        hint?.ResumeHint();
    }

    // ── Fade ──────────────────────────────────────────────────────────────────

    private IEnumerator FadeAll(float toExposure, float toVignette, float toUIAlpha)
    {
        float fromExposure = _colorAdjustments != null
            ? _colorAdjustments.postExposure.value : 0f;
        float fromVignette = _vignette != null
            ? _vignette.intensity.value : 0f;

        float[] fromAlphas = new float[worldSpaceUI != null ? worldSpaceUI.Length : 0];
        if (worldSpaceUI != null)
            for (int i = 0; i < worldSpaceUI.Length; i++)
                fromAlphas[i] = worldSpaceUI[i] != null ? worldSpaceUI[i].alpha : 1f;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t  = Mathf.Clamp01(elapsed / fadeDuration);

            if (_colorAdjustments != null)
                _colorAdjustments.postExposure.value = Mathf.Lerp(fromExposure, toExposure, t);
            if (_vignette != null)
                _vignette.intensity.value = Mathf.Lerp(fromVignette, toVignette, t);

            if (worldSpaceUI != null)
                for (int i = 0; i < worldSpaceUI.Length; i++)
                    if (worldSpaceUI[i] != null)
                        worldSpaceUI[i].alpha = Mathf.Lerp(fromAlphas[i], toUIAlpha, t);

            yield return null;
        }

        if (_colorAdjustments != null)
            _colorAdjustments.postExposure.value = toExposure;
        if (_vignette != null)
            _vignette.intensity.value = toVignette;

        if (worldSpaceUI != null)
            for (int i = 0; i < worldSpaceUI.Length; i++)
                if (worldSpaceUI[i] != null)
                    worldSpaceUI[i].alpha = toUIAlpha;
    }
}