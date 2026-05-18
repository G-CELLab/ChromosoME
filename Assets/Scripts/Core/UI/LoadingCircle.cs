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
    private Sprite originalSprite;
    private Color originalColor;

    public void Initialize()
    {
        if (image != null)
        {
            originalSprite = image.sprite;
            originalColor  = image.color;
        }
    }

    public bool Tick(float deltaTime)
    {
        timer += deltaTime;
        if (image != null)
            image.fillAmount = Mathf.Clamp01(timer / duration);
        return timer >= duration;
    }

    public void Reset()
    {
        timer = 0f;
        if (image != null)
        {
            image.fillAmount = 0f;
            if (originalSprite != null) image.sprite = originalSprite;
            if (originalColor != default) image.color = originalColor;
        }
    }

    public void Complete()
    {
        timer = duration;
        if (image != null) image.fillAmount = 1f;
    }

    public void SetSprite(Sprite sprite)
    {
        if (image != null) image.sprite = sprite;
    }

    public void SetColor(Color color)
    {
        if (image != null) image.color = color;
    }

    public void SetVisible(bool visible)
    {
        if (image != null) image.enabled = visible;
    }
}