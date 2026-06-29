using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class DNAColliderManager : MonoBehaviour, IPhaseController
{
    [Header("Uncondensed Collider")]
    public Vector3 uncondensedCenter = Vector3.zero;
    public Vector3 uncondensedSize   = new Vector3(0.5f, 0.8f, 0.5f);

    [Header("Condensed Collider")]
    public Vector3 condensedCenter = Vector3.zero;
    public Vector3 condensedSize   = new Vector3(0.3f, 0.3f, 0.3f);

    [Header("Zone Registry")]
    [Tooltip("Display name used in drop logs, e.g. 'RedDNA' or 'BlueDNA1'.")]
    [SerializeField] private string zoneDisplayName = "DNA";

    private BoxCollider        box;
    private XRGrabInteractable grabInteractable;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        box = GetComponent<BoxCollider>();
        if (box == null) box = gameObject.AddComponent<BoxCollider>();
        SetUncondensed();
        RegisterCollider();
    }

    private void Start()
    {
        // Register with PlacementZoneRegistry so DroppableObjectTracker can
        // detect drops onto the DNA collider (e.g. incorrect placements).
        // Done in Start rather than Awake so the registry singleton is ready.
        if (PlacementZoneRegistry.Instance != null)
            PlacementZoneRegistry.Instance.RegisterZone(box, zoneDisplayName);
        else
            Debug.LogWarning($"[DNAColliderManager] PlacementZoneRegistry not found — zone '{zoneDisplayName}' not registered.");
    }

    private void OnDestroy()
    {
        if (PlacementZoneRegistry.Instance != null)
            PlacementZoneRegistry.Instance.UnregisterZone(box);
    }

    private void OnEnable()  => GameManager.Register(this);
    private void OnDisable() => GameManager.Unregister(this);

    // ── IPhaseController ──────────────────────────────────────────────────────

    public void OnPhaseEnter(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Metaphase) SetCondensed();
    }

    public void OnPhaseExit(GameManager.GameState phase)
    {
        if (phase == GameManager.GameState.Metaphase) SetUncondensed();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetUncondensed()
    {
        box.center = uncondensedCenter;
        box.size   = uncondensedSize;
    }

    private void SetCondensed()
    {
        box.center = condensedCenter;
        box.size   = condensedSize;
    }

    private void RegisterCollider()
    {
        if (grabInteractable == null) return;
        if (!grabInteractable.colliders.Contains(box))
        {
            grabInteractable.colliders.Clear();
            grabInteractable.colliders.Add(box);
        }
    }

    public void RebuildColliders() { }
}