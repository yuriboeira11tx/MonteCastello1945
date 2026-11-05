using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class SoldierAI : MonoBehaviour
{
    public Transform player;
    public NavMeshAgent agent;
    public bool isStandingRifle;

    [SerializeField] private Animator animator;
    [SerializeField, Min(0.05f)] private float repathInterval = 0.2f;

    private static readonly int IsWalkingHash = Animator.StringToHash("isWalking");
    private static readonly int IsStandingRifleHash = Animator.StringToHash("isStandingRifle");
    private float nextRepathTime;
    private Vector3 lastDestination;
    private bool hasDestination;

    void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        animator.SetBool(IsStandingRifleHash, isStandingRifle);
    }

    void Update()
    {
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
        {
            hasDestination = false;
            SetWalking(false);
            return;
        }

        if (player == null)
        {
            if (agent.hasPath) agent.ResetPath();
            hasDestination = false;
            SetWalking(false);
            return;
        }

        // Atualiza o destino periodicamente para acompanhar o jogador em movimento.
        if (Time.time >= nextRepathTime)
        {
            // Só recalcula quando o alvo mudou, preservando a frenagem na chegada.
            if (!hasDestination || (player.position - lastDestination).sqrMagnitude > 0.01f)
            {
                hasDestination = agent.SetDestination(player.position);
                if (hasDestination) lastDestination = player.position;
            }
            nextRepathTime = Time.time + repathInterval;
        }

        SetWalking(agent.velocity.sqrMagnitude > 0.01f);
    }

    void OnDisable()
    {
        hasDestination = false;
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
            agent.ResetPath();

        SetWalking(false);
    }

    void SetWalking(bool walking)
    {
        if (animator == null)
            return;

        if (animator.runtimeAnimatorController != null)
        {
            animator.SetBool(IsWalkingHash, walking);
        }
    }
}
