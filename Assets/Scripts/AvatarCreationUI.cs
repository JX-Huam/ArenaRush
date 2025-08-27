using UnityEngine;
using UnityEngine.UI;

public class AvatarCreationUI : MonoBehaviour
{
    public static AvatarCreationUI Instance { get; private set; }
    [SerializeField] private Button createAvatarButton;

    private void Awake()
    {
        Instance = this;
    }
    
    private void Start()
    {
        if (createAvatarButton != null)
        {
            createAvatarButton.onClick.AddListener(() => 
            {
                RPMAvatarManager.Instance.OpenAvatarCreatorWithRecreate();
            });
        }
    }

    public void Hide() {
        gameObject.SetActive(false);
    }
}