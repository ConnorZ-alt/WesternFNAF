using TMPro;
using UnityEngine;

public class AmmoHUDDisplay : MonoBehaviour
{
    [SerializeField] private ItemController gun;   // drag your Revolver here
    [SerializeField] private TextMeshProUGUI ammoText;

    void Awake()
    {
        if (!ammoText)
            ammoText = GetComponent<TextMeshProUGUI>();
    }

    void OnEnable()
    {
        if (gun != null)
        {
            // Subscribe to the gun's ammo change event
            gun.OnAmmoChanged += HandleAmmoChanged;
        }
    }

    void OnDisable()
    {
        if (gun != null)
        {
            gun.OnAmmoChanged -= HandleAmmoChanged;
        }
    }

    void Start()
    {
        // Force initial display
        if (gun != null)
        {
            HandleAmmoChanged(gun.GetRoundsInCylinder(), gun.GetReserveAmmo());
        }
    }

    private void HandleAmmoChanged(int cyl, int reserve)
    {
        if (!ammoText) return;
        ammoText.text = "Ammo: " + cyl + "/" + gunCapacity + " | Reserve: " + reserve;
    }
    
    private int gunCapacity
    {
        get
        {
            return gun.GetTotalAmmoCapacity();
        }
    }
}