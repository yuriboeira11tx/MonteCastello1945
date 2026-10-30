using UnityEngine;
using UnityEngine.InputSystem;

public class MenuController : MonoBehaviour
{
    public GameObject menuOptions;
    public GameObject menuKey;

    void Update()
    {
        if (menuKey != null && menuKey.activeSelf && Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            if (menuOptions != null) menuOptions.SetActive(true);
            menuKey.SetActive(false);
        }
    }
}
