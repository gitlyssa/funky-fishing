using UnityEngine;
using TMPro;
using System;

public class ReplaceAllFonts : MonoBehaviour
{
    public TMP_FontAsset newFont;

    void Start()
    {
        TextMeshProUGUI[] texts = FindObjectsOfType<TextMeshProUGUI>(true);

        foreach (var text in texts)
        {
            // Replace font
            text.font = newFont;

            // Adjust font size
            float size = text.fontSize;

            if (Mathf.Approximately(size, 16f))
            {
                text.fontSize = 18f;
            }

            if (Mathf.Approximately(size, 14f))
            {
                text.fontSize = 16f;
            }

            else if (Mathf.Approximately(size, 12f))
            {
                text.fontSize = 14f;
            }
            // 20 stays unchanged automatically
        }

        Debug.Log("Fonts and sizes updated!");
    }
}
