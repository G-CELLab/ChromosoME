using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class NarrationLockController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Volume globalVolume;
    [SerializeField] private HandManager leftHand;
    [SerializeField] private HandManager rightHand;

    [Header("Hand Colliders")]
    [SerializeField] private GameObject leftHandRoot;
    [SerializeField] private GameObject rightHandRoot;

    [Header("Darkening Settings")]
    [SerializeField] private float targetExposure = -1.5f;
    [SerializeField] private float targetVignette = 0.45f;
    [SerializeField] private float fadeDuration = 0.8f;

    [Header("World Space UI")]
    [SerializeField] private CanvasGroup[] worldSpaceUI;
    [SerializeField] private float uiDimmedAlpha = 0.25f;

    [Header("Wound Handlers")]
    [SerializeField] private MonoBehaviour[] woundHandlers;

    private ColorAdjustments _colorAdjustments;
    private Vignette _vignette;
    private Coroutine _fadeCoroutine;
    private Collider[] _leftColliders;
    private Collider[] _rightColliders;
    private IWoundHandler[] _woundHandlers;

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
            Debug.LogWarning("[NarrationLockController] ColorAdjustments not found.");
        if (_vignette == null)
            Debug.LogWarning("[NarrationLockController] Vignette not found.");

        if (leftHandRoot != null)
            _leftColliders = leftHandRoot.GetComponentsInChildren<Collider>();
        if (rightHandRoot != null)
            _rightColliders = rightHandRoot.GetComponentsInChildren<Collider>();

        _woundHandlers = new IWoundHandler[woundHandlers != null ? woundHandlers.Length : 0];
        for (int i = 0; i < _woundHandlers.Length; i++)
            _woundHandlers[i] = woundHandlers[i] as IWoundHandler;
    }

    public bool IsLocked { get; private set; } = false;

    public void Lock()
    {
        IsLocked = true;
        DisableHands();
        SetHandCollidersEnabled(false);
        foreach (var w in _woundHandlers)
            w?.OnNarrationLock();
        PauseActiveHint();

        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeAll(targetExposure, targetVignette, uiDimmedAlpha));

        Debug.Log("[NarrationLockController] 🔒 Locked.");
    }

    public void Unlock()
    {
        EnableHands();
        foreach (var w in _woundHandlers)
            w?.OnNarrationLock(); // also clear on unlock so hand must re-enter
        SetHandCollidersEnabled(true);
        IsLocked = false;
        ResumeActiveHint();

        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
        _fadeCoroutine = StartCoroutine(FadeAll(0f, 0f, 1f));

        Debug.Log("[NarrationLockController] 🔓 Unlocked.");
    }

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

    private IEnumerator FadeAll(float toExposure, float toVignette, float toUIAlpha)
    {
        float fromExposure = _colorAdjustments != null ? _colorAdjustments.postExposure.value : 0f;
        float fromVignette = _vignette != null ? _vignette.intensity.value : 0f;

        float[] fromAlphas = new float[worldSpaceUI != null ? worldSpaceUI.Length : 0];
        if (worldSpaceUI != null)
            for (int i = 0; i < worldSpaceUI.Length; i++)
                fromAlphas[i] = worldSpaceUI[i] != null ? worldSpaceUI[i].alpha : 1f;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);

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