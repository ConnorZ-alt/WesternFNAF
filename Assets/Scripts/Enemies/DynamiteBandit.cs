using System;
using UnityEngine;
using System.Collections;
using Random = UnityEngine.Random;

public class DynamiteBandit : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform   trainRootTransform;      // drag Train root
    [SerializeField] private BoxCollider trainDeckBounds;         // Train/PlayerBounds
    [SerializeField] private float       deckYOffsetMeters = 0.0f;
    [SerializeField] private bool        usePhysics = false;
    [SerializeField] private BanditStats banditStats;             // use your BanditStats asset
    [SerializeField] private GameObject  dynamitePrefab;
    [SerializeField] private Transform   throwOriginTransform;
    [SerializeField] private LayerMask   trainFloorMask;
    [SerializeField] private float       throwCooldownSeconds = 15f;

    [SerializeField] private bool  spawnOnRight = true;
    [SerializeField] private bool  autoRespawnEnabled = true;
    [SerializeField] private float spawnYOffsetMeters = 1.0f;

    // DEBUG
    [SerializeField] private bool debugPaceForever = true;
    [SerializeField] private bool skipApproach     = false;

    private GameObject activeDynamiteGameObject;
    private float      nextThrowTimeSeconds;
    private Vector3    lastTrainWorldPosition;
    private Vector3    trainVelocity;

    public Action onFinished;

    void Start()
    {
        if (!ValidateSetup())
        {
            enabled = false;
            return;
        }

        lastTrainWorldPosition = trainRootTransform ? trainRootTransform.position : transform.position;
        StartCoroutine(StateLoop());
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            TryThrowDynamiteOnce();
        }
    }

    // ---- Y / deck helpers ----
    float GetDeckYPosition() =>
        trainDeckBounds ? trainDeckBounds.bounds.min.y + deckYOffsetMeters : transform.position.y;

    void SnapPositionYToDeck(ref Vector3 worldPosition)
    {
        worldPosition.y = GetDeckYPosition();
    }

    void ForceTransformYToDeck()
    {
        var position = transform.position;
        SnapPositionYToDeck(ref position);
        transform.position = position;
    }

    // ---- State machine ----
    IEnumerator StateLoop()
    {
        Debug.Log("[Bandit] StateLoop started.");

        if (!skipApproach)
        {
            yield return StartCoroutine(ApproachAlongside());
            Debug.Log("[Bandit] Approach finished, entering pacing/throw loop.");
        }
        else
        {
            Debug.Log("[Bandit] Skipping approach for debug.");
        }

        nextThrowTimeSeconds = Time.time + 0.75f; // short delay before first throw

        while (true)
        {
            // Pace with the train
            PaceBesideTrain();

            // Compute train velocity to lead throws
            if (trainRootTransform)
            {
                float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
                Vector3 currentTrainPosition = trainRootTransform.position;
                trainVelocity           = (currentTrainPosition - lastTrainWorldPosition) / deltaTime;
                lastTrainWorldPosition  = currentTrainPosition;
            }
            else
            {
                trainVelocity = Vector3.zero;
            }

            // Throw on cooldown if nothing active
            if (activeDynamiteGameObject == null && Time.time >= nextThrowTimeSeconds)
            {
                Debug.Log("[Bandit] Attempting throw...");
                bool throwSucceeded = TryThrowDynamiteOnce();
                nextThrowTimeSeconds = Time.time + (throwSucceeded ? throwCooldownSeconds : 1.0f);
            }

            yield return null;
        }
    }

    IEnumerator ApproachAlongside()
    {
        while (true)
        {
            Vector3 targetWorldPosition = GetDesiredAlongsideWorldPosition();
            transform.position = Vector3.MoveTowards(transform.position, targetWorldPosition, banditStats.moveSpeed * Time.deltaTime);
            ForceTransformYToDeck();
            FaceTrain();
            if (Vector3.Distance(transform.position, targetWorldPosition) < 0.2f) break;
            yield return null;
        }
    }

    void PaceBesideTrain()
    {
        Vector3 targetWorldPosition = GetDesiredAlongsideWorldPosition();
        transform.position = Vector3.Lerp(transform.position, targetWorldPosition, banditStats.acceleration * Time.deltaTime);
        ForceTransformYToDeck();
        FaceTrain();
    }

    IEnumerator RetreatAndWait()
    {
        float elapsedSeconds = 0f;
        Vector3 retreatDirection = (transform.position - trainRootTransform.position).normalized;
        while (elapsedSeconds < 2.5f)
        {
            transform.position += retreatDirection * (banditStats.moveSpeed * 1.3f * Time.deltaTime);
            ForceTransformYToDeck();
            elapsedSeconds += Time.deltaTime;
            yield return null;
        }

        if (autoRespawnEnabled)
        {
            yield return new WaitForSeconds(banditStats.respawnDelay);
            yield break;
        }
        else
        {
            onFinished?.Invoke();
            Destroy(gameObject);
            yield break;
        }
    }

    Vector3 GetDesiredAlongsideWorldPosition()
    {
        float sideSign = spawnOnRight ? +1f : -1f;

        // Work in train local space, then transform to world:
        Vector3 localPoint = new Vector3(banditStats.lateralOffset * sideSign, 0f, -banditStats.followDistanceBack);
        Vector3 worldPoint = trainRootTransform.TransformPoint(localPoint);

        // Normalize Y to train height so we don't sink/spawn underground
        worldPoint.y = GetDeckYPosition();
        return worldPoint;
    }

    void FaceTrain()
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

    // ---- Throwing ----
    private bool TryThrowDynamiteOnce()
    {
        if (activeDynamiteGameObject != null) return false;
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
            Debug.Log("[Bandit] Ray hit: " + floorHitInfo.collider.name + " (layer " + LayerMask.LayerToName(floorHitInfo.collider.gameObject.layer) + ")");
        }
        else
        {
            // Fallback: floor center
            landingPoint = new Vector3(
                trainDeckBounds.bounds.center.x,
                trainDeckBounds.bounds.min.y + 0.02f,
                trainDeckBounds.bounds.center.z
            );
            Debug.LogWarning("[Bandit] TrainFloor raycast failed; using bounds center fallback.");
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
        rigidbody.linearVelocity  = initialVelocity + trainVelocity; // lead the moving train
        rigidbody.angularVelocity = Random.insideUnitSphere * 4f;

        var projectile = instantiatedDynamite.GetComponent<DynamiteProjectile>();
        if (projectile)
        {
            var myCollider = GetComponentInChildren<Collider>();
            projectile.Initialize(myCollider);
            projectile.OnExploded += () => { if (activeDynamiteGameObject == instantiatedDynamite) activeDynamiteGameObject = null; };
        }
        else
        {
            // If no projectile script, at least clear lock after a few seconds
            StartCoroutine(ClearActiveDynamiteAfter(instantiatedDynamite, 5f));
        }

        activeDynamiteGameObject = instantiatedDynamite;

        Debug.DrawLine(throwOriginTransform.position, landingPoint, Color.yellow, 1.5f);
        Debug.DrawRay(throwOriginTransform.position, (initialVelocity + trainVelocity) * 0.25f, Color.cyan, 1.5f);
        Debug.Log("[Bandit] Thrown with initialVelocity=" + initialVelocity + " trainVelocity=" + trainVelocity + " final=" + (initialVelocity + trainVelocity));
        return true;
    }

    private IEnumerator ClearActiveDynamiteAfter(GameObject dynamiteToClear, float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);
        if (activeDynamiteGameObject == dynamiteToClear) activeDynamiteGameObject = null;
    }

    private Vector3 ComputeBallisticVelocityViaApex(Vector3 startPosition, Vector3 endPosition, float apexHeightY)
    {
        float gravityMagnitude               = Mathf.Abs(Physics.gravity.y); // ~9.81
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

    static Vector3 RandomLandingPointInsideBounds(BoxCollider boundsCollider, BanditStats stats)
    {
        var boundsTransform = boundsCollider.transform;
        float localX = Mathf.Lerp(stats.minTrainX, stats.maxTrainX, Random.value);
        float localZ = Mathf.Lerp(stats.minTrainZ, stats.maxTrainZ, Random.value);
        Vector3 localPoint = new Vector3(localX, boundsCollider.center.y, localZ);
        return boundsTransform.TransformPoint(localPoint);
    }

    // Keep the name so BanditSpawner continues to work
    public void SetupForSpawner(Transform trainRootParameter, BoxCollider deckBoundsParameter, BanditStats statsAsset, bool spawnOnRightSide, Action onFinishedCallback = null)
    {
        this.trainRootTransform  = trainRootParameter;
        this.trainDeckBounds     = deckBoundsParameter;
        this.banditStats         = statsAsset;
        this.spawnOnRight        = spawnOnRightSide;
        this.autoRespawnEnabled  = false;
        this.onFinished          = onFinishedCallback;
    }

    // DEBUG gizmos
    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        if (throwOriginTransform) Gizmos.DrawSphere(throwOriginTransform.position, 0.06f);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.25f);
    }

    private bool ValidateSetup()
    {
        if (!trainRootTransform)   { Debug.LogError("[Bandit] trainRootTransform not set.");    return false; }
        if (!trainDeckBounds)      { Debug.LogError("[Bandit] trainDeckBounds not set.");       return false; }
        if (!throwOriginTransform) { Debug.LogError("[Bandit] throwOriginTransform not set.");  return false; }
        if (!dynamitePrefab)       { Debug.LogError("[Bandit] dynamitePrefab not set.");        return false; }
        if (!banditStats)          { Debug.LogError("[Bandit] banditStats not set.");           return false; }
        return true;
    }
}
