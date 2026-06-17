using UnityEngine;

public class WoundVisualController : MonoBehaviour
{
    public Renderer woundRenderer;
    public Material skinMaterial;

    public void HealWound()
    {
        if (woundRenderer != null && skinMaterial != null)
            woundRenderer.material = skinMaterial;
    }
}