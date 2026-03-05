using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class JumpscareEnemy : EnemyHealth
{
    //[SerializeField] public List<train Car> trainCars;
    [SerializeField] public JumpscareHitbox JumpscareHitbox;
    [SerializeField] public GameObject RunningJumpscare;
    //[SerializeField] public  RunningJumpscare;

    
    [Tooltip("First postition is for is the Jumpsare enemy is ahead of the player, Second is when it is behind the player")]
    [SerializeField] public List<List<Vector3>> jumpscareEnemyPostitions;
    [SerializeField] public List<Vector3> jumpscareHitboxPostitions;
    

    private List<int> carjumpscarePostitions = new List<int>(); //car that the jumpscare enemy would be attached to
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        RunningJumpscare.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
    }

    public void StartRunning()
    {
        RunningJumpscare.SetActive(false);
    }

    private void TeleportEnemy()
    {
        List<Vector3> validTelaportPositions = FindValidTelaportPositions();

        int randomIndex = Random.Range(0, validTelaportPositions.Count);

        Vector3 randomHitboxPosition = jumpscareHitboxPostitions[randomIndex];
        Vector3 randomEnemyPosition = validTelaportPositions[randomIndex];

        transform.position = randomEnemyPosition;
        
        jumpscareEnemyPostitions = new List<List<Vector3>>();
        carjumpscarePostitions = new List<int>();
    }
    
    private List<Vector3> FindValidTelaportPositions()
    {
        List<Vector3> validTelaportPositions = new List<Vector3>(); // postition 
        int playerCar = 0; // placeHolder varable 0 would be engein room and the largest number the back of the train 

        for (int i = 0; i < jumpscareEnemyPostitions.Count; ++i)
        {
            if (playerCar < i)
            {
                validTelaportPositions.Add(jumpscareEnemyPostitions[i][1]);
                carjumpscarePostitions.Add(i+1);
                
            } else {
                validTelaportPositions.Add(jumpscareEnemyPostitions[i][0]);
                carjumpscarePostitions.Add(i);
            }
        }
        return validTelaportPositions;
    }
    
    
    protected override void OnTakeDamage()
    {
        TeleportEnemy();
    }
    
    
}
