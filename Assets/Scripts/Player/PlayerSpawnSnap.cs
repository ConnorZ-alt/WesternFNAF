using UnityEngine;

[DisallowMultipleComponent]
public class PlayerSpawnSnap : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;
    private CharacterController cc;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
    }

    private void Start()
    {
        SnapToSpawn();
    }

    public void SnapToSpawn()
    {
        if (!spawnPoint)
        {
            Debug.LogError("[PlayerSpawnSnap] No spawnPoint assigned.");
            return;
        }

        // Disable CC so we can place safely
        if (cc) cc.enabled = false;
        transform.position = spawnPoint.position;
        transform.rotation = spawnPoint.rotation;
        if (cc) cc.enabled = true;
    }
}