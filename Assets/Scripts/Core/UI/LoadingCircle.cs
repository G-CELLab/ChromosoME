using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Reusable utility for a radial loading/progress circle UI element.
/// Call Tick(Time.deltaTime) each frame — returns true when complete.
/// Call Reset() to restart the progress.
/// </summary>
[System.Serializable]
public class LoadingCircle
{
    [Tooltip("How long in seconds until the circle completes.")]
    public float duration = 3f;

    [Tooltip("The UI Image component set to Filled/Radial 360.")]
    public Image image;

    private float timer = 0f;

    /// <summary>
    /// Advances the loading circle. Returns true once duration is reached.
    /// </summary>
    public bool Tick(float deltaTime)
    {
        timer += deltaTime;
        if (image != null)
            image.fillAmount = Mathf.Clamp01(timer / duration);
        return timer >= duration;
    }

    /// <summary>
    /// Resets progress back to zero.
    /// </summary>
    public void Reset()
    {
        timer = 0f;
        if (image != null) image.fillAmount = 0f;
    }

    /// <summary>
    /// Fills the circle instantly and locks it at completion (e.g. success state).
    /// </summary>
    public void Complete()
    {
        timer = duration;
        if (image != null) image.fillAmount = 1f;
    }

    /// <summary>
    /// Sets the image sprite (e.g. swap to a checkmark on success).
    /// </summary>
    public void SetSprite(Sprite sprite)
    {
        if (image != null) image.sprite = sprite;
    }

    /// <summary>
    /// Sets the image color.
    /// </summary>
    public void SetColor(Color color)
    {
        if (image != null) image.color = color;
    }

    public void SetVisible(bool visible)
    {
        if (image != null) image.enabled = visible;
    }
}