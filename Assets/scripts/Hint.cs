using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class Hint : MonoBehaviour
{
    void Start()
    {
        // Automatically hook up to the button component if found
        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(OnHintClick);
            Debug.Log("Hint button automatically linked.");
        }
    }

    /// <summary>
    /// Call this from the Button's OnClick event in the Inspector.
    /// </summary>
    public void OnHintClick()
    {
        Debug.Log("Hint button clicked!");
        if (PuzzleManager.instance != null)
        {
            PuzzleManager.instance.UseHint();
        }
        else
        {
            Debug.LogError("PuzzleManager instance is null!");
        }
    }
}
