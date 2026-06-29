using UnityEngine;

/// <summary>
/// Watches both HandManagers for grab release events and logs
/// Action:Dropped:ObjectName:ZoneName to the Other column via MainLogger.
///
/// Uses Physics.OverlapSphere at the held object's release position for
/// reliable zone detection — bounds.Contains() is unreliable for capsule
/// colliders since Bounds is always an AABB.
///
/// Captures both correct and incorrect placements — if Protein is released
/// inside the Centriole zone instead of Mitochondria, the log reads:
///   Action:Dropped:Protein:Centriole
///
/// If the object is released with no zone overlap the event is not logged,
/// since a drop in empty space is not a meaningful placement.
///
/// Attach to any persistent GameObject in the scene (e.g. LoggingSystem).
/// Assign Left Hand Manager and Right Hand Manager in the Inspector.
/// </summary>
public class DroppableObjectTracker : MonoBehaviour
{
    [Header("Hand References")]
    [SerializeField] private HandManager leftHandManager;
    [SerializeField] private HandManager rightHandManager;

    [Header("Zone Detection")]
    [Tooltip("Radius of the overlap sphere used to find placement zones at release. " +
             "Increase if drops are missed; decrease if wrong zones are detected.")]
    [SerializeField] private float overlapRadius = 0.15f;

    [Header("Debug")]
    [SerializeField] private bool logToConsole = false;

    private void Start()
    {
        if (leftHandManager == null)
            leftHandManager = FindHandManagerBySide(HandManager.Side.Left);
        if (rightHandManager == null)
            rightHandManager = FindHandManagerBySide(HandManager.Side.Right);

        if (leftHandManager == null)
        {
            Debug.LogError("[DroppableObjectTracker] Left HandManager not found!");
            return;
        }
        if (rightHandManager == null)
        {
            Debug.LogError("[DroppableObjectTracker] Right HandManager not found!");
            return;
        }

        leftHandManager.OnGrabReleased  += OnLeftReleased;
        rightHandManager.OnGrabReleased += OnRightReleased;
    }

    private void OnDestroy()
    {
        if (leftHandManager  != null) leftHandManager.OnGrabReleased  -= OnLeftReleased;
        if (rightHandManager != null) rightHandManager.OnGrabReleased -= OnRightReleased;
    }

    // ── Release Handlers ──────────────────────────────────────────────────────

    private void OnLeftReleased(string heldObjectName)  => HandleRelease(leftHandManager,  heldObjectName);
    private void OnRightReleased(string heldObjectName) => HandleRelease(rightHandManager, heldObjectName);

    private void HandleRelease(HandManager hand, string heldObjectName)
    {
        if (string.IsNullOrEmpty(heldObjectName)) return;

        if (PlacementZoneRegistry.Instance == null)
        {
            Debug.LogWarning("[DroppableObjectTracker] PlacementZoneRegistry not found in scene.");
            return;
        }

        Vector3 releasePos = hand.GetHeldObjectPosition();
        if (releasePos == Vector3.zero) return;

        // Physics.OverlapSphere is reliable for all collider shapes including
        // capsules — unlike bounds.Contains() which uses AABB and misses when
        // the object centre is outside the box but inside the actual capsule.
        Collider[] hits = Physics.OverlapSphere(releasePos, overlapRadius);

        string zoneName = "";
        foreach (Collider hit in hits)
        {
            string candidate = PlacementZoneRegistry.Instance.GetDisplayNameForCollider(hit);
            if (string.IsNullOrEmpty(candidate)) continue;

            // Skip exact self-drops
            if (candidate == heldObjectName) continue;

            // Skip partial self-drops — e.g. "RedDNA" dropped on "DNA" zone,
            // or "CentrioleCopy" dropped on "Centriole" zone. The DNA object
            // registers its own collider as a zone so this prevents it from
            // showing up as a drop target for itself.
            if (heldObjectName.Contains(candidate) || candidate.Contains(heldObjectName)) continue;

            zoneName = candidate;
            break;
        }

        if (string.IsNullOrEmpty(zoneName))
        {
            if (logToConsole)
                Debug.Log($"[DroppableObjectTracker] No zone found for {heldObjectName} at {releasePos}");
            return;
        }

        string logEntry = $"Action:Dropped:{heldObjectName}:{zoneName}";

        if (logToConsole)
            Debug.Log($"[DroppableObjectTracker] {logEntry}");

        // Use immediate logging so the event is timestamped to the actual
        // release frame rather than the next 0.1s log interval tick.
        MainLogger.LogImmediateOtherEvent(logEntry);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private HandManager FindHandManagerBySide(HandManager.Side side)
    {
        foreach (var hm in FindObjectsByType<HandManager>())
            if (hm.side == side) return hm;
        return null;
    }
}