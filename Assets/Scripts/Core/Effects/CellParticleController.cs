using UnityEngine;

public class CellParticleController : MonoBehaviour
{
    [Header("Pause Settings")]
    [SerializeField] private float pauseSpeedMultiplier = 0f; // 0 = full freeze

    private ParticleSystem _ps;
    private bool _paused = false;

    private void Awake()
    {
        _ps = GetComponent<ParticleSystem>();
    }

    public void Pause()
    {
        if (_paused || _ps == null) return;
        _paused = true;
        _ps.Pause();
    }

    public void Resume()
    {
        if (!_paused || _ps == null) return;
        _paused = false;
        _ps.Play();
    }
}