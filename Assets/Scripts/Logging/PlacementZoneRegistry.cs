using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central registry of named placement zones used for drop-event logging.
/// Drag any collider from the scene into the Zones list in the Inspector and
/// give it a display name (e.g. "Mitochondria", "LeftPole", "MetaphaseCenter").
///
/// Dynamic colliders that don't exist at edit time (e.g. DNAColliderManager)
/// can register themselves at runtime via RegisterZone() / UnregisterZone().
///
/// DroppableObjectTracker queries this registry on every grab release to
/// determine which zone (if any) the released object overlaps, producing
/// Action:Dropped:ObjectName:ZoneName entries in the Other column.
///
/// Incorrect placements are captured automatically — if Protein is dropped
/// onto the Centriole zone, the log reads Action:Dropped:Protein:Centriole.
/// </summary>
public class PlacementZoneRegistry : MonoBehaviour
{
    [System.Serializable]
    public class ZoneEntry
    {
        [Tooltip("Drag any scene collider here — trigger or solid.")]
        public Collider zoneCollider;

        [Tooltip("Human-readable name written to the log, e.g. 'Mitochondria'.")]
        public string displayName;
    }

    [Header("Placement Zones (drag-and-drop)")]
    [SerializeField] private List<ZoneEntry> zones = new List<ZoneEntry>();

    // Runtime-registered zones — added via RegisterZone() for dynamic colliders
    // that don't exist at edit time (e.g. DNAColliderManager).
    private readonly List<ZoneEntry> runtimeZones = new List<ZoneEntry>();

    public static PlacementZoneRegistry Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    // ── Runtime Registration ──────────────────────────────────────────────────

    /// <summary>
    /// Registers a collider at runtime with a given display name.
    /// Use this for dynamic colliders that don't exist at edit time.
    /// Safe to call from Awake/Start on the collider's own GameObject.
    /// </summary>
    public void RegisterZone(Collider zoneCollider, string displayName)
    {
        if (zoneCollider == null || string.IsNullOrEmpty(displayName)) return;
        runtimeZones.Add(new ZoneEntry { zoneCollider = zoneCollider, displayName = displayName });
        Debug.Log($"[PlacementZoneRegistry] Runtime zone registered: {displayName}");
    }

    /// <summary>
    /// Removes a previously registered runtime zone.
    /// Call from OnDestroy on the collider's own GameObject.
    /// </summary>
    public void UnregisterZone(Collider zoneCollider)
    {
        runtimeZones.RemoveAll(e => e.zoneCollider == zoneCollider);
    }

    // ── Zone Queries ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the display name for a specific collider instance.
    /// Used by DroppableObjectTracker after a Physics.OverlapSphere hit.
    /// </summary>
    public string GetDisplayNameForCollider(Collider col)
    {
        if (col == null) return "";
        foreach (var entry in AllZones())
        {
            if (entry.zoneCollider == col && !string.IsNullOrEmpty(entry.displayName))
                return entry.displayName;
        }
        return "";
    }

    /// <summary>
    /// Returns the display name of the first zone whose bounds contain
    /// worldPoint, or "" if none match. Checks inspector zones first,
    /// then runtime-registered zones.
    /// </summary>
    public string GetZoneAtPoint(Vector3 worldPoint)
    {
        foreach (var entry in AllZones())
        {
            if (entry.zoneCollider == null || string.IsNullOrEmpty(entry.displayName))
                continue;
            if (entry.zoneCollider.bounds.Contains(worldPoint))
                return entry.displayName;
        }
        return "";
    }

    /// <summary>
    /// Returns the display name of the first zone whose bounds intersect
    /// bounds, or "" if none match.
    /// </summary>
    public string GetZoneOverlapping(Bounds bounds)
    {
        foreach (var entry in AllZones())
        {
            if (entry.zoneCollider == null || string.IsNullOrEmpty(entry.displayName))
                continue;
            if (entry.zoneCollider.bounds.Intersects(bounds))
                return entry.displayName;
        }
        return "";
    }

    /// <summary>
    /// Convenience overload — resolves zone from a Transform's world position.
    /// </summary>
    public string GetZoneAtTransform(Transform t)
    {
        if (t == null) return "";
        return GetZoneAtPoint(t.position);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IEnumerable<ZoneEntry> AllZones()
    {
        foreach (var z in zones)        yield return z;
        foreach (var z in runtimeZones) yield return z;
    }
}