using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class AmmoHUDDisplay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ItemController gun;          // Drag your Revolver (ItemController) here
    [SerializeField] private TextMeshProUGUI ammoText;    // Drag the TMP text here (or it will auto-find on this object)

    private void Awake()
    {
        // Awake runs when this object first turns on.
        // We use it to find the text component if the person forgot to drag it in.
        if (!ammoText)
            ammoText = GetComponent<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        // OnEnable runs every time this UI object becomes active.
        // We “subscribe” to the gun event so the HUD updates when ammo changes.
        if (gun != null)
        {
            gun.OnAmmoChanged += HandleAmmoChanged;
        }
        else
        {
            // If there is no gun reference, show something helpful instead of staying blank.
            SetTextSafe("Ammo: --");
        }
    }

    private void OnDisable()
    {
        // OnDisable runs when this UI object turns off.
        // We “unsubscribe” so we don’t keep listening after this object is gone.
        if (gun != null)
            gun.OnAmmoChanged -= HandleAmmoChanged;
    }

    private void Start()
    {
        // Start runs right after Awake, on the first frame.
        // We force one update so the HUD is correct even before the gun fires the event.
        RefreshDisplayFromGun();
    }

    /// <summary>
    /// Reads ammo info from the gun and updates the HUD text.
    /// </summary>
    private void RefreshDisplayFromGun()
    {
        if (gun == null)
        {
            SetTextSafe("Ammo: --");
            return;
        }

        int cylinder = gun.GetRoundsInCylinder();
        int reserve  = gun.GetReserveAmmo();
        int capacity = gun.GetTotalAmmoCapacity();

        UpdateAmmoText(cylinder, capacity, reserve);
    }

    /// <summary>
    /// This gets called automatically when the gun says “ammo changed”.
    /// The gun sends us the new cylinder and reserve values.
    /// </summary>
    private void HandleAmmoChanged(int cylinderRounds, int reserveRounds)
    {
        if (gun == null)
        {
            SetTextSafe("Ammo: --");
            return;
        }

        // Capacity is still read from the gun because it’s “gun settings”, not “current ammo”.
        int capacity = gun.GetTotalAmmoCapacity();
        UpdateAmmoText(cylinderRounds, capacity, reserveRounds);
    }

    /// <summary>
    /// Actually builds the string and puts it on the screen.
    /// Keeping this in one method makes it easier to change the HUD format later.
    /// </summary>
    private void UpdateAmmoText(int cylinderRounds, int cylinderCapacity, int reserveRounds)
    {
        SetTextSafe($"Ammo: {cylinderRounds}/{cylinderCapacity} | Reserve: {reserveRounds}");
    }

    /// <summary>
    /// Small helper so we never crash if the TextMeshPro reference is missing.
    /// </summary>
    private void SetTextSafe(string text)
    {
        if (!ammoText) return;
        ammoText.text = text;
    }
}
