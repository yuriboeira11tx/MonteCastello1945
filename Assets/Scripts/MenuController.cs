using UnityEngine;
using UnityEngine.InputSystem;

public class MenuController : MonoBehaviour
{
    public GameObject menuOpcoes;
    public GameObject menuTecla;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            Debug.Log("Usuário pressionou uma tecla");
            menuOpcoes.SetActive(true);
            menuTecla.SetActive(false);
        }
    }
}
