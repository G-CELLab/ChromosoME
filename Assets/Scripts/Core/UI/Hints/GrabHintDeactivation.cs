using UnityEngine;

public class GrabHintDeactivation : MonoBehaviour
{
    [SerializeField] private GameObject magnesium;

    void Update()
    {
        if (!magnesium.activeInHierarchy)
        {
            Destroy(gameObject);
        }
    }
}