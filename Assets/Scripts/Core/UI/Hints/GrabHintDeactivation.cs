using UnityEngine;

public class GrabHintDeactivation : MonoBehaviour
{
    [SerializeField] private GameObject magnesium;

    public float distanceFromStartPos;

    private Vector3 magnesiumStartPos;

    void Start()
    {
        magnesiumStartPos = magnesium.transform.position;
    }

    void Update()
    {
        if (IsMagnesiumGrabbed())
        {
            Destroy(gameObject);
        }
    }

    private bool IsMagnesiumGrabbed()
    {
        distanceFromStartPos = Vector3.Distance(magnesium.transform.position, magnesiumStartPos);
        return distanceFromStartPos > 0.01f;
    }
}