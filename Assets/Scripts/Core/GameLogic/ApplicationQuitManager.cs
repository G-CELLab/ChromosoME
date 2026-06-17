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
    private bool quitRequested = false;

    private void OnEnable()
    {
        handDetected = false;
        quitRequested = false;
        loadingCircle?.Reset();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (GameManager.eGameStatus != GameManager.GameState.GameOver) return;
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
        if (GameManager.eGameStatus != GameManager.GameState.GameOver)
        {
            if (handDetected) handDetected = false;
            if (loadingCircle != null) loadingCircle.Reset();
            return;
        }

        if (quitRequested) return;
        if (!handDetected) return;
        if (loadingCircle.Tick(Time.deltaTime))
        {
            quitRequested = true;
            Debug.Log("[ApplicationQuitHandler] Quitting application.");
            Application.Quit();
        }
    }
}