using UnityEngine;

public class FlickerLight : MonoBehaviour
{
    [Header("Flicker Settings")]
    public float minIntensity = 0f;
    public float maxIntensity = 25.0f;
    public float flickerSpeed = 0.05f;   // how often it changes
    public float smoothing = 8f;         // higher = snappier

    private Light _light;
    private float _targetIntensity;
    private float _timer;

    void Awake() => _light = GetComponent<Light>();

    void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer <= 0f)
        {
            // Weighted so it spends more time "mostly on" with occasional dips
            _targetIntensity = Random.value < 0.15f
                ? Random.Range(minIntensity, maxIntensity * 0.3f)   // sudden dip
                : Random.Range(maxIntensity * 0.6f, maxIntensity);  // mostly bright
            _timer = flickerSpeed + Random.Range(0f, flickerSpeed);
        }
        _light.intensity = Mathf.Lerp(_light.intensity, _targetIntensity, Time.deltaTime * smoothing);
    }
}