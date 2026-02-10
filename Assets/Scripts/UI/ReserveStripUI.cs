using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ReserveStripUI : MonoBehaviour
{
    [SerializeField] private TMP_Text ammoCountText;
    [SerializeField] private List<Image> bulletIcons; // Visual bullets in the strip
    [SerializeField] private int maxVisibleBullets = 6;
    
    private int displayIndex;

    public void UpdateDisplay(int reserveCount)
    {
        ammoCountText.text = reserveCount.ToString();
        
        // Update visible bullet icons
        for (int i = 0; i < bulletIcons.Count; i++)
        {
            bulletIcons[i].enabled = i < Mathf.Min(reserveCount, maxVisibleBullets);
        }
        
        displayIndex = Mathf.Min(reserveCount - 1, bulletIcons.Count - 1);
    }

    public Vector3 GetNextBulletPosition()
    {
        if (displayIndex < 0 || displayIndex >= bulletIcons.Count)
            return transform.position;
            
        Vector3 pos = bulletIcons[displayIndex].transform.position;
        bulletIcons[displayIndex].enabled = false;
        displayIndex--;
        return pos;
    }
}