using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using System.Collections.Generic;
using UnityEngine.XR.Hands;
using Unity.XR.CoreUtils;

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

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnEnable()  => GameManager.Register(this);
    private void OnDisable() => GameManager.Unregister(this);

    void Start()
    {
        xrOrigin = Object.FindAnyObjectByType<XROrigin>();
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

        if (!HasCurrentSelection())
        {
            isManualSelecting = false;
            return;
        }

        try { handInteractor.EndManualInteraction(); }
        catch (System.Exception) { /* interactable was destroyed, nothing to release */ }
        finally { isManualSelecting = false; }
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

        if (isManualSelecting && !HasCurrentSelection())
            isManualSelecting = false;

        if (active && !isManualSelecting)
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
        else if (!active && isManualSelecting)
        {
            ForceRelease();
        }
    }

    private bool HasCurrentSelection()
    {
        return handInteractor != null && handInteractor.interactablesSelected != null && handInteractor.interactablesSelected.Count > 0;
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

        // At least 3 fingers curled = closed fist
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
}