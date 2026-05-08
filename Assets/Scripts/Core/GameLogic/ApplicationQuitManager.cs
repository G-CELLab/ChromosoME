using UnityEngine;

/// <summary>
/// Quits the application when the player holds their hand over this trigger
/// for the required duration. Placed on the healed wound at game end.
/// </summary>
public class ApplicationQuitHandler : MonoBehaviour
{
    [Header("Loading Circle")]
    public LoadingCircle loadingCircle;

    private bool handDetected = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Left") || other.CompareTag("Right"))
            handDetected = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Left") || other.CompareTag("Right"))
        {
            handDetected = false;
            loadingCircle.Reset();
        }
    }

    private void Update()
    {
        if (!handDetected) return;
        if (loadingCircle.Tick(Time.deltaTime))
        {
            Debug.Log("[ApplicationQuitHandler] Quitting application.");
            Application.Quit();
        }
    }
}