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

    // ----------------------------
    // Spawning Settings
    // ----------------------------

    [Header("Spawning")]
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
    
    // Other scripts can listen without the spawner knowing they exist.
    // Example: UI ("Bandit incoming!"), audio, achievements, analytics, etc.
    public event Action<GameObject> BanditSpawned;
    public event Action<GameObject> BanditRemoved;
    public event Action SpawningStopped;
    
    // These are “things we do” when certain events happen.
    // Default behavior is included, but you can swap these out later if needed.
    private Action onGameEndedCommand;
    private Action<GameObject> onBanditFinishedCommand;
    
    // These booleans represent the spawner’s current "mode".
    private bool stopSpawning = false;
    private bool spawnWindowArmed = false;

    // Timer for next spawn
    private float nextSpawnTimeSeconds;

    // Track living bandits so we don’t spawn too many.
    private readonly List<GameObject> aliveInstances = new();

    // For alternating sides
    private bool spawnOnRightNext = true;

    // ----------------------------
    // Lifecycle
    // ----------------------------

    private void Awake()
    {
        // Awake runs when the object loads.
        // We check references so we don’t crash later.

        if (!ValidateReferences())
        {
            Debug.LogError("[BanditSpawner] Missing references. Disabling spawner.", this);
            enabled = false;
            return;
        }

        // Set default commands.
        onGameEndedCommand = StopSpawningForever;
        onBanditFinishedCommand = DefaultRemoveBanditAndArmNextWindow;
    }

    private void OnEnable()
    {
        // We stop spawning when the run ends.
        SceneManagement.GameEnded += HandleGameEnded;

        // If this spawner gets enabled after the game already ended, respect that.
        if (SceneManagement.HasGameEnded)
            StopSpawningForever();
    }

    private void OnDisable()
    {
        SceneManagement.GameEnded -= HandleGameEnded;
    }

    private void Start()
    {
        // Start runs after Awake. We begin by arming the first spawn timer.
        ArmNextSpawnWindow();
    }

    private void Update()
    {
        // Update runs every frame.
        // We clean up dead references, then spawn if our timer says we can.

        if (!enabled)
            return;

        if (stopSpawning)
            return;

        PruneDestroyedBandits();

        if (aliveInstances.Count >= maxAlive)
            return;

        if (spawnWindowArmed && Time.time >= nextSpawnTimeSeconds)
        {
            spawnWindowArmed = false; // we “use up” the window
            SpawnOne();
        }
    }

    // ----------------------------
    // Spawning Core
    // ----------------------------

    private void ArmNextSpawnWindow()
    {
        // This sets a future time when we are allowed to spawn again.
        // We do this so bandits don’t spawn instantly back-to-back.

        if (stopSpawning)
            return;

        if (spawnWindowArmed)
            return;

        if (aliveInstances.Count >= maxAlive)
            return;

        float delay = UnityEngine.Random.Range(minRespawnDelay, maxRespawnDelay);
        nextSpawnTimeSeconds = Time.time + delay;
        spawnWindowArmed = true;
    }

    private void SpawnOne()
    {
        // This actually creates one bandit and sets it up.

        if (stopSpawning)
            return;

        if (!ValidateReferences())
            return;

        bool spawnOnRight = ChooseSide();

        Vector3 worldPosition = ComputeSpawnPosition(spawnOnRight);
        Quaternion worldRotation = ComputeSpawnRotationFacingTrain(worldPosition);

        GameObject spawnedBandit = Instantiate(
            banditPrefab,
            worldPosition,
            worldRotation,
            container != null ? container : null
        );

        DynamiteBandit bandit = spawnedBandit.GetComponent<DynamiteBandit>();
        if (bandit == null)
        {
            Debug.LogError("[BanditSpawner] Prefab is missing DynamiteBandit. Destroying spawn.", this);
            Destroy(spawnedBandit);
            ArmNextSpawnWindow();
            return;
        }

        // Observer: tell listeners we spawned a bandit.
        BanditSpawned?.Invoke(spawnedBandit);

        // IMPORTANT: Subscribe BEFORE setup so we don’t miss the callback.
        bandit.onFinished += () =>
        {
            // Command: “what do we do when a bandit is finished?”
            onBanditFinishedCommand?.Invoke(spawnedBandit);
        };

        // When the bandit says “I’m ready”, we officially count it as alive.
        bandit.SignalReady += () =>
        {
            if (stopSpawning)
                return;

            if (!aliveInstances.Contains(spawnedBandit))
                aliveInstances.Add(spawnedBandit);
        };

        // Setup the bandit with the spawner settings.
        bandit.SetupForSpawner(trainRoot, target, defaultStats, spawnOnRight);
    }

    // ----------------------------
    // Side / Position / Rotation Helpers
    // ----------------------------

    private bool ChooseSide()
    {
        // This decides whether we spawn on the right side or left side.

        if (randomizeSide)
            return UnityEngine.Random.value > 0.5f;

        if (alternateSides)
        {
            bool side = spawnOnRightNext;
            spawnOnRightNext = !spawnOnRightNext;
            return side;
        }

        // Default: always spawn on the right if no options are selected.
        return true;
    }

    private Vector3 ComputeSpawnPosition(bool spawnOnRight)
    {
        // This finds the exact position in the world where the bandit spawns.

        Vector3 localOffset = new Vector3(
            (spawnOnRight ? +1f : -1f) * Mathf.Abs(lateralOffset),
            0f,
            -Mathf.Abs(backOffset)
        );

        // Deck height is based on the player's bounds (train deck area).
        float deckY = target.position.y + yOffset;

        Vector3 worldPosition = trainRoot.TransformPoint(localOffset);
        worldPosition.y = deckY;

        return worldPosition;
    }

    private Quaternion ComputeSpawnRotationFacingTrain(Vector3 spawnPos)
    {
        // This rotates the bandit so it faces toward the train.

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
        // This runs when the bandit says it is “finished”.
        // We remove it from our alive list, then arm the next spawn timer.

        RemoveFromAliveList(spawnedBandit);

        // Observer: tell listeners a bandit was removed.
        BanditRemoved?.Invoke(spawnedBandit);

        // Only arm a new window if the run is still active.
        if (!stopSpawning)
            ArmNextSpawnWindow();
    }

    private void RemoveFromAliveList(GameObject instance)
    {
        // This safely removes destroyed objects or a specific bandit from our tracking list.

        for (int i = aliveInstances.Count - 1; i >= 0; i--)
        {
            if (aliveInstances[i] == null || aliveInstances[i] == instance)
                aliveInstances.RemoveAt(i);
        }
    }

    private void PruneDestroyedBandits()
    {
        // This removes any null entries so our list stays clean.

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
        // When the game ends, we run our “game ended” command.
        onGameEndedCommand?.Invoke();
    }

    private void StopSpawningForever()
    {
        // This permanently stops new bandits from spawning.

        stopSpawning = true;
        spawnWindowArmed = false;

        // Observer: tell listeners that spawning stopped.
        SpawningStopped?.Invoke();

        // Optional: if you want to delete existing bandits when the run ends:
        // foreach (var go in aliveInstances) if (go) Destroy(go);
        // aliveInstances.Clear();
    }

    // Let other scripts replace behaviors without editing this file.
    public void SetOnBanditFinishedCommand(Action<GameObject> command) => onBanditFinishedCommand = command;
    public void SetOnGameEndedCommand(Action command) => onGameEndedCommand = command;

    // ----------------------------
    // Validation
    // ----------------------------

    private bool ValidateReferences()
    {
        // This checks if we have everything needed to spawn bandits safely.
        // If not, we return false.

        return trainRoot != null
               && target != null
               && banditPrefab != null
               && defaultStats != null;
    }

    // ----------------------------
    // Gizmos (Editor Debug)
    // ----------------------------

    private void OnDrawGizmosSelected()
    {
        // This draws little spheres in the editor showing spawn points.

        if (trainRoot == null || target == null)
            return;

        float deckY = target.position.y + yOffset;

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
