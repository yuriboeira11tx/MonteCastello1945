using UnityEngine;
using UnityEngine.InputSystem;

public class SoldierAnimStateController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private float secondsToIdle = 5f;
    [SerializeField] private bool isWalking = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        secondsToIdle -= Time.deltaTime;

        if (secondsToIdle <= 0)
        {
            secondsToIdle = 5f;
            isWalking = !isWalking;
            animator.SetBool("isWalking", isWalking);
        }
    } 
}
