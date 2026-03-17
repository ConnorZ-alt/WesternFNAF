using TMPro;
using UnityEngine;

/// Displays the number of ammo pickups remaining in the world (on the train)
/// Wire up to a TMP text element in the Inspector
/// Separate from the cylinder/reserve HUD ("Bullets on Train: X" counter)

[DisallowMultipleComponent]
public class WorldAmmoDisplay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI displayText;

    [Header("Display Format")]
    [Tooltip("Label shown before the number. Example: 'Ammo on Train: '")]
    [SerializeField] private string labelPrefix = "Ammo on Train: ";

    private void Awake()
    {
        // Auto-find TMP text on this object if not manually wired.
        if (!displayText)
            displayText = GetComponent<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        if (WorldAmmoTracker.Instance != null)
        {
            WorldAmmoTracker.Instance.OnWorldAmmoChanged += HandleWorldAmmoChanged;
            // Force an immediate refresh so the display is correct on enable.
            SetText(WorldAmmoTracker.Instance.WorldAmmoCount);
        }
        else
        {
            SetTextSafe(labelPrefix + "--");
            Debug.LogWarning("[WorldAmmoDisplay] No WorldAmmoTracker found in scene. Add it to the scene.");
        }
    }

    private void OnDisable()
    {
        if (WorldAmmoTracker.Instance != null)
            WorldAmmoTracker.Instance.OnWorldAmmoChanged -= HandleWorldAmmoChanged;
    }

    private void HandleWorldAmmoChanged(int newCount)
    {
        SetText(newCount);
    }

    private void SetText(int count)
    {
        SetTextSafe(labelPrefix + count);
    }

    private void SetTextSafe(string value)
    {
        if (displayText)
            displayText.text = value;
    }
}