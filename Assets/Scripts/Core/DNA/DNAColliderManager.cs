using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Collections.Generic;

/// <summary>
/// Dynamically generates and scales colliders on the DNA mesh based on
/// the CondenseProgress animator parameter driven by DNACondenser.
/// </summary>
[ExecuteInEditMode]
public class DNAColliderManager : MonoBehaviour
{
    [Header("Interaction Settings")]
    public float baseRadius          = 0.04f;
    public float condensedRadiusFactor = 1.5f;
    public float colliderGrowthSpeed = 2.0f;

    [Header("Runtime Info")]
    public float currentLerp = 0f;

    private Animator animator;
    private SkinnedMeshRenderer skinnedMeshRenderer;
    private XRGrabInteractable grabInteractable;

    private const string PREFIX = "_DNA_COLLIDER_";

    [ContextMenu("Setup Colliders")]
    public void InitializeColliders()
    {
        animator            = GetComponent<Animator>();
        skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        grabInteractable    = GetComponent<XRGrabInteractable>();

        // Cleanup existing generated colliders
        var allChildren = GetComponentsInChildren<Transform>(true);
        for (int i = allChildren.Length - 1; i >= 0; i--)
        {
            Transform t = allChildren[i];
            if (t != null && (t.name.StartsWith(PREFIX) || t.name.StartsWith("_InteractionSegment")))
            {
                if (Application.isPlaying) Destroy(t.gameObject);
                else DestroyImmediate(t.gameObject);
            }
        }

        foreach (var t in allChildren)
        {
            if (t == null || t.name.StartsWith(PREFIX) || t.name.StartsWith("_InteractionSegment")) continue;
            foreach (var col in t.GetComponents<Collider>())
            {
                if (col is BoxCollider || col is SphereCollider || col is CapsuleCollider)
                {
                    if (Application.isPlaying) Destroy(col);
                    else DestroyImmediate(col);
                }
            }
        }

        List<Collider> newColliders = new List<Collider>();

        if (skinnedMeshRenderer != null && skinnedMeshRenderer.bones != null && skinnedMeshRenderer.bones.Length > 0)
            SetupSkeletal(newColliders);
        else
            SetupMeshFallback(newColliders);

        UpdateColliderRadii(newColliders, 0);

        if (grabInteractable != null)
        {
            grabInteractable.colliders.Clear();
            foreach (var c in newColliders) grabInteractable.colliders.Add(c);
        }
    }

    private void SetupSkeletal(List<Collider> colliders)
    {
        foreach (var bone in skinnedMeshRenderer.bones)
        {
            if (bone == null) continue;

            List<Transform> validChildren = new List<Transform>();
            for (int i = 0; i < bone.childCount; i++)
            {
                Transform child = bone.GetChild(i);
                if (!child.name.StartsWith("_")) validChildren.Add(child);
            }

            if (validChildren.Count == 0)
            {
                GameObject tip = new GameObject(PREFIX + "Tip");
                tip.transform.SetParent(bone, false);
                tip.transform.localPosition = Vector3.zero;
                SphereCollider sc = tip.AddComponent<SphereCollider>();
                sc.radius = baseRadius;
                colliders.Add(sc);
            }
            else
            {
                foreach (var child in validChildren)
                {
                    Vector3 direction = child.localPosition;
                    float length      = direction.magnitude;
                    if (length < 0.001f) continue;

                    GameObject segment = new GameObject(PREFIX + "Segment");
                    segment.transform.SetParent(bone, false);
                    segment.transform.localPosition = direction * 0.5f;
                    if (direction != Vector3.zero)
                        segment.transform.localRotation = Quaternion.LookRotation(direction);

                    CapsuleCollider cc = segment.AddComponent<CapsuleCollider>();
                    cc.direction = 2;
                    cc.height    = length * 1.2f;
                    cc.center    = Vector3.zero;
                    colliders.Add(cc);
                }
            }
        }
    }

    private void SetupMeshFallback(List<Collider> colliders)
    {
        foreach (var rend in GetComponentsInChildren<MeshRenderer>(true))
        {
            if (rend.gameObject == gameObject) continue;

            GameObject segment = new GameObject(PREFIX + "Mesh");
            segment.transform.SetParent(rend.transform, false);
            segment.transform.localPosition = Vector3.zero;
            segment.transform.localRotation = Quaternion.identity;

            CapsuleCollider cc = segment.AddComponent<CapsuleCollider>();
            Bounds b = rend.localBounds;
            int dir  = 1;
            if      (b.size.x > b.size.y && b.size.x > b.size.z) dir = 0;
            else if (b.size.z > b.size.y && b.size.z > b.size.x) dir = 2;
            cc.direction = dir;
            cc.height    = b.size[dir] * 1.2f;
            cc.center    = b.center;
            colliders.Add(cc);
        }

        if (colliders.Count == 0)
        {
            SphereCollider sc = gameObject.AddComponent<SphereCollider>();
            sc.radius = baseRadius * 5f;
            colliders.Add(sc);
        }
    }

    void Start()
    {
        if (Application.isPlaying) InitializeColliders();
    }

    void Update()
    {
        if (animator == null) return;

        float targetLerp = 0f;
        if      (HasParam("CondenseProgress")) targetLerp = animator.GetFloat("CondenseProgress");
        else if (HasParam("isOpened"))         targetLerp = animator.GetBool("isOpened") ? 1f : 0f;
        else if (HasParam("IsCondensed"))      targetLerp = animator.GetBool("IsCondensed") ? 1f : 0f;

        currentLerp = Mathf.MoveTowards(currentLerp, targetLerp, Time.deltaTime * colliderGrowthSpeed);

        var cols   = GetComponentsInChildren<Collider>(true);
        var myCols = new List<Collider>();
        foreach (var c in cols)
            if (c != null && c.gameObject.name.StartsWith(PREFIX)) myCols.Add(c);

        UpdateColliderRadii(myCols, currentLerp);
    }

    private bool HasParam(string paramName)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return false;
        foreach (var p in animator.parameters)
            if (p.name == paramName) return true;
        return false;
    }

    private void UpdateColliderRadii(List<Collider> colliders, float lerp)
    {
        float targetRadius = baseRadius * Mathf.Lerp(1f, condensedRadiusFactor, lerp);

        foreach (var col in colliders)
        {
            if (col == null) continue;

            if (col is CapsuleCollider cc)
            {
                Vector3 scale  = cc.transform.lossyScale;
                float perpScale = cc.direction == 0 ? Mathf.Max(scale.y, scale.z)
                                : cc.direction == 1 ? Mathf.Max(scale.x, scale.z)
                                :                     Mathf.Max(scale.x, scale.y);
                if (perpScale > 0) cc.radius = targetRadius / perpScale;
            }
            else if (col is SphereCollider sc)
            {
                float maxScale = Mathf.Max(sc.transform.lossyScale.x,
                                 Mathf.Max(sc.transform.lossyScale.y, sc.transform.lossyScale.z));
                if (maxScale > 0) sc.radius = targetRadius / maxScale;
            }
        }
    }
}