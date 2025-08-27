using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChangeNameInputWindow : MonoBehaviour
{
    public static ChangeNameInputWindow Instance;

    private Button okBtn;
    private Button cancelBtn;
    private TMP_InputField inputField;
    [SerializeField] private Transform playerName;

    public event EventHandler<OnOKEventArgs> OnOK;
    public class OnOKEventArgs : EventArgs {
        public string newName;
    }

    private void Awake() {
        Instance = this;

        okBtn = transform.Find("OkBtn").GetComponent<Button>();
        cancelBtn = transform.Find("CancelBtn").GetComponent<Button>();
        inputField = transform.Find("InputField").GetComponent<TMP_InputField>();

        Hide();
    }

    public void Show()
    {
        TMP_Text nameObject = playerName.GetComponentInChildren<TMP_Text>();
        if(nameObject != null)
        {
            inputField.text = nameObject.text;
        }

        okBtn.onClick.AddListener(() => {
            OnOK?.Invoke(this, new OnOKEventArgs{
                newName = inputField.text
            });

            Hide();
        });

        cancelBtn.onClick.AddListener(() => {
            Hide();
        });

        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
