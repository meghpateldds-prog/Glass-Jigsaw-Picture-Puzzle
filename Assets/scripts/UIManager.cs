using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    public GameObject Win;

    private void Awake()
    {
        Instance = this;

        Win.SetActive(false);
    }

    public void ShowWinPanel()
    {
        Win.SetActive(true);
    }
}
