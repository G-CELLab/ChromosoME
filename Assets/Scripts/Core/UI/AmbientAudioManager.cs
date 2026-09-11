using System.Collections;
using UnityEngine;

/// <summary>
/// Loops a long ambient/background audio clip (e.g. a ~5 minute room-tone track).
/// Attach to the GameManager object and drop the clip into `ambientClip`.
///
/// Features:
///   - Loops seamlessly (standard AudioSource looping — fine for ambient noise
///     since there's no melodic phrase to line up, unlike music).
///   - Optional short crossfade at the loop point if your track has an audible
///     seam/click at start/end (enable `crossfadeAtLoop`).
///   - Fade in on start, fade out on command (SetAmbientEnabled / FadeOut).
///   - Survives scene reloads if `dontDestroyOnLoad` is checked, so it doesn't
///     restart/cut out on scene transitions.
/// </summary>
public class AmbientAudioManager : MonoBehaviour
{
    [Header("Clip")]
    [Tooltip("The ~5 minute ambient/room-tone loop.")]
    public AudioClip ambientClip;

    [Header("Playback")]
    [Range(0f, 1f)] public float targetVolume = 0.4f;
    public bool playOnAwake = true;
    public bool dontDestroyOnLoad = false;

    [Header("Fades")]
    [Tooltip("Seconds to fade in when playback starts.")]
    public float fadeInSeconds = 2f;
    [Tooltip("Seconds to fade out when Stop/Disable is called.")]
    public float fadeOutSeconds = 1.5f;

    [Header("Loop Crossfade (optional)")]
    [Tooltip("If the clip has an audible seam at the loop point, enable this to " +
             "crossfade between two sources a moment before the clip ends instead " +
             "of relying on AudioSource.loop's hard cut.")]
    public bool crossfadeAtLoop = false;
    [Tooltip("How many seconds before the clip ends to start the crossfade.")]
    public float crossfadeDuration = 1.5f;

    private AudioSource _sourceA;
    private AudioSource _sourceB;
    private AudioSource _activeSource;
    private Coroutine _fadeRoutine;
    private Coroutine _loopWatchRoutine;

    private void Awake()
    {
        if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);

        _sourceA = CreateSource("AmbientSourceA");
        if (crossfadeAtLoop) _sourceB = CreateSource("AmbientSourceB");

        _activeSource = _sourceA;
    }

    private void Start()
    {
        if (playOnAwake && ambientClip != null) Play();
    }

    private AudioSource CreateSource(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake  = false;
        src.loop         = !crossfadeAtLoop; // manual looping when crossfading
        src.spatialBlend = 0f;               // 2D ambient bed
        src.volume       = 0f;
        return src;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Play()
    {
        if (ambientClip == null)
        {
            Debug.LogWarning("[AmbientAudioManager] No ambientClip assigned.");
            return;
        }

        _activeSource.clip = ambientClip;
        _activeSource.Play();

        StartFade(_activeSource, targetVolume, fadeInSeconds);

        if (crossfadeAtLoop)
        {
            if (_loopWatchRoutine != null) StopCoroutine(_loopWatchRoutine);
            _loopWatchRoutine = StartCoroutine(WatchForLoop());
        }
    }

    public void Stop()
    {
        if (_loopWatchRoutine != null) { StopCoroutine(_loopWatchRoutine); _loopWatchRoutine = null; }
        StartFade(_sourceA, 0f, fadeOutSeconds, stopWhenDone: true);
        if (_sourceB != null) StartFade(_sourceB, 0f, fadeOutSeconds, stopWhenDone: true);
    }

    public void SetAmbientEnabled(bool enabled)
    {
        if (enabled) Play();
        else Stop();
    }

    public void SetVolume(float volume01)
    {
        targetVolume = Mathf.Clamp01(volume01);
        if (_activeSource != null && _activeSource.isPlaying)
            StartFade(_activeSource, targetVolume, 0.3f);
    }

    // ── Crossfade loop watcher ───────────────────────────────────────────────

    private IEnumerator WatchForLoop()
    {
        while (true)
        {
            if (_activeSource.clip == null) yield break;

            float timeLeft = _activeSource.clip.length - _activeSource.time;
            if (timeLeft <= crossfadeDuration)
            {
                var next = _activeSource == _sourceA ? _sourceB : _sourceA;
                next.clip = ambientClip;
                next.volume = 0f;
                next.time = 0f;
                next.Play();

                StartFade(_activeSource, 0f, crossfadeDuration, stopWhenDone: true);
                StartFade(next, targetVolume, crossfadeDuration);

                _activeSource = next;

                // Wait roughly a full loop before checking again.
                yield return new WaitForSeconds(Mathf.Max(0.1f, ambientClip.length - crossfadeDuration));
            }
            else
            {
                yield return new WaitForSeconds(0.25f);
            }
        }
    }

    // ── Fade helper ───────────────────────────────────────────────────────────

    private void StartFade(AudioSource src, float target, float duration, bool stopWhenDone = false)
    {
        StartCoroutine(FadeRoutine(src, target, duration, stopWhenDone));
    }

    private IEnumerator FadeRoutine(AudioSource src, float target, float duration, bool stopWhenDone)
    {
        float start = src.volume;
        float t = 0f;

        if (duration <= 0f)
        {
            src.volume = target;
        }
        else
        {
            while (t < duration)
            {
                t += Time.deltaTime;
                src.volume = Mathf.Lerp(start, target, t / duration);
                yield return null;
            }
            src.volume = target;
        }

        if (stopWhenDone && target <= 0f) src.Stop();
    }

    private void OnDisable()
    {
        if (_loopWatchRoutine != null) { StopCoroutine(_loopWatchRoutine); _loopWatchRoutine = null; }
    }
}