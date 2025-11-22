using System;
using System.Collections.Generic;
using UnityEngine;

public class BanditSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform   trainRoot;
    [SerializeField] private BoxCollider playerBounds;
    [SerializeField] private GameObject  banditPrefab;
    [SerializeField] private BanditStats defaultStats;

    [Header("Spawning")]
    [SerializeField] private float minRespawnDelay = 4f;
    [SerializeField] private float maxRespawnDelay = 9f;
    [SerializeField] private int   maxAlive       = 1;
    [SerializeField] private bool  alternateSides = true;
    [SerializeField] private bool  randomizeSide  = true;

    [SerializeField] private float lateralOffset  = 6f;
    [SerializeField] private float backOffset     = 3f;
    [SerializeField] private float yOffset        = 0.5f;

    [Tooltip("Optional: parent all spawned bandits under this transform")]
    [SerializeField] private Transform container;

    private readonly List<GameObject> aliveInstances = new();
    private bool  spawnOnRightNext   = true;
    private float nextSpawnTimeSeconds;
    private bool  spawnWindowArmed   = false;     // prevents stacking windows
    private bool  stopSpawning       = false;     // set when the run ends

    // ---------------- Lifecycle ----------------
    private void Awake()
    {
        if (!trainRoot || !playerBounds || !banditPrefab || !defaultStats)
        {
            Debug.LogError("[Spawner] Missing references.");
            enabled = false;
            return;
        }
    }

    private void OnEnable()
    {
        // Stop all future spawns when the game ends (game over or results)
        SceneManagement.GameEnded += StopSpawningForever;
        // If this component enabled after the game already ended, respect it
        if (SceneManagement.HasGameEnded) stopSpawning = true;
    }

    private void OnDisable()
    {
        SceneManagement.GameEnded -= StopSpawningForever;
    }

    private void Start()
    {
        ArmNextSpawnWindow();
    }

    private void Update()
    {
        if (!enabled) return;
        if (stopSpawning) return;

        // prune destroyed
        for (int index = aliveInstances.Count - 1; index >= 0; index--)
        {
            if (aliveInstances[index] == null)
                aliveInstances.RemoveAt(index);
        }

        if (aliveInstances.Count >= maxAlive) return;

        if (spawnWindowArmed && Time.time >= nextSpawnTimeSeconds)
        {
            spawnWindowArmed = false; // consume window
            SpawnOne();
        }
    }

    // ---------------- Spawning core ----------------
    private void ArmNextSpawnWindow()
    {
        if (stopSpawning) return;           // do not arm after run ends
        if (spawnWindowArmed) return;       // no stacking
        if (aliveInstances.Count >= maxAlive) return;

        spawnWindowArmed = true;
        nextSpawnTimeSeconds = Time.time + UnityEngine.Random.Range(minRespawnDelay, maxRespawnDelay);
        // Debug.Log($"[Spawner] Next window armed for t={nextSpawnTimeSeconds:0.00}");
    }

    private void SpawnOne()
    {
        if (stopSpawning) return;
        if (!trainRoot || !playerBounds || !banditPrefab || !defaultStats) return;

        // Decide side
        bool spawnOnRight;
        if (randomizeSide)             spawnOnRight = (UnityEngine.Random.value > 0.5f);
        else if (alternateSides)       spawnOnRight = spawnOnRightNext;
        else                           spawnOnRight = true;
        spawnOnRightNext = !spawnOnRight && alternateSides ? true : spawnOnRightNext;

        // Compute spawn position
        Vector3 localOffset = new Vector3(
            (spawnOnRight ? +1f : -1f) * Mathf.Abs(lateralOffset),
            0f,
            -Mathf.Abs(backOffset)
        );

        float deckY = playerBounds.bounds.min.y + yOffset;

        Vector3 worldPosition = trainRoot.TransformPoint(localOffset);
        worldPosition.y = deckY;

        // Face the train
        Vector3 toTrain = (trainRoot.position - worldPosition);
        toTrain.y = 0f;
        Quaternion spawnRotation = toTrain.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(toTrain.normalized, Vector3.up)
            : Quaternion.identity;

        GameObject spawnedBandit = Instantiate(
            banditPrefab,
            worldPosition,
            spawnRotation,
            container ? container : null
        );

        // Wire up bandit instance
        var bandit = spawnedBandit.GetComponent<DynamiteBandit>();
        if (!bandit)
        {
            Debug.LogError("[Spawner] Prefab is missing DynamiteBandit.");
            Destroy(spawnedBandit);
            ArmNextSpawnWindow();
            return;
        }

        // Subscribe BEFORE setup so we don’t miss the callback
        bandit.onFinished += () =>
        {
            // remove this instance
            for (int i = aliveInstances.Count - 1; i >= 0; i--)
            {
                if (aliveInstances[i] == null || aliveInstances[i] == spawnedBandit)
                    aliveInstances.RemoveAt(i);
            }

            // Only arm a new window if the run is still active
            if (!stopSpawning) ArmNextSpawnWindow();
        };

        // Setup from spawner
        bandit.SetupForSpawner(trainRoot, playerBounds, defaultStats, spawnOnRight);

        // Count it alive AFTER bandit confirms setup (prevents “idle statues”)
        bandit.SignalReady += () =>
        {
            if (stopSpawning) return;
            if (!aliveInstances.Contains(spawnedBandit))
                aliveInstances.Add(spawnedBandit);
        };
    }

    // ---------------- Helpers ----------------
    private void StopSpawningForever()
    {
        stopSpawning = true;
        spawnWindowArmed = false; // cancel any window that was armed
        // Optional: you can also clean up existing bandits here if desired.
        // foreach (var go in aliveInstances) if (go) Destroy(go);
        // aliveInstances.Clear();
    }

    // Debug gizmo
    private void OnDrawGizmosSelected()
    {
        if (!trainRoot || playerBounds == null) return;

        float deckY = playerBounds.bounds.min.y + yOffset;

        Vector3 leftLocal  = new Vector3(-Mathf.Abs(lateralOffset), 0f, -Mathf.Abs(backOffset));
        Vector3 rightLocal = new Vector3(+Mathf.Abs(lateralOffset), 0f, -Mathf.Abs(backOffset));

        Vector3 leftWorld  = trainRoot.TransformPoint(leftLocal);  leftWorld.y  = deckY;
        Vector3 rightWorld = trainRoot.TransformPoint(rightLocal); rightWorld.y = deckY;

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(leftWorld, 0.2f);
        Gizmos.DrawSphere(rightWorld, 0.2f);
        Gizmos.DrawLine(leftWorld,  trainRoot.position);
        Gizmos.DrawLine(rightWorld, trainRoot.position);
    }
}
