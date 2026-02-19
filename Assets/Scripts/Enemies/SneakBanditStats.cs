// using UnityEngine;
//
// [CreateAssetMenu(fileName = "SneakBanditStats", menuName = "Enemies/SneakBandit Stats")]
// public class SneakBanditStats : ScriptableObject
// {
//     [Header("Health")]
//     public float maxHealth = 40f;
//
//     [Header("Movement")]
//     public float walkSpeed = 2.2f;
//     public float sneakSpeed = 1.4f;
//     public float dashSpeed = 6f;
//     public float acceleration = 6f;
//
//     [Header("Stealth")]
//     [Range(10f, 160f)] public float fovDegrees = 95f;   // player "looking at me" cone
//     public float hearRadius = 6f;                       // (hook for later if you want)
//     public float minLOSSecondsBeforeAbort = 2.0f;       // if watched this long, relocate
//
//     [Header("Cover / Timing")]
//     public float hideTimeMin = 1.0f;
//     public float hideTimeMax = 2.0f;
//     public float peekTime = 0.35f;
//     public float reacquireDelay = 0.75f;
//
//     [Header("Jumpscare")]
//     public float jumpscareWindup = 0.35f;               // delay before final dash
//     public float killDistance = 1.8f;                   // distance to trigger kill
//
//     [Header("Spawning")]
//     public float respawnDelay = 5f;                     // used by spawner timing
//     public float spawnYOffset = 0.5f;                   // sits slightly above deck
//
//     [Header("Escalation")]
//     public bool  allowTwo = true;                       // permit 2 sneaks later
//     public float timeUntilSecondSpawn = 35f;            // backup; spawner may override
// }