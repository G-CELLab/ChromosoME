using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Data-driven gesture timing system. Reads a list of GestureDefinition assets
/// and triggers the first one whose keywords appear in the AI response.
///
/// Timing is synchronized to actual audio playback: the gesture fires at the
/// estimated moment the AI speaks the matched keyword.
///
/// To add a new gesture: create a GestureDefinition asset and drag it into
/// the Gesture Definitions list in the Inspector. No code changes needed.
/// </summary>
public class GestureSynchronizer : MonoBehaviour
{
    [Header("References")]
    public TTSAnimatorDriver ttsDriver;
    public TextToSpeechPlayer ttsPlayer;

    [Header("Gesture Definitions")]
    [Tooltip("List of all available gestures. Each is a GestureDefinition ScriptableObject. " +
             "Order matters — the first keyword match wins.")]
    public List<GestureDefinition> gestureDefinitions = new List<GestureDefinition>();

    [Header("Timing")]
    [Tooltip("Estimated speaking rate in characters per second (~200 wpm = 15.3 cps)")]
    public float charactersPerSecond = 15.3f;

    [Tooltip("If the calculated delay exceeds this, it is clamped to this value.")]
    public float maxGestureDelaySec = 10f;

    [Tooltip("How long to wait for audio playback to start before falling back to relative timing.")]
    public float maxWaitForAudioStartSec = 6f;

    [Header("Debug")]
    public bool verboseDebug = true;
    public bool showTimingCalculations = true;

    // ── State ─────────────────────────────────────────────────────────────────

    private Coroutine _activeGestureCoroutine;
    private float     _audioStartTime = -1f;
    private bool      _gestureTriggeredThisResponse = false;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (ttsDriver == null) ttsDriver = FindAnyObjectByType<TTSAnimatorDriver>();
        if (ttsPlayer == null) ttsPlayer = FindAnyObjectByType<TextToSpeechPlayer>();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Call this when a new AI response begins.</summary>
    public void OnResponseStart()
    {
        _audioStartTime = -1f;
        _gestureTriggeredThisResponse = false;
        CancelActiveGesture();

        if (verboseDebug)
            Debug.Log("[GestureSynchronizer] 🆕 New response — state reset.");
    }

    /// <summary>Call this when TTS audio playback actually begins.</summary>
    public void OnAudioPlaybackStart()
    {
        _audioStartTime = Time.realtimeSinceStartup;

        if (verboseDebug)
            Debug.Log($"[GestureSynchronizer] 🔊 Audio started at {_audioStartTime:F2}s");
    }

    /// <summary>
    /// Process the accumulated AI response text and schedule a gesture if a
    /// keyword is found. Safe to call repeatedly as tokens stream in — only
    /// the first match per response is acted on.
    /// </summary>
    public bool ProcessResponse(string accumulatedText)
    {
        if (_gestureTriggeredThisResponse) return false;
        if (string.IsNullOrWhiteSpace(accumulatedText)) return false;

        string normalized = NormalizeText(accumulatedText);

        // Iterate definitions in order — first match wins
        foreach (GestureDefinition def in gestureDefinitions)
        {
            if (def == null || def.keywords == null || def.keywords.Length == 0) continue;

            foreach (string keyword in def.keywords)
            {
                if (string.IsNullOrWhiteSpace(keyword)) continue;

                int keywordIndex = GetWholeWordPosition(normalized, keyword.ToLowerInvariant());
                if (keywordIndex < 0) continue;

                // Found a match
                float delay = CalculateDelay(normalized, keywordIndex, def);

                _gestureTriggeredThisResponse = true;
                _activeGestureCoroutine = StartCoroutine(TriggerAfterDelay(def, keyword, delay));

                if (verboseDebug)
                    Debug.Log($"[GestureSynchronizer] {def.logEmoji} Matched '{keyword}' " +
                              $"for gesture '{def.gestureName}' — scheduled in {delay:F2}s");

                return true;
            }
        }

        return false;
    }

    public bool IsGestureTriggeredThisResponse => _gestureTriggeredThisResponse;

    // ── Timing ────────────────────────────────────────────────────────────────

    private float CalculateDelay(string normalizedText, int keywordIndex, GestureDefinition def)
    {
        if (keywordIndex < 0 || keywordIndex >= normalizedText.Length)
        {
            float fallback = Mathf.Max(0f, def.fallbackDelaySeconds - Mathf.Max(0f, def.advanceSeconds));
            if (showTimingCalculations)
                Debug.LogWarning($"[GestureSynchronizer] ⚠️ Invalid keyword index for '{def.gestureName}', " +
                                 $"using fallback {fallback:F2}s");
            return fallback;
        }

        float rawTime     = keywordIndex / Mathf.Max(1f, charactersPerSecond);
        float scaledTime  = rawTime * Mathf.Clamp(def.timeScale, 0.2f, 2.0f);
        float totalDelay  = Mathf.Clamp(scaledTime - Mathf.Max(0f, def.advanceSeconds), 0f, maxGestureDelaySec);

        if (showTimingCalculations)
            Debug.Log($"[GestureSynchronizer] 📊 '{def.gestureName}' timing:" +
                      $"\n  Chars before keyword : {keywordIndex}" +
                      $"\n  Raw speak time       : {rawTime:F2}s" +
                      $"\n  Time scale           : {def.timeScale:F2}" +
                      $"\n  Advance              : {def.advanceSeconds:F2}s" +
                      $"\n  Final delay          : {totalDelay:F2}s");

        return totalDelay;
    }

    // ── Coroutine ─────────────────────────────────────────────────────────────

    private IEnumerator TriggerAfterDelay(GestureDefinition def, string matchedKeyword, float delaySec)
    {
        float waitStart = Time.realtimeSinceStartup;

        // Wait for audio to start, then seek to the right offset
        if (_audioStartTime <= 0f)
        {
            float waited = 0f;
            while (_audioStartTime <= 0f && waited < maxWaitForAudioStartSec)
            {
                yield return null;
                waited += Time.unscaledDeltaTime;
            }

            if (_audioStartTime <= 0f)
            {
                if (verboseDebug)
                    Debug.LogWarning($"[GestureSynchronizer] ⚠️ Audio start not detected within " +
                                     $"{maxWaitForAudioStartSec:F1}s — using relative wait.");
                yield return new WaitForSecondsRealtime(delaySec);
            }
            else
            {
                yield return new WaitForSecondsRealtime(delaySec);
            }
        }
        else
        {
            float elapsed   = Time.realtimeSinceStartup - _audioStartTime;
            float remaining = Mathf.Max(0f, delaySec - elapsed);
            yield return new WaitForSecondsRealtime(remaining);
        }

        float actualWait = Time.realtimeSinceStartup - waitStart;

        if (ttsDriver == null)
        {
            Debug.LogWarning($"[GestureSynchronizer] ❌ Cannot trigger '{def.gestureName}' — TTSAnimatorDriver is null.");
            yield break;
        }

        ttsDriver.TriggerGesture(def);
        _activeGestureCoroutine = null;

        if (verboseDebug)
            Debug.Log($"[GestureSynchronizer] {def.logEmoji} Triggered '{def.gestureName}' " +
                      $"(keyword: '{matchedKeyword}') after {actualWait:F2}s actual wait.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void CancelActiveGesture()
    {
        if (_activeGestureCoroutine == null) return;
        StopCoroutine(_activeGestureCoroutine);
        _activeGestureCoroutine = null;

        if (verboseDebug)
            Debug.Log("[GestureSynchronizer] 🚫 Cancelled active gesture.");
    }

    private static int GetWholeWordPosition(string text, string keyword)
    {
        string pattern = @"\b" + Regex.Escape(keyword) + @"\b";
        Match match = Regex.Match(text, pattern);
        return match.Success ? match.Index : -1;
    }

    private static string NormalizeText(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        text = text.ToLowerInvariant();
        text = Regex.Replace(text, @"[^\w\s]", " ");
        text = Regex.Replace(text, @"\s+", " ");
        return text.Trim();
    }

    // ── Inspector helpers ─────────────────────────────────────────────────────

    public void SetCharactersPerSecond(float cps)
    {
        charactersPerSecond = Mathf.Clamp(cps, 5f, 30f);
        if (verboseDebug)
            Debug.Log($"[GestureSynchronizer] ⚙️ Characters per second → {charactersPerSecond:F1}");
    }
}