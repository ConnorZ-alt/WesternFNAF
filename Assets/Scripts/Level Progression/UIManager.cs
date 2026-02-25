using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;

public class UIManager : MonoBehaviour {
    public void Show(GameObject group) {
        group.SetActive(true);
    }
    
    public void Hide(GameObject group) {
        group.SetActive(false);
    }
    
    protected IEnumerator ShowFor(GameObject group, float Time)
    {
        Show(group);
        yield return new WaitForSeconds(Time);
        Hide(group);
    }
}
