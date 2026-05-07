using TMPro;
using UnityEngine;

/// Displays the number of ammo pickups remaining in the world (on the train)

[DisallowMultipleComponent]
public class WorldAmmoDisplay : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag the WorldAmmoTracker GameObject from the scene here.")]
    [SerializeField] private WorldAmmoTracker tracker;

    [Tooltip("Drag the TMP Text to write to. Leave empty to use the TMP Text on this GameObject.")]
    [SerializeField] private TextMeshProUGUI displayText;

    [Header("Display Format")]
    [Tooltip("Label shown before the number. Example: 'Ammo on Train: '")]
    [SerializeField] private string labelPrefix = "Ammo on Train: ";

    private void Awake()
    {
        // Auto-find TMP text on this object only — tracker must always be manually wired.
        if (!displayText)
            displayText = GetComponent<TextMeshProUGUI>();

        if (!displayText)
            Debug.LogError("[WorldAmmoDisplay] No TextMeshProUGUI found. Attach one to this GameObject or wire the Display Text slot in the Inspector.");

        if (!tracker)
            Debug.LogError("[WorldAmmoDisplay] No WorldAmmoTracker wired. Drag the WorldAmmoTracker scene object into the Tracker slot in the Inspector.");
    }

    private void OnEnable()
    {
        if (tracker == null) return;

        tracker.OnWorldAmmoChanged += HandleWorldAmmoChanged;

        // Force an immediate refresh so the display is correct the moment it turns on.
        SetText(tracker.WorldAmmoCount);
    }

    private void OnDisable()
    {
        if (tracker == null) return;

        tracker.OnWorldAmmoChanged -= HandleWorldAmmoChanged;
    }

    private void HandleWorldAmmoChanged(int newCount)
    {
        SetText(newCount);
    }

    private void SetText(int count)
    {
        if (displayText)
            displayText.text = labelPrefix + count;
    }
}