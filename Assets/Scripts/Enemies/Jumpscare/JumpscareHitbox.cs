using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class JumpscareHitbox : MonoBehaviour
{
    public SceneManagement sceneManagement;
    
    public float timeToJumpscare = 2f;
    
    private Coroutine jumpscareTimer;
    private bool isJumpscareTimerRunning = false;
    public bool isBridgeHitbox = false; // 
    public JumpscareAnimation jumpscareAnimation;
    public void OnTriggerEnter(Collider other) 
    {
        Debug.Log("Something entered trigger: " + other.name);

        if (!other.CompareTag("Player")) return;
  
            if (timeToJumpscare == 0) // jumpscare player if there is no time before a jumpscare
            {
                JumpscarePlayer();
            } else if (!isJumpscareTimerRunning) // starts jumpscare timmer if one is not running
            {
                jumpscareTimer = StartCoroutine(WaitToJumpscare(timeToJumpscare));
                if (isBridgeHitbox)
                {
                    jumpscareAnimation.StartJumpscareLungeAnimation();
                }
            }
        
    }
    
    public void OnTriggerExit(Collider other)
    {
       StopJumpscareTimer();
    }
    
    protected IEnumerator WaitToJumpscare(float time)
    {
        isJumpscareTimerRunning = true;
        yield return new WaitForSeconds(time);
        JumpscarePlayer();
    }

    private void JumpscarePlayer()
    {
        isJumpscareTimerRunning = false;
        sceneManagement.IsJumpscared = true;
       sceneManagement.OnGameOver();
    }

    public void StopJumpscareTimer()
    {
        if (isJumpscareTimerRunning)
        {
            StopCoroutine(jumpscareTimer);
            jumpscareAnimation.Stop();
            isJumpscareTimerRunning = false;
        } 
    }
    void Start()
    {
        sceneManagement = FindObjectOfType<SceneManagement>();
    }
}

