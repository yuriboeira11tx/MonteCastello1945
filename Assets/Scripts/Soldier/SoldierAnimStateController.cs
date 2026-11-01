using UnityEngine;
using UnityEngine.InputSystem;

public class SoldierAnimStateController : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private float secondsToIdle = 5f;
    [SerializeField] private bool isWalking = false;
    [SerializeField] private bool isStandingRifle = false;

    private int isWalkingHash;
    private int isStandingRifleHash;
    private int isWalkingRifleHash;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        isWalkingHash = Animator.StringToHash("isWalking");
        isStandingRifleHash = Animator.StringToHash("isStandingRifle");
    }

    // Update is called once per frame
    void Update()
    {
        animator.SetBool(isWalkingHash, isWalking);

        animator.SetBool(isStandingRifleHash, isStandingRifle);
    } 
}
