using System.Buffers;
using UnityEngine;

public class JumpScareAnimation : UIManager
{
   //public GameObject deathUI;
   public GameObject jumpscareAnimation;
   public float time;
   public SceneManagement sceneManagement;
   
   private void Awake() {
      sceneManagement = SceneManagement.Instance;
      if (sceneManagement.IsJumpscared)
      {
         ShowJumpscare();
      }
   }
   public void ShowJumpscare()
   {
      StartCoroutine(ShowFor(jumpscareAnimation, time));
   }
}
