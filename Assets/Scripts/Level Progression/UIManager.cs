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
    
    protected IEnumerator ShowFor(GameObject group, float time)
    {
        Show(group);
        yield return new WaitForSeconds(time);
        Hide(group);
    }
}
