using UnityEngine;

public class NarrationParticleBridge : MonoBehaviour
{
    [SerializeField] private AITutor aiTutor;
    [SerializeField] private CellParticleController particles;

    private bool _wasPaused = false;

    private void Awake()
    {
        if (aiTutor == null)  aiTutor    = FindAnyObjectByType<AITutor>();
        if (particles == null) particles = FindAnyObjectByType<CellParticleController>();
    }

    private void Update()
    {
        if (aiTutor == null || particles == null) return;

        bool shouldPause = aiTutor.IsNarrating;

        if (shouldPause && !_wasPaused)
        {
            particles.Pause();
            _wasPaused = true;
        }
        else if (!shouldPause && _wasPaused)
        {
            particles.Resume();
            _wasPaused = false;
        }
    }
}