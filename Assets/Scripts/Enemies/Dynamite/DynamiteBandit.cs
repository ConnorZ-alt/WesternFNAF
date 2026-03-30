using System;
using System.Collections;
using UnityEngine;
using Random = UnityEngine.Random;

[DisallowMultipleComponent]
public class DynamiteBandit : MonoBehaviour, IDamageable
{
    // Bandit behavior happens in phases so we don’t have 1 giant messy loop.
    private enum BanditState
    {
        WaitingForSetup, // spawned but not fully wired yet
        Approaching,     // moving into position next to the train
        Pacing,          // staying beside the train and throwing on cooldown
        Retreating,      // backing away after throwing (optional behavior)
        Finished         // done and should be removed
    }

    [Header("References")]
    [SerializeField] private Transform trainRootTransform;      // Train root
    [SerializeField] private Transform specificTarget;
    [SerializeField] private float deckYOffsetMeters = 0.0f;

    [SerializeField] private BanditStats banditStats;           // Stats asset
    [SerializeField] private GameObject dynamitePrefab;
    [SerializeField] private TrainPathFollower trainPathFollower; 
    
    // private PlayerController playerController;
    // private TrainCarDynamiteTargets[] allCarTargets;
    private TrainPathFollower currentThrowTargetFollower;
    
    private PlayerCarTracker playerCarTracker;
    private TrainCarDynamiteTargets currentTargetCar;

    [SerializeField] private Transform throwOriginTransform;    // auto-wired if null
    [SerializeField] private LayerMask trainFloorMask;

    [SerializeField] private float throwCooldownSeconds = 15f;

    [Header("Spawn/Behavior")]
    [SerializeField] private bool spawnOnRight = true;
    [SerializeField] private float spawnYOffsetMeters = 1.0f;

    [Header("Life")]
    [SerializeField] private float maxHealth = 50f;
    [SerializeField] private bool destroyOnFinish = true;

    // DEBUG toggles
    [Header("Debug")]
    [SerializeField] private bool debugPaceForever = true;
    [SerializeField] private bool skipApproach = false;
    
    // Spawner, UI, audio, achievements, etc can listen without this class knowing about them.
    public event Action SignalReady;
    public event Action Finished;                  // “I’m done, free my spawn slot”
    public event Action<float> HealthChanged;      // current health
    public event Action ThrewDynamite;
    public event Action DynamiteExploded;

    // Keep your existing spawner hook name so you don’t break anything:
    public Action onFinished;
    
    // This lets you swap what happens when the bandit finishes.
    private Action finishCommand;

    private BanditState currentState = BanditState.WaitingForSetup;

    private float currentHealth;

    private bool setupReceived;
    private bool readySignalled;
    private bool shuttingDown;
    private bool finishedNotified;

    // Throw tracking
    private GameObject activeDynamiteGameObject;
    private DynamiteProjectile activeProjectile; // for unsubscribing
    private float nextThrowTimeSeconds;

    // Train motion for leading throws
    private Vector3 lastTrainWorldPosition;
    private Vector3 trainVelocity;

    // ----------------------------
    // Auto-wire helpers
    // ----------------------------

    private bool TryAutoWireThrowOrigin()
    {
        // This tries to find a child transform that is the throw point.
        // It makes prefabs easier because you don’t have to manually assign every time.

        if (throwOriginTransform != null)
            return true;

        throwOriginTransform =
            transform.Find("ThrowOrigin") ??
            transform.Find("Throw Origin") ??
            transform.Find("ThrowPoint") ??
            transform.Find("Throw Point") ??
            transform.Find("Hand") ??
            transform.Find("RightHand");

        return throwOriginTransform != null;
    }

    // ----------------------------
    // Unity Lifecycle
    // ----------------------------

    private void Awake()
    {
        // Awake happens when this object loads.
        // We try to auto-wire as early as possible.

        TryAutoWireThrowOrigin();

        // Default finish command uses the normal cleanup logic.
        finishCommand = DefaultFinishAndCleanup;
    }

    private IEnumerator Start()
    {
        Debug.Log($"[DynamiteBandit] Start on {name}. setupReceived={setupReceived}", this);
        
        // Start is a coroutine here so we can wait a few frames for setup injection.
        // This avoids “script order” bugs when a spawner configures the bandit right after spawning.

        TryAutoWireThrowOrigin();

        yield return StartCoroutine(WaitForSetupIfNeeded());

        if (!ValidateSetup())
        {
            Debug.LogError("[DynamiteBandit] Setup missing; disabling component.", this);
            enabled = false;
            yield break;
        }

        InitializeRuntimeData();

        // Tell the spawner we are “ready” (but only once).
        SignalReadyOnce();

        // Begin behavior.
        yield return StartCoroutine(RunStateMachine());
    }

    private void OnDisable()
    {
        // OnDisable happens when object is turned off or destroyed.
        // We clean up so we don’t leave event subscriptions hanging around.

        if (shuttingDown)
            return;

        shuttingDown = true;

        StopAllCoroutines();
        UnsubscribeProjectile();
        NotifyFinishedOnce();
    }

    private void OnDestroy()
    {
        // Extra safety if destroyed directly.
        NotifyFinishedOnce();
        UnsubscribeProjectile();
    }

    // ----------------------------
    // Setup from Spawner (keep name for compatibility)
    // ----------------------------

    public void SetupForSpawner(
        Transform trainRootParameter,
        Transform deckBoundsParameter,
        BanditStats statsAsset,
        bool spawnOnRightSide,
        TrainPathFollower trainFollowerParameter,
        Action onFinishedCallback = null)
    {
        trainRootTransform = trainRootParameter;
        specificTarget = deckBoundsParameter;
        banditStats = statsAsset;
        spawnOnRight = spawnOnRightSide;
        trainPathFollower = trainFollowerParameter;

        if (onFinishedCallback != null)
            onFinished = onFinishedCallback;

        setupReceived = true;

        TryAutoWireThrowOrigin();
    }

    // ----------------------------
    // IDamageable
    // ----------------------------

    public void TakeDamage(float damage)
    {
        // This reduces health and finishes the bandit if health hits 0.

        if (damage <= 0f || shuttingDown)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        HealthChanged?.Invoke(currentHealth);

        if (currentHealth <= 0f)
            Finish();
    }

    public void Kill()
    {
        // This is an instant kill for debugging or special cases.

        if (shuttingDown)
            return;

        currentHealth = 0f;
        HealthChanged?.Invoke(currentHealth);
        Finish();
    }

    // ----------------------------
    // Public finish helper (idempotent)
    // ----------------------------

    public void SignalFinishedOnce()
    {
        NotifyFinishedOnce();
    }

    private void Finish()
    {
        // This transitions the bandit into the finished state once.

        if (shuttingDown)
            return;

        SetState(BanditState.Finished);
        finishCommand?.Invoke();
    }

    // ----------------------------
    // State Machine
    // ----------------------------

    private IEnumerator RunStateMachine()
    {
        // This is the main behavior loop.
        // It runs until we are finished or shut down.

        if (!skipApproach)
            SetState(BanditState.Approaching);
        else
            SetState(BanditState.Pacing);

        // Short delay before first throw so spawning feels natural.
        nextThrowTimeSeconds = Time.time + 0.75f;

        while (!shuttingDown)
        {
            switch (currentState)
            {
                case BanditState.Approaching:
                    yield return StartCoroutine(StateApproach());
                    SetState(BanditState.Pacing);
                    break;

                case BanditState.Pacing:
                    yield return StartCoroutine(StatePace());
                    break;

                case BanditState.Retreating:
                    yield return StartCoroutine(StateRetreat());
                    // Retreat ends in Finish.
                    break;

                case BanditState.Finished:
                    // Stop the loop once finished.
                    yield break;

                default:
                    yield return null;
                    break;
            }

            yield return null;
        }
    }

    private IEnumerator StateApproach()
    {
        // Move toward the “alongside” target until we’re close enough.

        while (!shuttingDown && currentState == BanditState.Approaching)
        {
            Vector3 target = GetDesiredAlongsideWorldPosition();
            transform.position = Vector3.MoveTowards(
                transform.position,
                target,
                banditStats.moveSpeed * Time.deltaTime
            );

            ForceTransformYToDeck();
            FaceTrain();

            if (Vector3.Distance(transform.position, target) < 0.2f)
                yield break;

            yield return null;
        }
    }

    private IEnumerator StatePace()
    {
        // Stay beside the train and throw dynamite on cooldown.

        while (!shuttingDown && currentState == BanditState.Pacing)
        {
            PaceBesideTrain();
            UpdateTrainVelocity();

            // Throw if cooldown is done and we don’t have an active dynamite out.
            if (activeDynamiteGameObject == null && Time.time >= nextThrowTimeSeconds)
            {
                bool threw = TryThrowDynamiteOnce();

                // If throw worked, use normal cooldown. If not, retry soon.
                nextThrowTimeSeconds = Time.time + (threw ? throwCooldownSeconds : 1.0f);
            }

            yield return null;
        }
    }

    private IEnumerator StateRetreat()
    {
        // Back away for a short time, then finish.

        float elapsed = 0f;
        Vector3 retreatDir = (transform.position - trainRootTransform.position).normalized;

        while (!shuttingDown && currentState == BanditState.Retreating && elapsed < 2.5f)
        {
            transform.position += retreatDir * (banditStats.moveSpeed * 1.3f * Time.deltaTime);
            ForceTransformYToDeck();
            elapsed += Time.deltaTime;
            yield return null;
        }

        Finish();
    }

    private void SetState(BanditState newState)
    {
        // Central state change method so it’s easier to debug.

        if (currentState == newState)
            return;

        currentState = newState;
    }

    // ----------------------------
    // Movement helpers
    // ----------------------------

    private void PaceBesideTrain()
    {
        // Smoothly follow the alongside position so the bandit looks like it’s keeping up.

        Vector3 target = GetDesiredAlongsideWorldPosition();
        transform.position = Vector3.Lerp(
            transform.position,
            target,
            banditStats.acceleration * Time.deltaTime
        );

        ForceTransformYToDeck();
        FaceTrain();
    }

    private void FaceTrain()
    {
        TrainCarDynamiteTargets activeCar = GetActivePlayerCarTargets();

        Transform anchor = (activeCar != null && activeCar.BanditAnchor != null)
            ? activeCar.BanditAnchor
            : trainRootTransform;

        Vector3 toTrain = anchor.position - transform.position;
        toTrain.y = 0f;

        if (toTrain.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(toTrain, Vector3.up),
                Time.deltaTime * 6f
            );
        }
    }

    private void ForceTransformYToDeck()
    {
        Vector3 pos = transform.position;

        TrainCarDynamiteTargets activeCar = GetActivePlayerCarTargets();

        if (activeCar != null && activeCar.BanditAnchor != null)
            pos.y = activeCar.BanditAnchor.position.y;
        else if (specificTarget != null)
            pos.y = specificTarget.position.y;

        transform.position = pos;
    }

    private Vector3 GetDesiredAlongsideWorldPosition()
    {
        TrainCarDynamiteTargets activeCar = GetActivePlayerCarTargets();

        Transform anchor = (activeCar != null && activeCar.BanditAnchor != null)
            ? activeCar.BanditAnchor
            : trainRootTransform;

        float sideSign = spawnOnRight ? +1f : -1f;

        Vector3 worldPoint = anchor.position + anchor.right * (banditStats.lateralOffset * sideSign);
        worldPoint.y = anchor.position.y;

        return worldPoint;
    }

    private void UpdateTrainVelocity()
    {
        // We estimate train velocity so we can lead throws and hit a moving deck.

        if (trainRootTransform == null)
        {
            trainVelocity = Vector3.zero;
            return;
        }
        float dt = Time.deltaTime;
        if (dt <= 0) return;

        Vector3 currentPos = trainRootTransform.position;
        // Calculate velocity
        trainVelocity = (currentPos - lastTrainWorldPosition) / dt;
        // CRITICAL: Update the last position for the next frame
        lastTrainWorldPosition = currentPos;
    }

    // ----------------------------
    // Throwing
    // ----------------------------

    private bool TryThrowDynamiteOnce()
    {
        // This spawns and throws one dynamite if we’re allowed to.

        if (shuttingDown)
            return false;

        if (activeDynamiteGameObject != null)
            return false;

        if (!dynamitePrefab || !throwOriginTransform || banditStats == null)
        {
            Debug.LogWarning("[DynamiteBandit] Missing prefab/origin/stats, abort throw.", this);
            return false;
        }

        Vector3 landingPoint = ChooseLandingPointOnDeck();

        // For Step 3 cleanup, keep throw prediction simple.
        Vector3 predictedLandingPoint = landingPoint;

        GameObject dynamiteObj = Instantiate(
            dynamitePrefab,
            throwOriginTransform.position,
            Quaternion.identity
        );

        DynamiteProjectile projectile = dynamiteObj.GetComponent<DynamiteProjectile>();
        Rigidbody dynamiteRb = dynamiteObj.GetComponent<Rigidbody>();

        if (projectile == null || dynamiteRb == null)
        {
            Debug.LogError("[DynamiteBandit] Dynamite prefab is missing DynamiteProjectile or Rigidbody.", this);
            Destroy(dynamiteObj);
            return false;
        }

        // Ignore collision with the bandit itself.
        IgnoreProjectileCollisionWithBandit(dynamiteObj);
        projectile.SetInheritedTrain(currentThrowTargetFollower != null ? currentThrowTargetFollower : trainPathFollower);
        
        Vector3 throwVelocity = ComputeInitialThrowVelocity(predictedLandingPoint) * 1.02f;
        
        // Inherit the train's movement so the projectile stays with the moving train better.
        // Vector3 inheritedTrainVelocity = trainVelocity;
        // inheritedTrainVelocity.y = 0f;
        // throwVelocity += inheritedTrainVelocity;

        // Use the projectile’s own API.
        projectile.Throw(throwVelocity);

        // Optional spin for visual effect.
        dynamiteRb.angularVelocity = Random.insideUnitSphere * 2f;

        activeDynamiteGameObject = dynamiteObj;
        SubscribeToProjectile(projectile);

        // Failsafe: if the projectile never calls back, clear it anyway.
        StartCoroutine(ClearActiveDynamiteFailsafe(dynamiteObj, 6f));

        ThrewDynamite?.Invoke();

        Debug.DrawLine(throwOriginTransform.position, landingPoint, Color.yellow, 1.5f);

        return true;
    }
    
    private void IgnoreProjectileCollisionWithBandit(GameObject projectileObj)
    {
        Collider[] banditColliders = GetComponentsInChildren<Collider>(true);
        Collider[] projectileColliders = projectileObj.GetComponentsInChildren<Collider>(true);

        foreach (Collider banditCol in banditColliders)
        {
            if (banditCol == null) continue;

            foreach (Collider projectileCol in projectileColliders)
            {
                if (projectileCol == null) continue;
                Physics.IgnoreCollision(projectileCol, banditCol, true);
            }
        }
    }

    private Vector3 ChooseLandingPointOnDeck()
    {
        currentThrowTargetFollower = trainPathFollower;
        currentTargetCar = GetActivePlayerCarTargets();

        if (currentTargetCar != null && currentTargetCar.HasTargets)
        {
            Transform chosenPoint = currentTargetCar.GetRandomTargetPoint();
            if (chosenPoint != null)
            {
                currentThrowTargetFollower = currentTargetCar.CarFollower;

                Debug.Log($"[DynamiteBandit] Using player car: {currentTargetCar.name}, target point: {chosenPoint.name}", this);

                if (currentTargetCar.LandingCollider != null)
                {
                    // Sample from above the intended target point.
                    Vector3 samplePoint = chosenPoint.position + Vector3.up * 2f;

                    // ClosestPoint projects that sample onto the actual deck collider surface.
                    Vector3 landingPoint = currentTargetCar.LandingCollider.ClosestPoint(samplePoint);

                    // If ClosestPoint returns something meaningful, use it.
                    if ((landingPoint - samplePoint).sqrMagnitude > 0.0001f)
                        return landingPoint + Vector3.up * 0.05f;
                }

                Debug.LogWarning($"[DynamiteBandit] Falling back: {currentTargetCar.name} has no valid landing collider.", this);
                return GetFallbackLandingPoint();
            }
        }

        Debug.LogWarning("[DynamiteBandit] Falling back: no active player car target.", this);
        return GetFallbackLandingPoint();
    }
    
    private Vector3 GetFallbackLandingPoint()
    {
        currentThrowTargetFollower = trainPathFollower;

        if (specificTarget != null)
        {
            Vector3 right = trainRootTransform != null ? trainRootTransform.right : Vector3.right;
            Vector3 forward = trainRootTransform != null ? trainRootTransform.forward : Vector3.forward;

            float offsetX = Random.Range(-banditStats.throwInaccuracy, banditStats.throwInaccuracy);
            float offsetZ = Random.Range(-banditStats.throwInaccuracy, banditStats.throwInaccuracy);

            Vector3 offset = (right * offsetX) + (forward * offsetZ);
            Vector3 rayStart = specificTarget.position + offset + Vector3.up * 3f;

            if (Physics.Raycast(
                    rayStart,
                    Vector3.down,
                    out RaycastHit fallbackHit,
                    10f,
                    trainFloorMask,
                    QueryTriggerInteraction.Ignore))
            {
                return fallbackHit.point + Vector3.up * 0.05f;
            }

            return specificTarget.position + offset;
        }

        return transform.position;
    }

    private Vector3 ComputeInitialThrowVelocity(Vector3 landingPoint)
    {
        // This chooses an arc that goes up to an apex, then down to the landing point.

        float arcApexY = Mathf.Max(landingPoint.y, throwOriginTransform.position.y) + 2f;
        return ComputeBallisticVelocityViaApex(throwOriginTransform.position, landingPoint, arcApexY);
    }

    private void SubscribeToProjectile(DynamiteProjectile projectile)
    {
        // This hooks into the projectile so we know when it explodes.

        UnsubscribeProjectile();

        activeProjectile = projectile;
        if (activeProjectile != null)
            activeProjectile.OnExploded += HandleProjectileExploded;
    }

    private void HandleProjectileExploded()
    {
        // This runs when the dynamite explodes.

        activeDynamiteGameObject = null;
        UnsubscribeProjectile();

        DynamiteExploded?.Invoke();

        // If debugPaceForever is false, retreat after one explosion.
        if (!debugPaceForever && !shuttingDown)
            SetState(BanditState.Retreating);
    }

    private IEnumerator ClearActiveDynamiteFailsafe(GameObject dynamiteToCheck, float seconds)
    {
        // This prevents the bandit from getting stuck forever if the projectile never calls back.

        yield return new WaitForSeconds(seconds);

        if (activeDynamiteGameObject == dynamiteToCheck)
        {
            activeDynamiteGameObject = null;
            UnsubscribeProjectile();
        }
    }

    private void UnsubscribeProjectile()
    {
        // Always unhook events so we don’t leak references.

        if (activeProjectile != null)
        {
            activeProjectile.OnExploded -= HandleProjectileExploded;
            activeProjectile = null;
        }
    }

    private Vector3 ComputeBallisticVelocityViaApex(Vector3 start, Vector3 end, float apexHeightY)
    {
        // This computes a throw velocity by forcing the arc to reach a certain peak height.

        float g = Mathf.Abs(Physics.gravity.y);

        float vyUp = Mathf.Sqrt(2f * g * Mathf.Max(0.01f, apexHeightY - start.y));
        float tUp = vyUp / g;

        float vyDown = Mathf.Sqrt(2f * g * Mathf.Max(0.01f, apexHeightY - end.y));
        float tDown = vyDown / g;

        float totalTime = tUp + tDown;

        Vector3 horiz = end - start;
        horiz.y = 0f;

        Vector3 vxz = horiz / Mathf.Max(0.0001f, totalTime);
        return vxz + Vector3.up * vyUp;
    }

    // ----------------------------
    // Setup / Initialization helpers
    // ----------------------------

    private IEnumerator WaitForSetupIfNeeded()
    {
        // If setup wasn’t injected yet, wait a few frames for spawner timing.
        // This prevents random “missing reference” bugs.

        if (setupReceived && ValidateSetup())
            yield break;

        int framesWaited = 0;
        while (!setupReceived && framesWaited < 3)
        {
            yield return null;
            framesWaited++;
        }
    }

    private void InitializeRuntimeData()
    {
        // This sets starting health and caches train position for velocity calculation.

        currentHealth = Mathf.Max(1f, maxHealth);
        HealthChanged?.Invoke(currentHealth);

        lastTrainWorldPosition = trainRootTransform != null
            ? trainRootTransform.position
            : transform.position;
    }

    private void SignalReadyOnce()
    {
        // This tells the spawner “I’m ready” only one time.

        if (readySignalled)
            return;

        readySignalled = true;
        SignalReady?.Invoke();
    }

    private bool ValidateSetup()
    {
        TryAutoWireThrowOrigin();

        if (trainRootTransform == null)
        {
            Debug.LogError("[DynamiteBandit] trainRootTransform not set.", this);
            return false;
        }

        if (specificTarget == null)
        {
            Debug.LogError("[DynamiteBandit] specificTarget not set.", this);
            return false;
        }

        if (trainPathFollower == null)
        {
            Debug.LogError("[DynamiteBandit] trainPathFollower not set.", this);
            return false;
        }

        if (throwOriginTransform == null)
        {
            Debug.LogError("[DynamiteBandit] throwOriginTransform not set.", this);
            return false;
        }

        if (dynamitePrefab == null)
        {
            Debug.LogError("[DynamiteBandit] dynamitePrefab not set.", this);
            return false;
        }

        if (banditStats == null)
        {
            Debug.LogError("[DynamiteBandit] banditStats not set.", this);
            return false;
        }

        return true;
    }

    // ----------------------------
    // Finish / Notify
    // ----------------------------

    private void NotifyFinishedOnce()
    {
        // This tells the spawner we are done, but only once.

        if (finishedNotified)
            return;

        finishedNotified = true;

        // Old spawner callback
        onFinished?.Invoke();

        // New Observer event
        Finished?.Invoke();
    }

    private void DefaultFinishAndCleanup()
    {
        // This is the default “finish” behavior:
        // stop coroutines, unhook projectile events, notify spawner, then destroy/disable.

        if (shuttingDown)
            return;

        shuttingDown = true;

        StopAllCoroutines();
        UnsubscribeProjectile();
        NotifyFinishedOnce();

        if (destroyOnFinish)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }

    // Let other scripts swap finish behavior if they want.
    public void SetFinishCommand(Action command) => finishCommand = command;

    // ----------------------------
    // Debug Gizmos
    // ----------------------------

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        if (throwOriginTransform != null)
            Gizmos.DrawSphere(throwOriginTransform.position, 0.06f);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.25f);
    }
    
    private void EnsureSceneReferences()
    {
        if (playerCarTracker == null)
            playerCarTracker = FindFirstObjectByType<PlayerCarTracker>();
    }
    
    // private bool TryGetPlayerCurrentCarTargets(out TrainCarDynamiteTargets result)
    // {
    //     result = null;
    //
    //     EnsureSceneReferences();
    //
    //     if (playerController == null || playerController.CurrentTrain == null)
    //         return false;
    //
    //     TrainPathFollower playerCurrentFollower = playerController.CurrentTrain;
    //
    //     foreach (TrainCarDynamiteTargets carTargets in allCarTargets)
    //     {
    //         if (carTargets == null)
    //             continue;
    //
    //         if (carTargets.CarFollower == playerCurrentFollower)
    //         {
    //             result = carTargets;
    //             return true;
    //         }
    //     }
    //
    //     return false;
    // }
    
    private TrainCarDynamiteTargets GetActivePlayerCarTargets()
    {
        EnsureSceneReferences();

        if (playerCarTracker == null)
        {
            Debug.LogWarning("[DynamiteBandit] No PlayerCarTracker found.", this);
            return null;
        }

        if (playerCarTracker.CurrentCarTargets == null)
        {
            Debug.LogWarning("[DynamiteBandit] PlayerCarTracker has no current car yet.", this);
            return null;
        }

        return playerCarTracker.CurrentCarTargets;
    }
}
