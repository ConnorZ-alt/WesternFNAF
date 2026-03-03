using UnityEngine;

/// <summary>
/// Flickers a Light component between min and max intensity.
/// Transitions are eased (smooth start and end) with randomized speed and interval.
/// </summary>
[RequireComponent(typeof(Light))]
public class FlickerLight : MonoBehaviour
{
    [Header("Intensity Range")]
    public float minIntensity = 0f;
    public float maxIntensity = 1.8f;

    [Header("Transition Speed")]
    [Tooltip("Slowest transition to a new target (seconds)")]
    public float minTransitionTime = 0.05f;
    [Tooltip("Fastest transition to a new target (seconds)")]
    public float maxTransitionTime = 0.4f;

    [Header("Interval Between Changes")]
    [Tooltip("Shortest time to hold a value before picking a new one (seconds)")]
    public float minInterval = 0.05f;
    [Tooltip("Longest time to hold a value before picking a new one (seconds)")]
    public float maxInterval = 0.5f;

    private Light _light;
    private float _startIntensity;   // intensity at the beginning of this transition
    private float _targetIntensity;
    private float _transitionTime;
    private float _transitionTimer;
    private float _holdTimer;
    private bool _transitioning;

    private void Awake()
    {
        _light = GetComponent<Light>();
        _startIntensity = _light.intensity;
        _targetIntensity = _startIntensity;
        PickNextTarget();
    }

    private void Update()
    {
        if (_transitioning)
        {
            _transitionTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_transitionTimer / _transitionTime);

            // SmoothStep applies an S-curve to t:
            // starts slow, accelerates through the middle, slows again at the end.
            // This gives a natural ease-in/ease-out feel rather than a linear snap.
            float easedT = Mathf.SmoothStep(0f, 1f, t);

            _light.intensity = Mathf.Lerp(_startIntensity, _targetIntensity, easedT);

            if (_transitionTimer >= _transitionTime)
            {
                _light.intensity = _targetIntensity;
                _startIntensity = _targetIntensity;
                _transitioning = false;
                _holdTimer = Random.Range(minInterval, maxInterval);
            }
        }
        else
        {
            _holdTimer -= Time.deltaTime;
            if (_holdTimer <= 0f)
                PickNextTarget();
        }
    }

    private void PickNextTarget()
    {
        float roll = Random.value;
        if (roll < 0.3f)
            _targetIntensity = minIntensity;                             // 30%: full off
        else if (roll < 0.6f)
            _targetIntensity = maxIntensity;                             // 30%: full on
        else
            _targetIntensity = Random.Range(minIntensity, maxIntensity); // 40%: somewhere between

        _startIntensity = _light.intensity; // ease FROM wherever we currently are
        _transitionTime = Random.Range(minTransitionTime, maxTransitionTime);
        _transitionTimer = 0f;
        _transitioning = true;
    }
}