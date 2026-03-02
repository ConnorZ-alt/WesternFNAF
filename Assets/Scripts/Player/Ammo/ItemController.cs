using System;
using UnityEngine;
using System.Collections;

[DisallowMultipleComponent]
public class ItemController : MonoBehaviour
{
    [Header("Links")]
    [Tooltip("Player camera used for aiming/shooting raycasts")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private PlayerCoalThrower playerCoalThrower;

    [Header("Revolver Ammo")]
    [Tooltip("How many bullets the cylinder can hold")]
    [SerializeField] private int totalAmmoCapacity = 6;

    [Tooltip("How far the hit-scan ray can go")]
    [SerializeField] private float shotRaycastRange = 100f;

    [Tooltip("What layers the raycast is allowed to hit")]
    [SerializeField] private LayerMask shotHitMask = ~0;

    [Header("Input")]
    [SerializeField] private bool holdToAimDownSights = true;
    [SerializeField] private KeyCode aimKeyCode    = KeyCode.Mouse1; // RMB
    [SerializeField] private KeyCode shootKeyCode  = KeyCode.Mouse0; // LMB
    [SerializeField] private KeyCode reloadKeyCode = KeyCode.R;

    [Header("Aiming Visual (optional)")]
    [SerializeField] private bool  useAimFieldOfView = true;
    [SerializeField] private float normalFieldOfView = 60f;
    [SerializeField] private float aimFieldOfView    = 45f;
    [SerializeField] private float fieldOfViewLerpSpeed = 10f;

    [Header("Runtime Ammo (read-only while playing)")]
    [SerializeField] private int roundsInCylinder = 0; // starts empty
    [SerializeField] private int reserveAmmo      = 0; // filled by pickups

    [Header("Bullet Visual (optional)")]
    [SerializeField] private Transform muzzleTransform;
    [SerializeField] private GameObject bulletPrefab;

    [Tooltip("Tiny camera kick back when shooting (just a small effect)")]
    [SerializeField] private float bulletRecoilKickDistance = 0.03f;
    
    [Tooltip("Cooldown before the gun can fire again")]
    [SerializeField] private float cooldownlength = 0.5f;

    private bool isOnCooldown = false;
    private bool isHoldingCoal = false;

    [Header("Damage")]
    [Tooltip("Damage dealt to IDamageable targets when raycast hits")]
    [SerializeField] private float shotDamage = 25f;

    [Header("Debug")]
    [SerializeField] private bool debugForceAim = false;

    // ===================== Events =====================

    /// <summary>
    /// Other scripts (like the HUD) can listen to this to update ammo UI.
    /// </summary>
    public event Action<int, int> OnAmmoChanged; // (roundsInCylinder, reserveAmmo)

    // ===================== Internal State =====================

    [SerializeField] private bool isAimingDownSights = false;

    // This lets other scripts block shooting (ex: throwing coal so Mouse0 doesn’t also fire).
    [SerializeField] private bool externalShootBlockEnabled = false;

    public bool IsAiming => isAimingDownSights;

    private void Awake()
    {
        // Awake runs when the object is created.
        // We use it to find our camera and make sure the gun isn’t acting like a physics object.

        ResolveCameraReference();
        MakeSelfNotAPhysicsItem();
    }

    private void Start()
    {
        // Start runs once right after Awake (first frame).
        // We set the starting FOV and tell the HUD our ammo numbers.

        if (playerCamera && useAimFieldOfView)
            playerCamera.fieldOfView = normalFieldOfView;

        RaiseAmmoChanged();
    }

    private void OnEnable()
    {
        if (playerCoalThrower == null)
            playerCoalThrower = FindObjectOfType<PlayerCoalThrower>();
        
        playerCoalThrower.CoalPickedUp += CoalPickedUp;
        playerCoalThrower.CoalThrown   += CoalThrown;
    }
    
    private void OnDisable()
    {
        if (playerCoalThrower != null)
        {
            playerCoalThrower.CoalPickedUp -= CoalPickedUp;
            playerCoalThrower.CoalThrown   -= CoalThrown;
        }
    }

    private void Update()
    {
        // Update runs every frame.
        // This is where we read player input and do actions.

        HandleAimInput();
        HandleReloadInput();
        HandleShootInput();
        UpdateAimFieldOfView();
    }

    // ===================== Public API =====================

    public int GetTotalAmmoCapacity() => totalAmmoCapacity;

    public int GetRoundsInCylinder() => roundsInCylinder;
    public int GetReserveAmmo() => reserveAmmo;

    public void SetExternalShootBlock(bool isBlocked)
    {
        // Another script can call this to temporarily prevent shooting.
        // Example: coal thrower blocks gun so Mouse0 doesn’t do two actions in one click.
        externalShootBlockEnabled = isBlocked;
    }

    public void AddAmmo(int amount)
    {
        // This is called by ammo pickups.
        if (amount <= 0) return;

        reserveAmmo += amount;
        Debug.Log("[GUN] Picked up ammo. Reserve now = " + reserveAmmo);

        RaiseAmmoChanged();
    }

    public void Reload()
    {
        // Reload moves bullets from reserve into the cylinder.

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

        Debug.Log("[GUN] Reloaded " + roundsToLoad +
                  " | Cylinder = " + roundsInCylinder + "/" + totalAmmoCapacity +
                  " | Reserve = " + reserveAmmo);

        RaiseAmmoChanged();
    }

    public bool CanShoot()
    {
        // You can only shoot if:
        // 1) You have bullets loaded,
        // 2) You are aiming,
        // 3) Nobody blocked shooting this frame.
        return roundsInCylinder > 0 && isAimingDownSights && !externalShootBlockEnabled && !isOnCooldown && !isHoldingCoal;
    }

    public void Shoot()
    {
        // Shoot does 3 main things:
        // - spends ammo
        // - spawns bullet VFX (optional)
        // - raycasts to deal damage (hitscan)

        if (!CanShoot())
        {
            Debug.Log("[GUN] Tried to shoot but can't (no aim or no rounds, or blocked).");
            return;
        }

        SpendOneRound();
        SpawnBulletVisual();
        DoRaycastDamage();
        ApplyCameraRecoilKick();
        StartCoroutine(StartCooldown());

        // If we just used the last bullet, auto-cancel aiming.
        if (roundsInCylinder == 0)
        {
            isAimingDownSights = false;
            Debug.Log("[GUN] Cylinder empty. Aim auto-canceled.");
        } 
    }
    
    // ===================== Cooldown =====================

    private IEnumerator StartCooldown()
    {
        isOnCooldown = true;
        yield return new WaitForSeconds(cooldownlength);
        isOnCooldown = false;
    }
    
    // ===================== Coal =====================
    private void CoalPickedUp(GameObject coal)
    {
        isHoldingCoal = true;
    }

    private void CoalThrown(Vector3 start, Vector3 target)
    {
        isHoldingCoal = false;
    }

    // ===================== Input Handling =====================

    private void HandleAimInput()
    {
        // This reads the aim key and decides if aiming should be on/off.

        if (!playerCamera) return;

        // Safety: if you run out of bullets, aiming turns off.
        if (roundsInCylinder <= 0 && isAimingDownSights)
        {
            isAimingDownSights = false;
            Debug.Log("[GUN] ADS off (empty).");
            return;
        }

        bool pressedAimThisFrame = Input.GetKeyDown(aimKeyCode);
        bool holdingAim          = Input.GetKey(aimKeyCode);

        if (holdToAimDownSights)
        {
            // HOLD mode: aiming is only true while you hold RMB.
            SetAimingState(holdingAim && roundsInCylinder > 0, "hold mode");
        }
        else
        {
            // TOGGLE mode: pressing RMB flips aim on/off.
            if (pressedAimThisFrame)
            {
                if (!isAimingDownSights && roundsInCylinder <= 0)
                {
                    Debug.Log("[GUN] Ignored ADS toggle (no rounds).");
                }
                else
                {
                    SetAimingState(!isAimingDownSights, "toggle mode");
                }
            }
        }
    }

    private void HandleReloadInput()
    {
        // If player presses R, try to reload.
        if (Input.GetKeyDown(reloadKeyCode))
            Reload();
    }

    private void HandleShootInput()
    {
        // If a different script blocked shooting, do nothing.
        if (externalShootBlockEnabled) return;

        // Shoot on click.
        if (Input.GetKeyDown(shootKeyCode))
            Shoot();
    }

    private void UpdateAimFieldOfView()
    {
        // This smoothly zooms the camera in/out while aiming.

        if (!playerCamera || !useAimFieldOfView) return;

        bool aimingForFieldOfView = debugForceAim ? true : isAimingDownSights;
        float targetFov = aimingForFieldOfView ? aimFieldOfView : normalFieldOfView;

        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView,
            targetFov,
            Time.deltaTime * fieldOfViewLerpSpeed
        );
    }

    // ===================== Helpers =====================

    private void ResolveCameraReference()
    {
        // Try to find the camera in a few reasonable places.
        if (!playerCamera)
        {
            playerCamera = GetComponentInParent<Camera>();
            if (!playerCamera) playerCamera = Camera.main;
        }

        if (!playerCamera)
            Debug.LogWarning("[GUN] No playerCamera found. Aiming and raycasts may not work.");
    }

    private void MakeSelfNotAPhysicsItem()
    {
        // If the gun has a Rigidbody (like if it’s on a pickup prefab),
        // force it to be kinematic so it doesn't fall over or bounce around.
        var rigidbodyComponent = GetComponent<Rigidbody>();
        if (rigidbodyComponent) rigidbodyComponent.isKinematic = true;
    }

    private void SetAimingState(bool newAimingState, string modeLabel)
    {
        // Only log and change if the state actually changed.
        if (newAimingState == isAimingDownSights) return;

        isAimingDownSights = newAimingState;
        Debug.Log("[GUN] ADS " + (isAimingDownSights ? "ON" : "OFF") + " (" + modeLabel + ")");
    }

    private void SpendOneRound()
    {
        roundsInCylinder = Mathf.Max(0, roundsInCylinder - 1);
        Debug.Log("[GUN] BANG. Cylinder now = " + roundsInCylinder + "/" + totalAmmoCapacity);
        RaiseAmmoChanged();
    }

    private void SpawnBulletVisual()
    {
        // This is just a visual bullet object (optional).
        // The real damage is the raycast below.
        if (!muzzleTransform || !bulletPrefab)
        {
            Debug.LogWarning("[GUN] No muzzleTransform or bulletPrefab assigned, can't spawn bullet visuals.");
            return;
        }

        Instantiate(bulletPrefab, muzzleTransform.position, muzzleTransform.rotation);
    }

    private void DoRaycastDamage()
    {
        // Raycast is like an invisible laser line.
        // If it hits something damageable, we deal damage instantly.

        if (!playerCamera) return;

        Ray shotRay = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (Physics.Raycast(shotRay, out RaycastHit hitInfo, shotRaycastRange, shotHitMask, QueryTriggerInteraction.Ignore))
        {
            var damageable = hitInfo.collider.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(shotDamage);
            }
        }
    }

    private void ApplyCameraRecoilKick()
    {
        // This is a tiny camera “kick” back. It’s super simple recoil.
        // NOTE: This can drift the camera over time if you do it repeatedly.
        // A better version would use an animation or a spring recoil.
        if (!playerCamera) return;

        playerCamera.transform.localPosition += -playerCamera.transform.forward * bulletRecoilKickDistance;
    }

    private void RaiseAmmoChanged()
    {
        // This tells the UI (and anything else listening) that ammo changed.
        OnAmmoChanged?.Invoke(roundsInCylinder, reserveAmmo);
    }
}
