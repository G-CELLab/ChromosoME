using System.Collections;
using UnityEngine;

public class FadeScreen : MonoBehaviour
{
    public float fadeDuration = 1.0f;
    public Color fadeColor = Color.black;
    private Renderer rend;

    private void Awake()
    {
        rend = GetComponent<Renderer>();
        SetAlpha(0f);

        if (transform.parent != null)
            transform.SetParent(null, true);

        DontDestroyOnLoad(gameObject); // survive scene reloads
    }

    public void StartFadeToClear()
    {
        StartCoroutine(FadeToClear());
    }

    public IEnumerator FadeToBlack()
    {
        gameObject.SetActive(true);
        rend = GetComponent<Renderer>();
        SetAlpha(0f);
        yield return FadeRoutine(0f, 1f);
    }

    public IEnumerator FadeToClear()
    {
        gameObject.SetActive(true);
        rend = GetComponent<Renderer>();
        SetAlpha(1f);
        yield return FadeRoutine(1f, 0f);
        gameObject.SetActive(false);
    }

    private IEnumerator FadeRoutine(float from, float to)
    {
        float timer = 0f;
        while (timer <= fadeDuration)
        {
            SetAlpha(Mathf.Lerp(from, to, timer / fadeDuration));
            timer += Time.deltaTime;
            yield return null;
        }
        SetAlpha(to);
    }

    private void SetAlpha(float alpha)
    {
        Color c = fadeColor;
        c.a = alpha;
        rend.material.SetColor("_BaseColor", c);
    }
}