using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.XR.Hands;
using Unity.XR.CoreUtils;
using System;

/// <summary>
/// Manages hand tracking, fist detection, and XRI selection for one hand.
/// Replaces LeftHandManager and RightHandManager.
/// Set Side in the inspector — Left hand = Left, Right hand = Right.
/// </summary>
public class HandManager : MonoBehaviour, IPhaseController
{
    public enum Side { Left, Right }

    [Header("Configuration")]
    public Side side;

    [Header("State")]
    public bool isGrabbed = false;

    [Header("Interactor Source")]
    [SerializeField] private NearFarInteractor handInteractor;

    [Header("Fist Settings")]
    [SerializeField] private float fistThreshold = 0.08f;

    private XRHandSubsystem handSubsystem;
    private static List<XRHandSubsystem> s_Subsystems = new List<XRHandSubsystem>();
    private XROrigin xrOrigin;

    private Transform visualPalm;
    private Vector3 lastValidWorldPos;
    private Quaternion lastValidWorldRot;
    private bool hasEverBeenTracked = false;
    private bool isManualSelecting = false;
    private float grabCooldownUntil = 0f;
    private bool releaseCycleRunning = false;

    // Tracks the last frame's grab state so we can detect the falling edge
    private bool wasGrabbed = false;

    // ── Public Events ─────────────────────────────────────────────────────────

    /// <summary>
    /// Fired on the frame isGrabbed transitions from true to false.
    /// The string argument is the name of the object that was held,
    /// resolved via ColliderNameResolver before the selection clears.
    /// Empty string if nothing identifiable was held.
    /// </summary>
    public event Action<string> OnGrabReleased;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnEnable()  => GameManager.Register(this);
    private void OnDisable() => GameManager.Unregister(this);

    void Start()
    {
        xrOrigin = UnityEngine.Object.FindAnyObjectByType<XROrigin>();
        EnsureInteractorReference();
        ConfigureInteractorInput();
        FindVisualPalm();

        lastValidWorldPos = transform.position;
        lastValidWorldRot = transform.rotation;
    }

    void Update()
    {
        EnsureInteractorReference();

        if (TryGetPalmWorldPose(out Vector3 pPos, out Quaternion pRot))
        {
            lastValidWorldPos   = pPos;
            lastValidWorldRot   = pRot;
            hasEverBeenTracked  = true;
        }
        else if (visualPalm != null && visualPalm.gameObject.activeInHierarchy)
        {
            lastValidWorldPos   = visualPalm.position;
            lastValidWorldRot   = visualPalm.rotation;
            hasEverBeenTracked  = true;
        }

        if (hasEverBeenTracked && handInteractor != null)
        {
            handInteractor.transform.position = lastValidWorldPos;
            handInteractor.transform.rotation = lastValidWorldRot;
        }

        bool gestureActive = EvaluateClosedFist();

        // Detect grab release — fire event before UpdateXRISelection clears
        // the interactor's selection so GetHeldObjectName() still works.
        if (wasGrabbed && !gestureActive)
            FireGrabReleased();

        wasGrabbed = gestureActive;

        UpdateXRISelection(gestureActive);
        isGrabbed = gestureActive;
    }

    // ── IPhaseController ──────────────────────────────────────────────────────

    public void OnPhaseEnter(GameManager.GameState phase) { }

    public void OnPhaseExit(GameManager.GameState phase)
    {
        ForceRelease();
    }

    private void ForceRelease()
    {
        if (handInteractor == null) return;

        isManualSelecting = false;
        grabCooldownUntil = Time.time + 0.5f;

        if (HasCurrentSelection())
        {
            try { handInteractor.EndManualInteraction(); }
            catch (System.Exception) { }

            try
            {
                var mgr = handInteractor.interactionManager;
                if (mgr != null)
                    mgr.CancelInteractorSelection(
                        (UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor)handInteractor);
            }
            catch (System.Exception) { }
        }

        if (!releaseCycleRunning)
            StartCoroutine(CycleInteractorEnabled());
    }

    private IEnumerator CycleInteractorEnabled()
    {
        releaseCycleRunning = true;
        handInteractor.enabled = false;
        yield return null;
        handInteractor.enabled = true;
        releaseCycleRunning = false;
    }

    // ── Setup ─────────────────────────────────────────────────────────────────

    private void ConfigureInteractorInput()
    {
        if (handInteractor == null) return;

        handInteractor.selectInput.inputSourceMode =
            (UnityEngine.XR.Interaction.Toolkit.Inputs.Readers.XRInputButtonReader.InputSourceMode)4;
        handInteractor.attachTransform = null;

        foreach (Transform child in handInteractor.transform)
        {
            if (child.name.Contains("Select Input") || child.name.Contains("UI Press Input"))
                child.gameObject.SetActive(false);
        }
    }

    private void FindVisualPalm()
    {
        if (transform.parent == null) return;

        string palmName = side == Side.Left ? "L_Palm" : "R_Palm";
        foreach (Transform child in transform.parent.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == palmName && child.gameObject.activeInHierarchy)
            {
                visualPalm = child;
                return;
            }
        }
    }

    private void EnsureInteractorReference()
    {
        if (handInteractor != null && handInteractor.gameObject.activeInHierarchy) return;
        handInteractor = GetComponentInChildren<NearFarInteractor>(true);
        if (handInteractor == null && transform.parent != null)
            handInteractor = transform.parent.GetComponentInChildren<NearFarInteractor>(true);
    }

    // ── XRI Selection ─────────────────────────────────────────────────────────

    private void UpdateXRISelection(bool active)
    {
        if (handInteractor == null || !handInteractor.enabled || !handInteractor.gameObject.activeInHierarchy)
            return;

        if (!active)
        {
            if (isManualSelecting || HasCurrentSelection())
                ForceRelease();
            return;
        }

        if (Time.time < grabCooldownUntil)
        {
            if (HasCurrentSelection())
                ForceRelease();
            return;
        }

        if (isManualSelecting && !HasCurrentSelection())
        {
            isManualSelecting = false;
            grabCooldownUntil = Time.time + 0.5f;
            return;
        }

        if (!isManualSelecting && HasCurrentSelection())
        {
            ForceRelease();
            return;
        }

        if (!isManualSelecting)
        {
            var targets = handInteractor.interactablesHovered;
            if (targets != null && targets.Count > 0)
            {
                var target = targets[0] as UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable;
                if (target != null && target.IsSelectableBy(handInteractor))
                {
                    handInteractor.StartManualInteraction(target);
                    isManualSelecting = true;
                }
            }
        }
    }

    private bool HasCurrentSelection()
    {
        return handInteractor != null
            && handInteractor.interactablesSelected != null
            && handInteractor.interactablesSelected.Count > 0;
    }

    // ── Grab Release ──────────────────────────────────────────────────────────

    private void FireGrabReleased()
    {
        string heldName = GetHeldObjectName();
        OnGrabReleased?.Invoke(heldName);
    }

    /// <summary>
    /// Returns the ColliderNameResolver display name of the currently held
    /// object, or "" if nothing is held or the name cannot be resolved.
    /// Safe to call from OnGrabReleased listeners — fires before selection clears.
    /// </summary>
    public string GetHeldObjectName()
    {
        if (handInteractor == null) return "";

        var selected = handInteractor.interactablesSelected;
        if (selected == null || selected.Count == 0) return "";

        var interactable = selected[0];
        if (interactable == null) return "";

        Transform t = (interactable as UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable)?.transform
                   ?? (interactable as Component)?.transform;

        if (t == null) return "";

        return ColliderNameResolver.ResolveName(t);
    }

    /// <summary>
    /// Returns the world-space position of the currently held object,
    /// or Vector3.zero if nothing is held.
    /// </summary>
    public Vector3 GetHeldObjectPosition()
    {
        if (handInteractor == null) return Vector3.zero;

        var selected = handInteractor.interactablesSelected;
        if (selected == null || selected.Count == 0) return Vector3.zero;

        var interactable = selected[0];
        Transform t = (interactable as UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable)?.transform
                   ?? (interactable as Component)?.transform;

        return t != null ? t.position : Vector3.zero;
    }

    /// <summary>
    /// Returns the Bounds of the currently held object's first collider,
    /// or an empty Bounds at Vector3.zero if nothing is held.
    /// </summary>
    public Bounds GetHeldObjectBounds()
    {
        if (handInteractor == null) return new Bounds();

        var selected = handInteractor.interactablesSelected;
        if (selected == null || selected.Count == 0) return new Bounds();

        var interactable = selected[0];
        Transform t = (interactable as UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable)?.transform
                   ?? (interactable as Component)?.transform;

        if (t == null) return new Bounds();

        Collider col = t.GetComponentInChildren<Collider>();
        return col != null ? col.bounds : new Bounds(t.position, Vector3.one * 0.1f);
    }

    // ── Hand Tracking ─────────────────────────────────────────────────────────

    private bool TryGetPalmWorldPose(out Vector3 pos, out Quaternion rot)
    {
        pos = Vector3.zero;
        rot = Quaternion.identity;

        if (handSubsystem == null || !handSubsystem.running) TryFindHandSubsystem();
        if (handSubsystem == null || !handSubsystem.running) return false;

        XRHand hand = side == Side.Left ? handSubsystem.leftHand : handSubsystem.rightHand;
        if (!hand.isTracked) return false;

        var palm = hand.GetJoint(XRHandJointID.Palm);
        if (!palm.TryGetPose(out var pose)) return false;

        if (xrOrigin != null)
        {
            pos = xrOrigin.transform.TransformPoint(pose.position);
            rot = xrOrigin.transform.rotation * pose.rotation;
        }
        else
        {
            pos = pose.position;
            rot = pose.rotation;
        }

        return true;
    }

    private bool EvaluateClosedFist()
    {
        if (handSubsystem == null || !handSubsystem.running) TryFindHandSubsystem();
        if (handSubsystem == null || !handSubsystem.running) return false;

        XRHand hand = side == Side.Left ? handSubsystem.leftHand : handSubsystem.rightHand;
        if (!hand.isTracked) return false;

        var palm = hand.GetJoint(XRHandJointID.Palm);
        if (!palm.TryGetPose(out var pPose)) return false;

        int curled = 0;
        XRHandJointID[] tips =
        {
            XRHandJointID.IndexTip, XRHandJointID.MiddleTip,
            XRHandJointID.RingTip,  XRHandJointID.LittleTip
        };

        foreach (var id in tips)
        {
            if (hand.GetJoint(id).TryGetPose(out var tipPose))
                if (Vector3.Distance(pPose.position, tipPose.position) <= fistThreshold)
                    curled++;
        }

        return curled >= 3;
    }

    private void TryFindHandSubsystem()
    {
        SubsystemManager.GetSubsystems(s_Subsystems);
        foreach (var s in s_Subsystems)
        {
            if (s.running) { handSubsystem = s; return; }
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void SetInteractionEnabled(bool enabled)
    {
        if (handInteractor != null)
            handInteractor.enabled = enabled;
    }
}