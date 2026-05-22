using UnityEngine;

public class GhostHandHint : MonoBehaviour
{
    [Header("Ghost Hand Settings")]
    [SerializeField] private GameObject ghostHandObject;
    [SerializeField] private float fadeSpeed = 2f;
    [SerializeField] private float targetAlpha = 0.4f;
    [Header("Collider Trigger")]
    [SerializeField] private Collider hintCollider;

    private Renderer[] renderers;
    private float currentAlpha;
    private bool playerHandInside = false;

    void Start()
    {
        renderers = ghostHandObject.GetComponentsInChildren<Renderer>();
        currentAlpha = targetAlpha;
        SetAlpha(currentAlpha);
        Debug.Log($"GhostHandHint found {renderers.Length} renderers");
    }

    void Update()
    {
        float target = playerHandInside ? 0f : targetAlpha;
        currentAlpha = Mathf.MoveTowards(currentAlpha, target, fadeSpeed * Time.deltaTime);
        SetAlpha(currentAlpha);
    }

    void SetAlpha(float alpha)
    {
        foreach (Renderer r in renderers)
        {
            foreach (Material mat in r.materials)
            {
                Color c = mat.color;
                c.a = alpha;
                mat.color = c;

                // Fade emission too
                Color emission = mat.GetColor("_EmissionColor");
                emission.a = alpha;
                mat.SetColor("_EmissionColor", emission * alpha);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Right"))
            playerHandInside = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Right"))
            playerHandInside = false;
    }

    public void DestroyHint()
    {
        Destroy(gameObject);
    }
}