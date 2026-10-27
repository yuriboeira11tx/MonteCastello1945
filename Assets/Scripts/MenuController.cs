using UnityEngine;
using UnityEngine.InputSystem;

public class MenuController : MonoBehaviour
{
    public GameObject menuOpcoes;
    public GameObject menuTecla;

    void Update()
    {
        if (menuTecla != null && menuTecla.activeSelf && Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            if (menuOpcoes != null) menuOpcoes.SetActive(true);
            menuTecla.SetActive(false);
        }
    }
}
