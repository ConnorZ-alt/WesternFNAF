using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ReserveStripUI : MonoBehaviour
{
    [SerializeField] private TMP_Text ammoCountText;
    [SerializeField] private List<Image> bulletIcons;
    [SerializeField] private int maxVisibleBullets = 6;

    private int displayIndex;

    public void UpdateDisplay(int reserveCount)
    {
        if (ammoCountText)
            ammoCountText.text = reserveCount.ToString();

        // Show/hide bullet icons based on reserve count
        for (int i = 0; i < bulletIcons.Count; i++)
        {
            bulletIcons[i].enabled = i < Mathf.Min(reserveCount, maxVisibleBullets);
        }

        // Track the index of the topmost visible bullet (the next one to animate out)
        displayIndex = Mathf.Min(reserveCount - 1, bulletIcons.Count - 1);
    }

    /// <summary>
    /// Returns the screen-space position of the next bullet icon to animate from.
    /// Does NOT hide the icon or modify state — UpdateDisplay handles that.
    /// </summary>
    public Vector3 GetNextBulletPosition()
    {
        if (displayIndex < 0 || displayIndex >= bulletIcons.Count)
            return transform.position;

        // transform.position on a Screen Space Overlay canvas is already screen-space
        return bulletIcons[displayIndex].transform.position;
    }
}