using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class SoldierAI : MonoBehaviour
{
    private enum CampActivity
    {
        Standard, SittingIdle, SittingRelaxed, Meeting, Talking, Looking, Drinking, LiftingObject, Saluting, Sleeping
    }

    public NavMeshAgent agent;
    public bool isStandingRifle;
    public bool isJack;
    public bool isBurpee;
    public bool isPushup;

    [SerializeField] private Animator animator;
    [SerializeField, Tooltip("Atividade fixa no acampamento. Standard mantém patrulhas e exercícios.")]
    private CampActivity campActivity;
    [SerializeField] private Transform[] patrolPoints;
    [SerializeField, Min(0.05f)] private float repathInterval = 0.2f;
    private int nextPatrolPoint;
    private float nextRouteCheck;

    private static readonly int IsWalkingHash = Animator.StringToHash("isWalking");
    private static readonly int IsStandingRifleHash = Animator.StringToHash("isStandingRifle");
    private static readonly int IsJackHash = Animator.StringToHash("isJack");
    private static readonly int IsBurpeeHash = Animator.StringToHash("isBurpee");
    private static readonly int IsPushupHash = Animator.StringToHash("isPushup");
    private static readonly int CampActivityHash = Animator.StringToHash("campActivity");

    void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        if (campActivity != CampActivity.Standard)
        {
            animator.SetInteger(CampActivityHash, (int)campActivity);
            animator.Play("Base Layer.Camp." + campActivity, 0, 0f);
            return;
        }

        animator.SetBool(IsStandingRifleHash, isStandingRifle);
        animator.SetBool(IsJackHash, isJack);
        animator.SetBool(IsBurpeeHash, isBurpee);
        animator.SetBool(IsPushupHash, isPushup);
    }

    void Update()
    {
        if (campActivity != CampActivity.Standard) return;

        bool onNavMesh = agent.isActiveAndEnabled && agent.isOnNavMesh;
        animator.SetBool(IsWalkingHash, onNavMesh && agent.velocity.sqrMagnitude > 0.01f);
        if (onNavMesh && patrolPoints != null && patrolPoints.Length > 1)
            UpdatePatrol();
    }

    private void UpdatePatrol()
    {
        if (Time.time < nextRouteCheck || agent.pathPending) return;
        nextRouteCheck = Time.time + repathInterval;
        if (agent.hasPath && agent.remainingDistance > agent.stoppingDistance + 0.15f) return;

        Transform point = patrolPoints[nextPatrolPoint];
        nextPatrolPoint = (nextPatrolPoint + 1) % patrolPoints.Length;
        if (point != null) agent.SetDestination(point.position);
    }
}
