using System;
using UnityEngine;
using System.Collections;
using Random = UnityEngine.Random;

public class DynamiteBandit : MonoBehaviour, IDamageable
{
    [Header("References")]
    [SerializeField] private Transform   trainRootTransform;      // drag Train root
    [SerializeField] private BoxCollider trainDeckBounds;         // Train/PlayerBounds
    [SerializeField] private float       deckYOffsetMeters = 0.0f;
    [SerializeField] private bool        usePhysics = false;
    [SerializeField] private BanditStats banditStats;             // use your BanditStats asset
    [SerializeField] private GameObject  dynamitePrefab;
    [SerializeField] private Transform   throwOriginTransform;    // auto-wired if null
    [SerializeField] private LayerMask   trainFloorMask;
    [SerializeField] private float       throwCooldownSeconds = 15f;

    [Header("Spawn/Behavior")]
    [SerializeField] private bool  spawnOnRight = true;
    [SerializeField] private bool  autoRespawnEnabled = true;     // spawner controls respawn timing; this just indicates "finish" behavior
    [SerializeField] private float spawnYOffsetMeters = 1.0f;

    [Header("Life")]
    [SerializeField] private float maxHealth = 50f;
    [SerializeField] private bool  destroyOnFinish = true;        // true: Destroy(gameObject) when finished; false: SetActive(false)

    // DEBUG
    [SerializeField] private bool debugPaceForever = true;
    [SerializeField] private bool skipApproach     = false;

    private GameObject          activeDynamiteGameObject;
    private DynamiteProjectile  activeProjectile;                 // for unsubscribing
    private float               nextThrowTimeSeconds;
    private Vector3             lastTrainWorldPosition;
    private Vector3             trainVelocity;

    // life/finish flags
    private float currentHealth;
    private bool  finishedNotified;
    private bool  shuttingDown;                                   // avoid work during teardown
    private bool  setupReceived;
    private bool  readySignalled;

    public Action onFinished;
    public event Action SignalReady;

    // ---------- Auto-wire helpers ----------
    private bool TryAutoWireThrowOrigin()
    {
        if (throwOriginTransform) return true;

        // Common child names people use
        throwOriginTransform =
            transform.Find("ThrowOrigin") ??
            transform.Find("Throw Origin") ??
            transform.Find("ThrowPoint") ??
            transform.Find("Throw Point");

        if (!throwOriginTransform)
        {
            // fallback: first child named like "Hand" or "RightHand" if present
            throwOriginTransform =
                transform.Find("Hand") ??
                transform.Find("RightHand");
        }

        return throwOriginTransform != null;
    }

    // ---------- Unity ----------
    private void Awake()
    {
        // Try to auto-wire as early as possible (covers prefab spawn paths)
        TryAutoWireThrowOrigin();
    }

    private IEnumerator Start()
    {
        // Try again in case hierarchy was modified after Awake()
        TryAutoWireThrowOrigin();

        if (!ValidateSetup())
        {
            // If setup wasn’t injected yet, wait a couple frames (script-order/prefab timing edge cases)
            int framesWaited = 0;
            while (!setupReceived && framesWaited < 3)
            {
                yield return null;
                framesWaited++;
            }

            // One last attempt to auto-wire before giving up
            TryAutoWireThrowOrigin();

            if (!setupReceived || !ValidateSetup())
            {
                Debug.LogError("[Bandit] Setup missing; disabling component.");
                enabled = false;
                yield break;
            }
        }

        currentHealth = Mathf.Max(1f, maxHealth);
        lastTrainWorldPosition = trainRootTransform ? trainRootTransform.position : transform.position;

        if (!readySignalled)
        {
            readySignalled = true;
            SignalReady?.Invoke();
        }

        yield return StartCoroutine(StateLoop());
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            TryThrowDynamiteOnce();
        }
    }

    private void OnDisable()
    {
        if (shuttingDown) return;
        Debug.Log("[Bandit] OnDisable()");
        shuttingDown = true;
        StopAllCoroutines();
        UnsubscribeProjectile();
        NotifyFinishedOnce();
    }

    private void OnDestroy()
    {
        // Extra safety: if destroyed directly, ensure we clean up & notify once.
        Debug.Log("[Bandit] OnDestroy()");
        NotifyFinishedOnce();
        UnsubscribeProjectile();
    }

    // ---------- Public finisher (idempotent) ----------
    public void SignalFinishedOnce()
    {
        NotifyFinishedOnce();
    }

    // ---------- Finish / Notify ----------
    private void NotifyFinishedOnce()
    {
        if (finishedNotified) return;
        finishedNotified = true;
        Debug.Log("[Bandit] Notifying spawner (onFinished).");
        onFinished?.Invoke();
    }

    private void FinishAndFreeSlot()
    {
        if (shuttingDown) return;
        shuttingDown = true;

        Debug.Log("[Bandit] FinishAndFreeSlot → stopping coroutines, clearing projectile, then destroy/disable.");
        NotifyFinishedOnce();
        StopAllCoroutines();
        UnsubscribeProjectile();

        if (destroyOnFinish)
        {
            Debug.Log("[Bandit] Destroy(gameObject).");
            Destroy(gameObject);
        }
        else
        {
            Debug.Log("[Bandit] SetActive(false).");
            gameObject.SetActive(false);
        }
    }

    // ---------- IDamageable ----------
    public void TakeDamage(float damage)
    {
        if (damage <= 0f || shuttingDown) return;

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        Debug.Log($"[Bandit] Took {damage} (now {currentHealth}/{maxHealth})");
        if (currentHealth <= 0f)
        {
            FinishAndFreeSlot();
        }
    }

    public void Kill()
    {
        if (shuttingDown) return;
        Debug.Log("[Bandit] Kill() called.");
        currentHealth = 0f;
        FinishAndFreeSlot();
    }

    // ---------- Y / deck helpers ----------
    private float GetDeckYPosition() =>
        trainDeckBounds ? trainDeckBounds.bounds.min.y + deckYOffsetMeters : transform.position.y;

    private void SnapPositionYToDeck(ref Vector3 worldPosition)
    {
        worldPosition.y = GetDeckYPosition();
    }

    private void ForceTransformYToDeck()
    {
        var position = transform.position;
        SnapPositionYToDeck(ref position);
        transform.position = position;
    }

    // ---------- State machine ----------
    private IEnumerator StateLoop()
    {
        if (!skipApproach)
            yield return StartCoroutine(ApproachAlongside());

        nextThrowTimeSeconds = Time.time + 0.75f; // short delay before first throw

        while (!shuttingDown)
        {
            PaceBesideTrain();

            // Train velocity for leading throws
            if (trainRootTransform)
            {
                float dt = Mathf.Max(Time.deltaTime, 0.0001f);
                Vector3 current = trainRootTransform.position;
                trainVelocity          = (current - lastTrainWorldPosition) / dt;
                lastTrainWorldPosition = current;
            }
            else
            {
                trainVelocity = Vector3.zero;
            }

            // Throw on cooldown if nothing active
            if (activeDynamiteGameObject == null && Time.time >= nextThrowTimeSeconds)
            {
                bool threw = TryThrowDynamiteOnce();
                nextThrowTimeSeconds = Time.time + (threw ? throwCooldownSeconds : 1.0f);
            }

            yield return null;
        }
    }

    private IEnumerator ApproachAlongside()
    {
        while (!shuttingDown)
        {
            Vector3 targetWorldPosition = GetDesiredAlongsideWorldPosition();
            transform.position = Vector3.MoveTowards(transform.position, targetWorldPosition, banditStats.moveSpeed * Time.deltaTime);
            ForceTransformYToDeck();
            FaceTrain();
            if (Vector3.Distance(transform.position, targetWorldPosition) < 0.2f) break;
            yield return null;
        }
    }

    private void PaceBesideTrain()
    {
        Vector3 targetWorldPosition = GetDesiredAlongsideWorldPosition();
        transform.position = Vector3.Lerp(transform.position, targetWorldPosition, banditStats.acceleration * Time.deltaTime);
        ForceTransformYToDeck();
        FaceTrain();
    }

    private IEnumerator RetreatAndWait()
    {
        float elapsedSeconds = 0f;
        Vector3 retreatDirection = (transform.position - trainRootTransform.position).normalized;
        while (elapsedSeconds < 2.5f && !shuttingDown)
        {
            transform.position += retreatDirection * (banditStats.moveSpeed * 1.3f * Time.deltaTime);
            ForceTransformYToDeck();
            elapsedSeconds += Time.deltaTime;
            yield return null;
        }

        FinishAndFreeSlot();
    }

    private Vector3 GetDesiredAlongsideWorldPosition()
    {
        float sideSign = spawnOnRight ? +1f : -1f;
        Vector3 localPoint = new Vector3(banditStats.lateralOffset * sideSign, 0f, -banditStats.followDistanceBack);
        Vector3 worldPoint = trainRootTransform.TransformPoint(localPoint);
        worldPoint.y = GetDeckYPosition();
        return worldPoint;
    }

    private void FaceTrain()
    {
        Vector3 toTrainVector = trainRootTransform.position - transform.position;
        toTrainVector.y = 0f;
        if (toTrainVector.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(toTrainVector, Vector3.up),
                Time.deltaTime * 6f
            );
        }
    }

    // ---------- Throwing ----------
    private bool TryThrowDynamiteOnce()
    {
        if (activeDynamiteGameObject != null || shuttingDown) return false;
        if (!dynamitePrefab || !throwOriginTransform || !trainDeckBounds)
        {
            Debug.LogWarning("[Bandit] Missing prefab/origin/bounds, abort throw.");
            return false;
        }

        // 1) Choose a landing point on TrainFloor by raycasting down from above the deck
        Vector3 pickOriginAboveDeck = trainDeckBounds.bounds.center + Vector3.up * 8f;
        pickOriginAboveDeck += new Vector3(
            Random.Range(-banditStats.throwInaccuracy, banditStats.throwInaccuracy),
            0f,
            Random.Range(-banditStats.throwInaccuracy, banditStats.throwInaccuracy)
        );

        Vector3 landingPoint;
        if (Physics.Raycast(pickOriginAboveDeck, Vector3.down, out RaycastHit floorHitInfo, 30f, trainFloorMask, QueryTriggerInteraction.Ignore))
        {
            landingPoint = floorHitInfo.point + Vector3.up * 0.02f;
        }
        else
        {
            landingPoint = new Vector3(
                trainDeckBounds.bounds.center.x,
                trainDeckBounds.bounds.min.y + 0.02f,
                trainDeckBounds.bounds.center.z
            );
        }

        // 2) Ballistic initial velocity to 'landingPoint'
        float   arcApexHeightY = Mathf.Max(landingPoint.y, throwOriginTransform.position.y) + 1.2f;
        Vector3 initialVelocity = ComputeBallisticVelocityViaApex(throwOriginTransform.position, landingPoint, arcApexHeightY);

        // 3) Spawn projectile, set velocity once, add train velocity to lead the moving deck
        GameObject instantiatedDynamite = Instantiate(dynamitePrefab, throwOriginTransform.position, Quaternion.identity);
        instantiatedDynamite.transform.parent = null;

        var rigidbody = instantiatedDynamite.GetComponent<Rigidbody>();
        if (!rigidbody)
        {
            Debug.LogWarning("[Bandit] Dynamite prefab needs a Rigidbody.");
            Destroy(instantiatedDynamite);
            return false;
        }

        rigidbody.isKinematic     = false;
        rigidbody.useGravity      = true;
        rigidbody.linearVelocity  = initialVelocity + trainVelocity;
        rigidbody.angularVelocity = Random.insideUnitSphere * 4f;

        // Subscribe to projectile events (with a failsafe)
        UnsubscribeProjectile();
        activeDynamiteGameObject = instantiatedDynamite;
        activeProjectile = instantiatedDynamite.GetComponent<DynamiteProjectile>();
        if (activeProjectile)
        {
            var myCollider = GetComponentInChildren<Collider>();
            activeProjectile.Initialize(myCollider);
            activeProjectile.OnExploded += HandleProjectileExploded;
        }
        StartCoroutine(ClearActiveDynamiteFailsafe(instantiatedDynamite, 6f));

        Debug.DrawLine(throwOriginTransform.position, landingPoint, Color.yellow, 1.5f);
        Debug.DrawRay(throwOriginTransform.position, (initialVelocity + trainVelocity) * 0.25f, Color.cyan, 1.5f);
        return true;
    }

    private void HandleProjectileExploded()
    {
        activeDynamiteGameObject = null;
        UnsubscribeProjectile();

        if (!debugPaceForever && !shuttingDown)
            StartCoroutine(RetreatAndWait());
    }

    private IEnumerator ClearActiveDynamiteFailsafe(GameObject dynamiteToCheck, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (activeDynamiteGameObject == dynamiteToCheck)
        {
            activeDynamiteGameObject = null;
            UnsubscribeProjectile();
        }
    }

    private void UnsubscribeProjectile()
    {
        if (activeProjectile != null)
        {
            activeProjectile.OnExploded -= HandleProjectileExploded;
            activeProjectile = null;
        }
    }

    private Vector3 ComputeBallisticVelocityViaApex(Vector3 startPosition, Vector3 endPosition, float apexHeightY)
    {
        float gravityMagnitude               = Mathf.Abs(Physics.gravity.y);
        float initialVerticalVelocityUp      = Mathf.Sqrt(2f * gravityMagnitude * Mathf.Max(0.01f, apexHeightY - startPosition.y));
        float timeAscendingToApexSeconds     = initialVerticalVelocityUp / gravityMagnitude;

        float initialVerticalVelocityDown    = Mathf.Sqrt(2f * gravityMagnitude * Mathf.Max(0.01f, apexHeightY - endPosition.y));
        float timeDescendingFromApexSeconds  = initialVerticalVelocityDown / gravityMagnitude;

        float totalFlightTimeSeconds         = timeAscendingToApexSeconds + timeDescendingFromApexSeconds;

        Vector3 horizontalDisplacement       = endPosition - startPosition;
        horizontalDisplacement.y             = 0f;
        Vector3 horizontalVelocity           = horizontalDisplacement / totalFlightTimeSeconds;

        return horizontalVelocity + Vector3.up * initialVerticalVelocityUp;
    }

    // Keep the name so BanditSpawner continues to work
    public void SetupForSpawner(Transform trainRootParameter, BoxCollider deckBoundsParameter, BanditStats statsAsset, bool spawnOnRightSide, Action onFinishedCallback = null)
    {
        this.trainRootTransform  = trainRootParameter;
        this.trainDeckBounds     = deckBoundsParameter;
        this.banditStats         = statsAsset;
        this.spawnOnRight        = spawnOnRightSide;
        this.autoRespawnEnabled  = false; // spawner controls respawn timing
        this.onFinished          = onFinishedCallback;

        setupReceived = true;

        // If prefab didn’t have it, try to resolve throw origin now that we’re fully placed in scene
        TryAutoWireThrowOrigin();
    }

    // DEBUG gizmos
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        if (throwOriginTransform) Gizmos.DrawSphere(throwOriginTransform.position, 0.06f);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.25f);
    }

    private bool ValidateSetup()
    {
        // Try one last time to auto-wire
        TryAutoWireThrowOrigin();

        if (!trainRootTransform)   { Debug.LogError("[Bandit] trainRootTransform not set.");    return false; }
        if (!trainDeckBounds)      { Debug.LogError("[Bandit] trainDeckBounds not set.");       return false; }
        if (!throwOriginTransform) { Debug.LogError("[Bandit] throwOriginTransform not set.");  return false; }
        if (!dynamitePrefab)       { Debug.LogError("[Bandit] dynamitePrefab not set.");        return false; }
        if (!banditStats)          { Debug.LogError("[Bandit] banditStats not set.");           return false; }
        return true;
    }
}
