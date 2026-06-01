using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Central AI orchestrator for the VR biology tutor.
///
/// Pipeline:
///   OnTranscriptReceived(text)
///     → TutorSceneState.Refresh()
///     → RAGIndex.Retrieve()
///     → AIResponseGenerator.GenerateStreamingResponse()
///         → per sentence: EnqueueTTS() → PrefetchNext() → DrainTTSQueue()
///     → GestureSynchronizer.ProcessResponse() (keyword-driven, no fallback)
///
/// Interruption:
///   OnSpeechStarted fires as soon as VAD detects the user speaking.
///   This immediately stops TTS and goes to idle — before the transcript arrives.
///   When the transcript arrives via OnTranscriptReceived, the query is sent
///   and the thinking animation triggers as normal.
/// </summary>
[RequireComponent(typeof(AIResponseGenerator))]
[AddComponentMenu("AI/AI Tutor")]
public class AITutor : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("References")]
    [SerializeField] private TextToSpeechPlayer     ttsPlayer;
    [SerializeField] private TTSAnimatorDriver      animatorDriver;
    [SerializeField] private OpenAISpeechRecognizer speechRecognizer;
    [SerializeField] private GestureSynchronizer    gestureSynchronizer;

    [Header("RAG Settings")]
    [Tooltip("Path relative to Assets folder. e.g. 'Scripts/AI/knowledge_base'")]
    [SerializeField] private string knowledgeBaseFolder = "Scripts/AI/knowledge_base";
    [SerializeField] private int    ragTopK             = 3;
    [SerializeField] private int    ragMaxContextChars  = 2500;
    [SerializeField] private float  ragMinScore         = 0.08f;
    [SerializeField] private bool   logRagRetrieval     = true;

    [Header("Tutor Settings")]
    [Tooltip("Cooldown after a response completes before accepting the next transcript (seconds)")]
    [SerializeField] private float responseCooldownSec = 0.5f;

    [Tooltip("How long to wait for the interrupted coroutine to release the gate before forcing a new query (seconds)")]
    [SerializeField] private float interruptTimeoutSec = 1.5f;

    [Header("Events")]
    public UnityEvent<string> OnResponseStarted   = new UnityEvent<string>();
    public UnityEvent<string> OnResponseCompleted = new UnityEvent<string>();
    public UnityEvent<string> OnErrorOccurred     = new UnityEvent<string>();

    // ── Private state ─────────────────────────────────────────────────────────

    private AIResponseGenerator _generator;
    private RAGIndex            _ragIndex;
    private TutorSceneState     _sceneState;

    private bool  _isProcessing   = false;
    private bool  _interrupted    = false;
    private float _lastResponseAt = -999f;

    private bool _ttsBusy          = false;
    private int  _prefetchInFlight = 0;
    private int  _enqueueOrder     = 0;
    private int  _playbackOrder    = 0;

    // Keyed by enqueue order — drain loop waits for each key explicitly
    // so sentences always play in LLM stream order regardless of fetch speed
    private readonly SortedDictionary<int, AudioClip> _orderedClipQueue
        = new SortedDictionary<int, AudioClip>();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _generator  = GetComponent<AIResponseGenerator>();
        _sceneState = new TutorSceneState();

        if (ttsPlayer           == null) ttsPlayer           = FindAnyObjectByType<TextToSpeechPlayer>();
        if (animatorDriver      == null) animatorDriver      = FindAnyObjectByType<TTSAnimatorDriver>();
        if (speechRecognizer    == null) speechRecognizer    = FindAnyObjectByType<OpenAISpeechRecognizer>();
        if (gestureSynchronizer == null) gestureSynchronizer = FindAnyObjectByType<GestureSynchronizer>();

        BuildRAGIndex();
        StartCoroutine(WarmUpOnStart());
    }

    private void OnEnable()
    {
        if (speechRecognizer != null)
        {
            speechRecognizer.OnTranscriptReady += OnTranscriptReceived;
            speechRecognizer.OnSpeechStarted   += OnUserSpeechStarted;
        }
    }

    private void OnDisable()
    {
        if (speechRecognizer != null)
        {
            speechRecognizer.OnTranscriptReady -= OnTranscriptReceived;
            speechRecognizer.OnSpeechStarted   -= OnUserSpeechStarted;
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void RefreshSceneState()
    {
        _sceneState.Refresh();
        Debug.Log($"[AITutor] Scene state refreshed → {_sceneState}");
    }

    public TutorSceneState GetSceneState() => _sceneState;

    public void ProcessUserQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return;

        if (_isProcessing)
        {
            // TTS is already stopped by OnUserSpeechStarted — just queue the query
            Debug.Log("[AITutor] Queuing new query after speech-start interrupt.");
            StartCoroutine(InterruptThenQuery(query));
            return;
        }

        if (Time.realtimeSinceStartup - _lastResponseAt < responseCooldownSec)
        {
            Debug.Log("[AITutor] In cooldown — query ignored.");
            return;
        }

        StartCoroutine(RunQueryCoroutine(query));
    }

    public bool IsProcessing => _isProcessing;

    /// <summary>
    /// Immediately stops TTS playback and signals all coroutines to exit.
    /// Respects killAllTTSOnInterrupt on the TTS player.
    /// The processing gate is released by the finally block in RunQueryCoroutine.
    /// </summary>
    public void Interrupt()
    {
        if (!_isProcessing) return;

        Debug.Log("[AITutor] 🛑 Interrupt called.");
        _interrupted = true;

        // Stop audio — respect the killAllTTSOnInterrupt toggle
        if (ttsPlayer != null)
        {
            if (ttsPlayer.killAllTTSOnInterrupt)
                TextToSpeechPlayer.KillAllTTS();
            else
                ttsPlayer.StopSpeaking();
        }

        // Clear the clip queue so DrainTTSQueue exits on its next iteration
        _orderedClipQueue.Clear();
        _ttsBusy = false;
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Called as soon as VAD detects the user has started speaking.
    /// Stops TTS immediately and returns the animator to idle.
    /// Does NOT send a query yet — that happens when the transcript arrives.
    /// </summary>
    private void OnUserSpeechStarted()
    {
        if (!_isProcessing) return;

        Debug.Log("[AITutor] 🎤 Speech detected — stopping TTS immediately.");

        // Stop audio right away
        if (ttsPlayer != null)
        {
            if (ttsPlayer.killAllTTSOnInterrupt)
                TextToSpeechPlayer.KillAllTTS();
            else
                ttsPlayer.StopSpeaking();
        }

        // Cancel any pending thinking animation and return to idle
        animatorDriver?.CancelThinking();

        // Mark as interrupted so DrainTTSQueue and other coroutines bail out
        _interrupted = true;
        _orderedClipQueue.Clear();
        _ttsBusy = false;
    }

    private void OnTranscriptReceived(string transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript)) return;
        Debug.Log($"[AITutor] Transcript received: {transcript}");
        ProcessUserQuery(transcript);
    }

    /// <summary>
    /// Waits for the processing gate to release after an interrupt,
    /// then starts the new query.
    /// </summary>
    private IEnumerator InterruptThenQuery(string query)
    {
        // Wait for the processing gate to release (finally block in RunQueryCoroutine)
        float timeout = Time.realtimeSinceStartup + interruptTimeoutSec;
        while (_isProcessing && Time.realtimeSinceStartup < timeout)
            yield return null;

        if (_isProcessing)
        {
            Debug.LogWarning("[AITutor] Interrupt timeout — forcing gate release.");
            _isProcessing = false;
        }

        // Brief pause so the animator can settle before the thinking gesture
        yield return new WaitForSecondsRealtime(0.1f);

        Debug.Log($"[AITutor] Starting new query after interrupt: {query}");
        StartCoroutine(RunQueryCoroutine(query));
    }

    private IEnumerator RunQueryCoroutine(string query)
    {
        _isProcessing = true;
        _interrupted  = false;
        string fullResponse = "";
        bool   ttsCompleted = false;

        OnResponseStarted.Invoke(query);
        gestureSynchronizer?.OnResponseStart();
        animatorDriver?.TriggerThinking();

        try
        {
            // 0. Refresh scene state
            _sceneState.Refresh();

            // 1. RAG retrieval
            string ragQuery = $"{_sceneState.Phase} {query}";
            float  bestScore;
            string bestSource;
            _ragIndex.BuildContextPrompt(
                ragQuery, _sceneState.Phase, ragTopK, ragMaxContextChars,
                out bestScore, out bestSource);

            if (logRagRetrieval)
                Debug.Log($"[AITutor] RAG: query='{query}' bestScore={bestScore:0.000} source='{bestSource}'");

            if (bestScore < ragMinScore)
                Debug.LogWarning($"[AITutor] RAG confidence low ({bestScore:0.000} < {ragMinScore}).");

            List<RAGIndex.Hit> hits = _ragIndex.Retrieve(ragQuery, ragTopK);

            // 2. Protect mic from picking up TTS audio
            speechRecognizer?.NotifyTTSStarted();

            // 3. Stream response
            yield return _generator.GenerateStreamingResponse(
                query,
                hits,
                _sceneState,
                onSentenceReady: sentence =>
                {
                    if (_interrupted) return;

                    animatorDriver?.CancelThinking();
                    EnqueueTTS(sentence);
                    fullResponse += (fullResponse.Length > 0 ? " " : "") + sentence;

                    // Check for gesture keywords as text streams in
                    gestureSynchronizer?.ProcessResponse(fullResponse);
                },
                onComplete: full =>
                {
                    if (_interrupted) return;

                    if (string.IsNullOrWhiteSpace(fullResponse))
                        fullResponse = full;

                    // Final check on complete response in case keywords arrived late
                    gestureSynchronizer?.ProcessResponse(fullResponse);
                }
            );

            // 4. Wait for TTS to finish (skipped if interrupted)
            if (!_interrupted)
            {
                float ttsTimeout = Time.realtimeSinceStartup + 30f;
                while (_ttsBusy && Time.realtimeSinceStartup < ttsTimeout)
                {
                    if (_interrupted) break;
                    yield return null;
                }

                if (_ttsBusy && !_interrupted)
                    Debug.LogWarning("[AITutor] TTS drain timed out after 30s.");
            }

            ttsCompleted = !_interrupted;
        }
        finally
        {
            _isProcessing     = false;
            _ttsBusy          = false;
            _prefetchInFlight = 0;
            _enqueueOrder     = 0;
            _playbackOrder    = 0;
            _orderedClipQueue.Clear();
            _lastResponseAt   = Time.realtimeSinceStartup;

            if (_interrupted)
                Debug.Log("[AITutor] Response interrupted — gate released.");
            else
                Debug.Log("[AITutor] Processing gate released.");
        }

        if (ttsCompleted)
            OnResponseCompleted.Invoke(fullResponse);
    }

    // ── TTS sentence queue ────────────────────────────────────────────────────

    /// <summary>
    /// Assigns a sequential order number to each sentence as it arrives from
    /// the LLM stream, then kicks off a background TTS fetch.
    /// </summary>
    private void EnqueueTTS(string sentence)
    {
        if (string.IsNullOrWhiteSpace(sentence)) return;
        if (_interrupted) return;

        int order = _enqueueOrder++;
        StartCoroutine(PrefetchNext(sentence, order));
        if (!_ttsBusy)
        {
            _ttsBusy = true;
            StartCoroutine(DrainTTSQueue());
        }
    }

    /// <summary>
    /// Fetches TTS audio in the background. Stores the result keyed by its
    /// original order so DrainTTSQueue always plays in LLM stream order.
    /// </summary>
    private IEnumerator PrefetchNext(string sentence, int order)
    {
        if (ttsPlayer == null) yield break;
        _prefetchInFlight++;
        AudioClip clip = null;
        yield return ttsPlayer.FetchAudioClip(sentence, c => clip = c);
        _prefetchInFlight--;

        // Don't add to queue if interrupted — DrainTTSQueue may already be gone
        if (!_interrupted)
            _orderedClipQueue[order] = clip;
    }

    /// <summary>
    /// Sequential playback loop. Explicitly waits for each key in order
    /// (0, 1, 2...) so a fast-fetching sentence 2 never plays before
    /// a slow-fetching sentence 1. Keeps IsSpeaking true across all
    /// sentences so the animator never drops to idle between them.
    /// </summary>
    private IEnumerator DrainTTSQueue()
    {
        while (true)
        {
            // Exit immediately if interrupted
            if (_interrupted) goto done;

            if (_prefetchInFlight == 0 && !_orderedClipQueue.ContainsKey(_playbackOrder))
                break;

            float waitStart = Time.realtimeSinceStartup;
            while (!_orderedClipQueue.ContainsKey(_playbackOrder))
            {
                if (_interrupted) goto done;
                if (_prefetchInFlight == 0 && _orderedClipQueue.Count == 0)
                    goto done;

                if (Time.realtimeSinceStartup - waitStart > 10f)
                {
                    Debug.LogWarning($"[AITutor] Timed out waiting for clip {_playbackOrder} (10s).");
                    goto done;
                }
                yield return null;
            }

            if (_interrupted) goto done;

            AudioClip clip = _orderedClipQueue[_playbackOrder];
            _orderedClipQueue.Remove(_playbackOrder);
            _playbackOrder++;

            // Notify gesture synchronizer when audio actually starts playing
            bool isFirstClip = _playbackOrder == 1;

            bool playDone = false;
            if (clip != null && ttsPlayer != null)
            {
                ttsPlayer.PlayClip(clip, () => playDone = true);

                if (isFirstClip)
                    gestureSynchronizer?.OnAudioPlaybackStart();
            }
            else
            {
                playDone = true;
            }

            float playStart = Time.realtimeSinceStartup;
            while (!playDone)
            {
                if (_interrupted) goto done;

                if (_orderedClipQueue.ContainsKey(_playbackOrder) && ttsPlayer?.audioSource != null)
                {
                    float elapsed   = Time.realtimeSinceStartup - playStart;
                    float remaining = ttsPlayer.audioSource.clip != null
                        ? ttsPlayer.audioSource.clip.length - ttsPlayer.audioSource.time
                        : 0f;
                    if (elapsed > 0.3f && remaining < 0.12f) break;
                }

                if (Time.realtimeSinceStartup - playStart > 30f)
                {
                    Debug.LogWarning("[AITutor] Playback timed out (30s).");
                    break;
                }
                yield return null;
            }

            bool isLastClip = _prefetchInFlight == 0 && _orderedClipQueue.Count == 0;
            if (isLastClip)
            {
                if (ttsPlayer != null) ttsPlayer.IsSpeaking = false;
                Debug.Log("[AITutor] All sentences played — IsSpeaking = false.");
            }
        }

        done:
        if (ttsPlayer != null) ttsPlayer.IsSpeaking = false;
        _ttsBusy = false;
    }

    // ── RAG index setup ───────────────────────────────────────────────────────

    private void BuildRAGIndex()
    {
        _ragIndex = new RAGIndex { ChunkSize = 1500, ChunkOverlap = 80 };
        _ragIndex.Rebuild(ResolveKBPath());
    }

    private string ResolveKBPath()
    {
        string candidate = knowledgeBaseFolder ?? "";

        if (!Path.IsPathRooted(candidate))
            candidate = Path.GetFullPath(Path.Combine(Application.dataPath, candidate));

        if (Directory.Exists(candidate))
        {
            Debug.Log($"[AITutor] Knowledge base resolved: {candidate}");
            return candidate;
        }

        Debug.LogError(
            $"[AITutor] Knowledge base NOT found at: '{candidate}'\n" +
            $"Set 'Knowledge Base Folder' in the Inspector, e.g. 'Scripts/AI/knowledge_base'.");
        return candidate;
    }

    private IEnumerator WarmUpOnStart()
    {
        yield return new WaitForSeconds(2f);
        yield return _generator.WarmUp();
    }
}