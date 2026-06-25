using UnityEngine;

public class CellParticleController : MonoBehaviour
{
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