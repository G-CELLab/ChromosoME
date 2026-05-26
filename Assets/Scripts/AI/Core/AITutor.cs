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

    [Header("Events")]
    public UnityEvent<string> OnResponseStarted   = new UnityEvent<string>();
    public UnityEvent<string> OnResponseCompleted = new UnityEvent<string>();
    public UnityEvent<string> OnErrorOccurred     = new UnityEvent<string>();

    // ── Private state ─────────────────────────────────────────────────────────

    private AIResponseGenerator _generator;
    private RAGIndex            _ragIndex;
    private TutorSceneState     _sceneState;

    private bool  _isProcessing   = false;
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
            speechRecognizer.OnTranscriptReady += OnTranscriptReceived;
    }

    private void OnDisable()
    {
        if (speechRecognizer != null)
            speechRecognizer.OnTranscriptReady -= OnTranscriptReceived;
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
            Debug.LogWarning("[AITutor] Already processing — query ignored.");
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

    // ── Internal ──────────────────────────────────────────────────────────────

    private void OnTranscriptReceived(string transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript)) return;
        Debug.Log($"[AITutor] Transcript received: {transcript}");
        ProcessUserQuery(transcript);
    }

    private IEnumerator RunQueryCoroutine(string query)
    {
        _isProcessing = true;
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
                    animatorDriver?.CancelThinking();
                    EnqueueTTS(sentence);
                    fullResponse += (fullResponse.Length > 0 ? " " : "") + sentence;

                    // Check for gesture keywords as text streams in
                    gestureSynchronizer?.ProcessResponse(fullResponse);
                },
                onComplete: full =>
                {
                    if (string.IsNullOrWhiteSpace(fullResponse))
                        fullResponse = full;

                    // Final check on complete response in case keywords arrived late
                    gestureSynchronizer?.ProcessResponse(fullResponse);
                }
            );

            // 4. Wait for TTS to finish
            float ttsTimeout = Time.realtimeSinceStartup + 30f;
            while (_ttsBusy && Time.realtimeSinceStartup < ttsTimeout)
                yield return null;

            if (_ttsBusy)
                Debug.LogWarning("[AITutor] TTS drain timed out after 30s.");

            ttsCompleted = true;
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
            if (_prefetchInFlight == 0 && !_orderedClipQueue.ContainsKey(_playbackOrder))
                break;

            float waitStart = Time.realtimeSinceStartup;
            while (!_orderedClipQueue.ContainsKey(_playbackOrder))
            {
                if (_prefetchInFlight == 0 && _orderedClipQueue.Count == 0)
                    goto done;

                if (Time.realtimeSinceStartup - waitStart > 10f)
                {
                    Debug.LogWarning($"[AITutor] Timed out waiting for clip {_playbackOrder} (10s).");
                    goto done;
                }
                yield return null;
            }

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