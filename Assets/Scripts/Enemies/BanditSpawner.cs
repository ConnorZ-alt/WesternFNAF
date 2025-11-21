using System.Collections.Generic;
using UnityEngine;

public class BanditSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform    trainRoot;       // drag Train root
    [SerializeField] private BoxCollider  playerBounds;    // Train/PlayerBounds (landing area)
    [SerializeField] private GameObject   banditPrefab;    // your Bandit prefab (has DynamiteBandit)
    [SerializeField] private BanditStats  defaultStats;    // a BanditStats asset (Easy/Normal/Hard)

    [Header("Spawning")]
    [SerializeField] private float minRespawnDelay = 4f;
    [SerializeField] private float maxRespawnDelay = 9f;
    [SerializeField] private int   maxAlive       = 1;
    [SerializeField] private bool  alternateSides = true;   // L/R/L/R...
    [SerializeField] private bool  randomizeSide  = true;   // ignore alternate; pick random each spawn

    [SerializeField] private float lateralOffset  = 6f;
    [SerializeField] private float backOffset     = 3f;
    [SerializeField] private float yOffset        = 0.5f;

    [Tooltip("Optional: parent all spawned bandits under this transform")]
    [SerializeField] private Transform container;

    private readonly List<GameObject> aliveInstances = new();
    private bool  spawnOnRightNext = true; // for alternating
    private float nextSpawnTimeSeconds;

    void Awake()
    {
        if (!trainRoot || !playerBounds || !banditPrefab || !defaultStats)
        {
            Debug.LogError("[BanditSpawner] Missing references.");
            enabled = false;
            return;
        }
    }

    void Start()
    {
        // First spawn after a random delay
        nextSpawnTimeSeconds = Time.time + Random.Range(minRespawnDelay, maxRespawnDelay);
    }

    void Update()
    {
        if (!enabled) return;

        // Prune destroyed instances
        for (int index = aliveInstances.Count - 1; index >= 0; index--)
        {
            if (aliveInstances[index] == null) aliveInstances.RemoveAt(index);
        }

        if (aliveInstances.Count >= maxAlive) return;

        if (Time.time >= nextSpawnTimeSeconds)
        {
            SpawnOne();
            // Schedule next attempt after a random delay
            nextSpawnTimeSeconds = Time.time + Random.Range(minRespawnDelay, maxRespawnDelay);
        }
    }

    private void SpawnOne()
    {
        if (!trainRoot || !playerBounds || !banditPrefab || !defaultStats) return;

        // Decide spawn side
        bool spawnOnRight;
        if (randomizeSide)
        {
            spawnOnRight = (Random.value > 0.5f);
        }
        else if (alternateSides)
        {
            spawnOnRight = spawnOnRightNext;
            spawnOnRightNext = !spawnOnRightNext;
        }
        else
        {
            spawnOnRight = true;
        }

        // Compute spawn position beside the train (local → world)
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

        aliveInstances.Add(spawnedBandit);

        // Wire up bandit instance
        DynamiteBandit bandit = spawnedBandit.GetComponent<DynamiteBandit>();
        if (!bandit)
        {
            Debug.LogError("[BanditSpawner] Bandit prefab is missing DynamiteBandit.");
            return;
        }

        // Call the 4-arg version (compatible with older signature)
        bandit.SetupForSpawner(trainRoot, playerBounds, defaultStats, spawnOnRight);

        // Then assign the completion callback via the public field
        bandit.onFinished = () =>
        {
            // Remove this specific instance from the 'aliveInstances' list
            for (int index = aliveInstances.Count - 1; index >= 0; index--)
            {
                if (aliveInstances[index] == null) { aliveInstances.RemoveAt(index); continue; }
                if (aliveInstances[index] == spawnedBandit) { aliveInstances.RemoveAt(index); break; }
            }

            // Schedule a new spawn window
            nextSpawnTimeSeconds = Time.time + Random.Range(minRespawnDelay, maxRespawnDelay);
        };
    }

    // Optional gizmo to see where the first spawn would be
    private void OnDrawGizmosSelected()
    {
        if (!trainRoot || playerBounds == null) return;

        float deckY = playerBounds.bounds.min.y + yOffset;

        // Left/right gizmos
        Vector3 leftLocal  = new Vector3(-Mathf.Abs(lateralOffset), 0f, -Mathf.Abs(backOffset));
        Vector3 rightLocal = new Vector3(+Mathf.Abs(lateralOffset), 0f, -Mathf.Abs(backOffset));

        Vector3 leftWorld  = trainRoot.TransformPoint(leftLocal);  leftWorld.y  = deckY;
        Vector3 rightWorld = trainRoot.TransformPoint(rightLocal); rightWorld.y = deckY;

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(leftWorld, 0.2f);
        Gizmos.DrawSphere(rightWorld, 0.2f);
        Gizmos.DrawLine(leftWorld, trainRoot.position);
        Gizmos.DrawLine(rightWorld, trainRoot.position);
    }
}