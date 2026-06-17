using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Records the initial transform and active state of all registered objects
/// and their children, then restores them on reset.
/// Drag in parent objects to reset entire hierarchies at once.
/// Runtime-created objects (e.g. SpindleFibers) are destroyed on reset.
/// </summary>
public class ResetManager : MonoBehaviour
{
    [Header("Objects to Reset")]
    public List<GameObject> objectsToReset;

    [Header("Runtime Created Parents")]
    [Tooltip("Children containing these name keywords will be destroyed on reset.")]
    public List<Transform> runtimeParents;
    public List<string> runtimeDestroyKeywords = new List<string> { "SpindleFiber" };

    private class ObjectState
    {
        public GameObject obj;
        public Transform originalParent;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;
        public bool activeSelf;
    }

    private List<ObjectState> savedStates = new List<ObjectState>();

    private void Awake()
    {
        SaveAllStates();
    }

    private void SaveAllStates()
    {
        savedStates.Clear();
        foreach (var root in objectsToReset)
        {
            if (root == null) continue;
            SaveHierarchy(root.transform);
        }
        Debug.Log($"[ResetManager] Saved state for {savedStates.Count} objects.");
    }

    private void SaveHierarchy(Transform t)
    {
        savedStates.Add(new ObjectState
        {
            obj           = t.gameObject,
            originalParent = t.parent,
            localPosition = t.localPosition,
            localRotation = t.localRotation,
            localScale    = t.localScale,
            activeSelf    = t.gameObject.activeSelf
        });

        foreach (Transform child in t)
            SaveHierarchy(child);
    }

    public void ResetAll()
    {
        // Destroy runtime-created objects first
        foreach (var parent in runtimeParents)
        {
            if (parent == null) continue;
            foreach (Transform child in parent)
            {
                foreach (var keyword in runtimeDestroyKeywords)
                {
                    if (child.name.Contains(keyword))
                    {
                        Destroy(child.gameObject);
                        break;
                    }
                }
            }
        }

        // Restore saved states
        foreach (var state in savedStates)
        {
            if (state.obj == null) continue;
            
            // Reparent to original parent if it's been moved
            if (state.obj.transform.parent != state.originalParent)
            {
                state.obj.transform.SetParent(state.originalParent);
            }
            
            state.obj.transform.localPosition = state.localPosition;
            state.obj.transform.localRotation = state.localRotation;
            state.obj.transform.localScale    = state.localScale;
            state.obj.SetActive(state.activeSelf);
        }

        Debug.Log("[ResetManager] All objects reset.");
    }
}