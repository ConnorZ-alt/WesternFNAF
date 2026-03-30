using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BanditSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform trainRoot;
    [SerializeField] private Transform target;
    [SerializeField] private GameObject banditPrefab;
    [SerializeField] private BanditStats defaultStats;
    [SerializeField] private TrainPathFollower trainPathFollower;

    [Header("Spawning")]
    [SerializeField] private float initialSpawnDelaySeconds = 10f;
    [SerializeField] private float minRespawnDelay = 4f;
    [SerializeField] private float maxRespawnDelay = 9f;
    [SerializeField] private int maxAlive = 1;

    [SerializeField] private bool alternateSides = true;
    [SerializeField] private bool randomizeSide = true;

    [Header("Spawn Position Offsets")]
    [SerializeField] private float lateralOffset = 6f;
    [SerializeField] private float backOffset = 3f;
    [SerializeField] private float yOffset = 0.5f;

    [Tooltip("Optional: parent all spawned bandits under this transform")]
    [SerializeField] private Transform container;

    public event Action<GameObject> BanditSpawned;
    public event Action<GameObject> BanditRemoved;
    public event Action SpawningStopped;

    private Action onGameEndedCommand;
    private Action<GameObject> onBanditFinishedCommand;

    private bool stopSpawning = false;
    private bool spawnWindowArmed = false;

    private float nextSpawnTimeSeconds;

    private readonly List<GameObject> aliveInstances = new();

    private bool spawnOnRightNext = true;

    // ----------------------------
    // Lifecycle
    // ----------------------------

    private void Awake()
    {
        Debug.Log(
            $"[BanditSpawner] Awake. trainRoot={(trainRoot ? trainRoot.name : "NULL")}, " +
            $"target={(target ? target.name : "NULL")}, " +
            $"banditPrefab={(banditPrefab ? banditPrefab.name : "NULL")}, " +
            $"defaultStats={(defaultStats ? defaultStats.name : "NULL")}",
            this
        );

        if (!ValidateReferences())
        {
            Debug.LogError("[BanditSpawner] Missing references. Disabling spawner.", this);
            enabled = false;
            return;
        }

        onGameEndedCommand = StopSpawningForever;
        onBanditFinishedCommand = DefaultRemoveBanditAndArmNextWindow;
    }

    private void OnEnable()
    {
        SceneManagement.GameEnded += HandleGameEnded;

        if (SceneManagement.HasGameEnded)
            StopSpawningForever();
    }

    private void OnDisable()
    {
        SceneManagement.GameEnded -= HandleGameEnded;
    }

    private void Start()
    {
        // First spawn uses the special startup delay.
        ArmSpawnWindow(initialSpawnDelaySeconds);
    }

    private void Update()
    {
        if (!enabled)
            return;

        if (stopSpawning)
            return;

        PruneDestroyedBandits();

        if (aliveInstances.Count >= maxAlive)
            return;

        if (spawnWindowArmed && Time.time >= nextSpawnTimeSeconds)
        {
            spawnWindowArmed = false;
            SpawnOne();
        }
    }

    // ----------------------------
    // Spawning Core
    // ----------------------------

    private void ArmSpawnWindow(float delay)
    {
        if (stopSpawning)
            return;

        if (spawnWindowArmed)
            return;

        if (aliveInstances.Count >= maxAlive)
            return;

        nextSpawnTimeSeconds = Time.time + delay;
        spawnWindowArmed = true;

        Debug.Log($"[BanditSpawner] Next spawn armed in {delay:F2}s at Time.time={nextSpawnTimeSeconds:F2}", this);
    }

    private void ArmNextSpawnWindow()
    {
        // Later spawns use normal respawn timing.
        if (stopSpawning)
            return;

        if (spawnWindowArmed)
            return;

        if (aliveInstances.Count >= maxAlive)
            return;

        float delay = UnityEngine.Random.Range(minRespawnDelay, maxRespawnDelay);
        nextSpawnTimeSeconds = Time.time + delay;
        spawnWindowArmed = true;

        Debug.Log($"[BanditSpawner] Next respawn armed in {delay:F2}s at Time.time={nextSpawnTimeSeconds:F2}", this);
    }

    private void SpawnOne()
    {
        Debug.Log("[BanditSpawner] SpawnOne entered.", this);

        if (stopSpawning)
            return;

        if (!ValidateReferences())
        {
            Debug.LogError("[BanditSpawner] SpawnOne blocked: ValidateReferences failed.", this);
            return;
        }

        bool spawnOnRight = ChooseSide();

        Vector3 worldPosition = ComputeSpawnPosition(spawnOnRight);
        Quaternion worldRotation = ComputeSpawnRotationFacingTrain(worldPosition);

        Debug.Log($"[BanditSpawner] Instantiating bandit at {worldPosition} side={(spawnOnRight ? "RIGHT" : "LEFT")}", this);

        GameObject spawnedBandit = Instantiate(
            banditPrefab,
            worldPosition,
            worldRotation,
            container != null ? container : null
        );

        Debug.Log($"[BanditSpawner] Instantiate returned: {(spawnedBandit ? spawnedBandit.name : "NULL")}", this);

        DynamiteBandit bandit = spawnedBandit.GetComponent<DynamiteBandit>();
        if (bandit == null)
        {
            Debug.LogError("[BanditSpawner] Prefab is missing DynamiteBandit. Destroying spawn.", this);
            Destroy(spawnedBandit);
            ArmNextSpawnWindow();
            return;
        }

        BanditSpawned?.Invoke(spawnedBandit);

        bandit.onFinished += () =>
        {
            onBanditFinishedCommand?.Invoke(spawnedBandit);
        };

        bandit.SignalReady += () =>
        {
            if (stopSpawning)
                return;

            if (!aliveInstances.Contains(spawnedBandit))
                aliveInstances.Add(spawnedBandit);

            Debug.Log($"[BanditSpawner] Bandit signaled ready: {spawnedBandit.name}", this);
        };

        bandit.SetupForSpawner(trainRoot, target, defaultStats, spawnOnRight, trainPathFollower);
    }

    // ----------------------------
    // Side / Position / Rotation Helpers
    // ----------------------------

    private bool ChooseSide()
    {
        if (randomizeSide)
            return UnityEngine.Random.value > 0.5f;

        if (alternateSides)
        {
            bool side = spawnOnRightNext;
            spawnOnRightNext = !spawnOnRightNext;
            return side;
        }

        return true;
    }

    private Vector3 ComputeSpawnPosition(bool spawnOnRight)
    {
        Vector3 localOffset = new Vector3(
            (spawnOnRight ? +1f : -1f) * Mathf.Abs(lateralOffset),
            0f,
            -Mathf.Abs(backOffset)
        );

        float deckY = target.position.y + yOffset;

        Vector3 worldPosition = trainRoot.TransformPoint(localOffset);
        worldPosition.y = deckY;

        return worldPosition;
    }

    private Quaternion ComputeSpawnRotationFacingTrain(Vector3 spawnPos)
    {
        Vector3 toTrain = trainRoot.position - spawnPos;
        toTrain.y = 0f;

        if (toTrain.sqrMagnitude > 0.0001f)
            return Quaternion.LookRotation(toTrain.normalized, Vector3.up);

        return Quaternion.identity;
    }

    // ----------------------------
    // Cleanup / Finish Logic
    // ----------------------------

    private void DefaultRemoveBanditAndArmNextWindow(GameObject spawnedBandit)
    {
        RemoveFromAliveList(spawnedBandit);

        BanditRemoved?.Invoke(spawnedBandit);

        if (!stopSpawning)
            ArmNextSpawnWindow();
    }

    private void RemoveFromAliveList(GameObject instance)
    {
        for (int i = aliveInstances.Count - 1; i >= 0; i--)
        {
            if (aliveInstances[i] == null || aliveInstances[i] == instance)
                aliveInstances.RemoveAt(i);
        }
    }

    private void PruneDestroyedBandits()
    {
        for (int i = aliveInstances.Count - 1; i >= 0; i--)
        {
            if (aliveInstances[i] == null)
                aliveInstances.RemoveAt(i);
        }
    }

    // ----------------------------
    // Game End Handling
    // ----------------------------

    private void HandleGameEnded()
    {
        onGameEndedCommand?.Invoke();
    }

    private void StopSpawningForever()
    {
        if (stopSpawning)
            return;

        stopSpawning = true;
        spawnWindowArmed = false;

        SpawningStopped?.Invoke();
    }

    public void SetOnBanditFinishedCommand(Action<GameObject> command) => onBanditFinishedCommand = command;
    public void SetOnGameEndedCommand(Action command) => onGameEndedCommand = command;

    // ----------------------------
    // Validation
    // ----------------------------

    private bool ValidateReferences()
    {
        return trainRoot != null
               && target != null
               && banditPrefab != null
               && defaultStats != null
               && trainPathFollower != null;
    }

    private void OnValidate()
    {
        initialSpawnDelaySeconds = Mathf.Max(0f, initialSpawnDelaySeconds);
        minRespawnDelay = Mathf.Max(0f, minRespawnDelay);
        maxRespawnDelay = Mathf.Max(minRespawnDelay, maxRespawnDelay);
        maxAlive = Mathf.Max(1, maxAlive);
    }

    // ----------------------------
    // Gizmos
    // ----------------------------

    private void OnDrawGizmosSelected()
    {
        if (trainRoot == null || target == null)
            return;

        float deckY = target.position.y + yOffset;

        Vector3 leftLocal = new Vector3(-Mathf.Abs(lateralOffset), 0f, -Mathf.Abs(backOffset));
        Vector3 rightLocal = new Vector3(+Mathf.Abs(lateralOffset), 0f, -Mathf.Abs(backOffset));

        Vector3 leftWorld = trainRoot.TransformPoint(leftLocal);
        leftWorld.y = deckY;

        Vector3 rightWorld = trainRoot.TransformPoint(rightLocal);
        rightWorld.y = deckY;

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(leftWorld, 0.2f);
        Gizmos.DrawSphere(rightWorld, 0.2f);
        Gizmos.DrawLine(leftWorld, trainRoot.position);
        Gizmos.DrawLine(rightWorld, trainRoot.position);
    }
}