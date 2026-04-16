using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
using System.Collections;
using Unity.VisualScripting;


public class JumpscareEnemy : EnemyHealth
{
    //[SerializeField] public List<train Car> trainCars;
    [SerializeField] public GameObject JumpscareVisual;
    [SerializeField] public TrainPathFollower RunningJumpscare;
    [SerializeField] public float timeToTeleport;
    [SerializeField] public PlayerController playerController;
    [SerializeField] public JumpscareAnimation jumpscareAnimation;
    [SerializeField] private TrainController trainController;
    [SerializeField] private TrainPathFollower backOfTrain;
    [SerializeField] private float backOfTrainOffset;
    //[SerializeField] public  RunningJumpscare;

    
    [Tooltip("First postition is for is the Jumpsare enemy is ahead of the player, Second is when it is behind the player")]
    [SerializeField] public List<CarTeleportPositions> jumpscareEnemyPositions;
    [SerializeField] public List<TrainPathFollower> trainCars;
    [SerializeField] public List<JumpscareHitbox>  bridgeJumpscareHitboxs;
    [SerializeField] public float verticalOffset = 2;

    private bool isJumpscareFacingForward = true;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        RunningJumpscare.gameObject.SetActive(false);
        StartCoroutine(TeleportIn());
        JumpscareHitbox[] allHitboxes = FindObjectsOfType<JumpscareHitbox>(true);

        bridgeJumpscareHitboxs = new List<JumpscareHitbox>();

        foreach (JumpscareHitbox hitbox in allHitboxes)
        {
            if (hitbox.isBridgeHitbox)
            {
                bridgeJumpscareHitboxs.Add(hitbox);
            }
        }
        bridgeJumpscareHitboxs.Sort((a, b) => 
            b.transform.position.z.CompareTo(a.transform.position.z)
        );
        bridgeJumpscareHitboxs.Reverse();
        bridgeJumpscareHitboxs.RemoveAt(0);
    }

    // Update is called once per frame
    void Update()
    {
    }

    public void StartRunning()
    {
        TeleportOut();
        RunningJumpscare.gameObject.SetActive(true);
        RunningJumpscare.setCurrentUnits(backOfTrain.getCurrentUnits()-backOfTrainOffset);
    }

    private void TeleportEnemy()
    {
        List<TeleportOption> validTeleportPositions = FindValidTelaportPositions();
        if (validTeleportPositions.Count == 0)
        {
            Debug.LogWarning("No valid teleport positions found!");
            return;
        }

        int randomIndex = Random.Range(0, validTeleportPositions.Count);
        var choice = validTeleportPositions[randomIndex];
        
        if (choice.hitbox == null || choice.position == null)
        {
            Debug.LogError("Teleport option is invalid: " + choice.carIndex);
            return;
        }


        foreach (JumpscareHitbox hitbox in bridgeJumpscareHitboxs) // make sure all jumpscare hit boxes is off
        {
            hitbox.StopJumpscareTimer();
            hitbox.gameObject.SetActive(false);
        }

        if (choice.isAbovePlayer != isJumpscareFacingForward)
        {
            isJumpscareFacingForward = !isJumpscareFacingForward;
            jumpscareAnimation.FlipAnimation();
        }
        choice.hitbox.gameObject.SetActive(true);
        this.transform.SetParent(choice.position, true);
        this.transform.localPosition = new Vector3(0, verticalOffset, 0);
        this.transform.localRotation = Quaternion.identity;
        jumpscareAnimation.StartCrouchingAnimation();
        
    }
    
    private List<TeleportOption> FindValidTelaportPositions()
    {
        List<TeleportOption> options = new List<TeleportOption>();// postition 
        int playerCar = FindPlayerCar();
        for (int i = 0; i < trainCars.Count; ++i)
        {
            bool isTop;
            if (i != playerCar)
            {
                Transform pos;
                if (playerCar < i)
                {
                    pos = jumpscareEnemyPositions[i].top;
                    isTop = true;
                } else {
                    pos = jumpscareEnemyPositions[i].bottom;
                    isTop = false;
                }

                // Find the corresponding hitbox for this car
                JumpscareHitbox hitbox = GetBridgeHitboxForCar(playerCar, i);

                options.Add(new TeleportOption
                {
                    position = pos,
                    hitbox = hitbox,
                    carIndex = i, 
                    isAbovePlayer = isTop
                });
            }
        }
        return options;
    }
    
    
    protected override void OnTakeDamage()
    {
        Debug.Log("jumpscare Enemy hit");
        canTakeDamage = false;

        TeleportOut();
        
        if (CurrentHP == 0)
        {
            
        } else {
            StartCoroutine( TeleportIn());
        }
    }

    void TeleportOut()
    {
        JumpscareVisual.SetActive(false);
        foreach (JumpscareHitbox hitbox in bridgeJumpscareHitboxs)
        {
            hitbox.StopJumpscareTimer();
            hitbox.gameObject.SetActive(false);
        }
    }
    
    protected IEnumerator TeleportIn()
    {
        yield return new WaitForSeconds(timeToTeleport);
        if (this == null || JumpscareVisual == null) {
            Debug.Log("TeleportIn interupted");
            yield break;
        }

        JumpscareVisual.SetActive(true);
        canTakeDamage = true;
        if (this != null)
            TeleportEnemy();
    }

    protected override void OnReset()
    {
        StartCoroutine(TeleportIn());
    }

    private int FindPlayerCar()
    {
        for (int i = 0; i < trainCars.Count; ++i)
        {
            if (playerController.CurrentTrain == trainCars[i])
                return i;
        }
        return -1;
    }
    private JumpscareHitbox GetBridgeHitboxForCar(int playerCar, int enemyCar)
    {
        if (enemyCar > playerCar)
            return bridgeJumpscareHitboxs[enemyCar - 1]; // enemy ahead → bridge before it
        else
            return bridgeJumpscareHitboxs[enemyCar];     // enemy behind → bridge after it
    }
    void OnDestroy()
    {
        StopAllCoroutines();
    }

    private void OnEnable()
    {
        if (trainController != null)
            trainController.CoalChanged += OnCoalChanged;
    }

    private void OnDisable()
    {
        if (trainController != null)
            trainController.CoalChanged -= OnCoalChanged;
    }
    
    private void OnCoalChanged(float coal)
    {
        if (coal <= 0f)
        {
            StartRunning();
        }
    }

}
