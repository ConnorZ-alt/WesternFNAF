using UnityEngine;
using System;
using UnityEngine.Serialization;

public class ItemController : MonoBehaviour
{
    [Header("Links")]
    [Tooltip("Player camera used for aiming/shooting raycasts")]
    [SerializeField] private Camera playerCamera;
    
    [Header("Revolver")]
    [Tooltip("Capacity of the cylinder")]
    [SerializeField] private int totalAmmoCapacity = 6;
    public int GetTotalAmmoCapacity() => totalAmmoCapacity;

    [Tooltip("Max shoot distance for raycast (debug only)")]
    [SerializeField] private float shotRaycastRange = 100f;

    [Tooltip("LayerMask for what the debug raycast can hit")]
    [SerializeField] private LayerMask shotHitMask = ~0;

    [Header("Input")]
    [SerializeField] private bool holdToAimDownSights = true;
    [SerializeField] private KeyCode aimKeyCode = KeyCode.Mouse1;   // RMB
    [SerializeField] private KeyCode shootKeyCode = KeyCode.Mouse0; // LMB
    [SerializeField] private KeyCode reloadKeyCode = KeyCode.R;

    [Header("Aiming Visual (optional)")]
    [SerializeField] private bool useAimFieldOfView = true;
    [SerializeField] private float normalFieldOfView = 60f;
    [SerializeField] private float aimFieldOfView = 45f;
    [SerializeField] private float fieldOfViewLerpSpeed = 10f;

    [Header("Ammo State (read-only at runtime)")]
    [SerializeField] private int roundsInCylinder = 0; // starts empty
    [SerializeField] private int reserveAmmo = 0;       // filled by pickups

    [Header("Bullet Visual")] 
    [SerializeField] private Transform muzzleTransform;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float bulletRecoilKickDistance = 0.03f;
    
    [Header("Damage")]
    [Tooltip("Damage dealt to IDamageable targets when a raycast shot hits")]
    [SerializeField] private float shotDamage = 25f;
    
    [SerializeField] private bool debugForceAim = false;
    private bool lastAimingState = false;

    [SerializeField] private bool externalShootBlockEnabled = false;

    public void SetExternalShootBlock(bool isBlocked)
    {
        externalShootBlockEnabled = isBlocked;
    }
    
    // Events (for HUD later)
    public event Action<int, int> OnAmmoChanged; // (roundsInCylinder, reserveAmmo)

    // Internals
    [SerializeField] private bool isAimingDownSights = false;
    public bool IsAiming => isAimingDownSights;

    void Awake()
    {
        if (!playerCamera)
        {
            playerCamera = GetComponentInParent<Camera>();
            if (!playerCamera) playerCamera = Camera.main;
        }

        // Ensure this never behaves like a loose physics item
        var rigidbodyComponent = GetComponent<Rigidbody>();
        if (rigidbodyComponent) rigidbodyComponent.isKinematic = true;
    }

    void Start()
    {
        if (playerCamera && useAimFieldOfView) playerCamera.fieldOfView = normalFieldOfView;
        RaiseAmmoChanged();
    }

    void Update()
    {
        HandleAimInput();
        HandleReloadInput();
        HandleShootInput();
        UpdateAimFieldOfView();
    }

    // ---------- Input ----------
    private void HandleAimInput()
    {
        if (!playerCamera) return;

        // Safety: drop ADS if empty
        if (roundsInCylinder <= 0 && isAimingDownSights)
        {
            isAimingDownSights = false;
            Debug.Log("[GUN] ADS off (empty).");
        }

        bool pressedAimThisFrame = Input.GetKeyDown(aimKeyCode);
        bool holdingAim          = Input.GetKey(aimKeyCode);

        if (holdToAimDownSights)
        {
            // HOLD-TO-AIM: active only while the key is held, and only if we have rounds
            bool newAim = holdingAim && roundsInCylinder > 0;
            if (newAim != isAimingDownSights)
            {
                isAimingDownSights = newAim;
                Debug.Log("[GUN] ADS " + (isAimingDownSights ? "ON" : "OFF") + " (hold mode)");
            }
        }
        else
        {
            // TOGGLE-TO-AIM: flip on press, but only allow turning on if we have rounds
            if (pressedAimThisFrame)
            {
                if (!isAimingDownSights && roundsInCylinder <= 0)
                {
                    // Can't enter ADS if empty
                    Debug.Log("[GUN] Ignored ADS toggle (no rounds).");
                }
                else
                {
                    isAimingDownSights = !isAimingDownSights;
                    Debug.Log("[GUN] ADS " + (isAimingDownSights ? "ON" : "OFF") + " (toggle mode)");
                }
            }
        }

        // Optional: force-aim debug overrides *only* the FOV later; do not change isAiming here.
    }


    private void HandleReloadInput()
    {
        if (Input.GetKeyDown(reloadKeyCode))
            Reload();
    }

    private void HandleShootInput()
    {
        if (externalShootBlockEnabled) return;
        if (Input.GetKeyDown(shootKeyCode))
            Shoot();
    }

    private void UpdateAimFieldOfView()
    {
        if (!playerCamera || !useAimFieldOfView) return;
        bool aimingForFieldOfView = debugForceAim ? true : isAimingDownSights;
        float targetFieldOfView = aimingForFieldOfView ? aimFieldOfView : normalFieldOfView;
        playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFieldOfView, Time.deltaTime * fieldOfViewLerpSpeed);
    }

    // ---------- Public API ----------
    public void AddAmmo(int amount)
    {
        if (amount <= 0) return;
        reserveAmmo += amount;
        Debug.Log("[GUN] Picked up ammo. Reserve now = " + reserveAmmo);
        RaiseAmmoChanged();
    }

    public void Reload()
    {
        int spaceInCylinder = totalAmmoCapacity - roundsInCylinder;
        if (spaceInCylinder <= 0)
        {
            Debug.Log("[GUN] Reload: cylinder already full.");
            return;
        }
        if (reserveAmmo <= 0)
        {
            Debug.Log("[GUN] Reload: no reserve ammo.");
            return;
        }

        int roundsToLoad = Mathf.Min(spaceInCylinder, reserveAmmo);
        roundsInCylinder += roundsToLoad;
        reserveAmmo -= roundsToLoad;

        Debug.Log("[GUN] Reloaded " + roundsToLoad + " rounds. Cylinder = " 
                  + roundsInCylinder + "/" + totalAmmoCapacity 
                  + " | Reserve = " + reserveAmmo);

        RaiseAmmoChanged();
    }

    public bool CanShoot() => roundsInCylinder > 0 && isAimingDownSights && !externalShootBlockEnabled;

    public void Shoot()
    {
        if (!CanShoot())
        {
            Debug.Log("[GUN] Tried to shoot but can't (no aim or no rounds).");
            return;
        }

        roundsInCylinder--;
        Debug.Log("[GUN] BANG. Cylinder now = " + roundsInCylinder + "/" + totalAmmoCapacity);

        RaiseAmmoChanged();

        // Visual for Bullet Projectile (optional)
        if (muzzleTransform && bulletPrefab)
        {
            GameObject spawnedBullet = Instantiate(bulletPrefab, muzzleTransform.position, muzzleTransform.rotation);
        }
        else
        {
            Debug.LogWarning("[GUN] No muzzleTransform or bulletPrefab assigned, can't spawn bullets.");
        }
        
        // Debug raycast damage
        if (playerCamera)
        {
            Ray shotRay = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            if (Physics.Raycast(shotRay, out RaycastHit raycastHit, shotRaycastRange, shotHitMask, QueryTriggerInteraction.Ignore))
            {
                var damageable = raycastHit.collider.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(shotDamage);
                }
            }
        }
        
        // Tiny recoil kick for the camera to shake
        if (playerCamera)
        {
            playerCamera.transform.localPosition += -playerCamera.transform.forward * bulletRecoilKickDistance;
        }

        if (roundsInCylinder == 0)
        {
            isAimingDownSights = false;
            Debug.Log("[GUN] Cylinder empty. Aim auto-canceled.");
        }
    }
    
    public int GetRoundsInCylinder() => roundsInCylinder;
    public int GetReserveAmmo() => reserveAmmo;

    // ---------- Helpers ----------
    private void RaiseAmmoChanged() => OnAmmoChanged?.Invoke(roundsInCylinder, reserveAmmo);
}
