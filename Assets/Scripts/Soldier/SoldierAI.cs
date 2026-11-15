using System.Collections;
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
    public bool isAimRifle;
    [Tooltip("Dispara repetidamente enquanto estiver mirando, respeitando o cooldown.")]
    public bool isFire;

    [Header("Disparo do fuzil")]
    [SerializeField] private Transform muzzle;
    [SerializeField, Min(0.15f)] private float shotCooldown = 2f;
    [SerializeField, Min(1)] private int shotsPerClip = 8;
    [SerializeField, Min(0.15f)] private float reloadCooldown = 10f;
    [SerializeField, Min(1f)] private float shotRange = 150f;
    [SerializeField, Range(0f, 1f), Tooltip("Momento do disparo no clipe FIRERIFLE, em tempo normalizado.")]
    private float shotNormalizedTime = 0.085714f;
    [SerializeField] private LayerMask shotMask = Physics.DefaultRaycastLayers;
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private ParticleSystem muzzleSmoke;
    [SerializeField] private ParticleSystem impactEffect;
    [SerializeField] private LineRenderer tracer;
    [SerializeField] private AudioSource shotAudio;
    [SerializeField] private AudioClip clipEjectionAudio;
    [SerializeField, Min(1f)] private float tracerSpeed = 800f;

    private readonly RaycastHit[] shotHits = new RaycastHit[32];
    private float nextShotTime;
    private int shotsInClip;
    private bool firing, shotDelivered;
    private Coroutine tracerRoutine;

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
    private static readonly int IsAimRifle = Animator.StringToHash("isAimRifle");
    private static readonly int ShootHash = Animator.StringToHash("shoot");
    private static readonly int CanShootHash = Animator.StringToHash("canShoot");
    private static readonly int AimStateHash = Animator.StringToHash("Base Layer.AIMRIFLE");
    private static readonly int FireStateHash = Animator.StringToHash("Base Layer.FIRERIFLE");


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

        animator.SetBool(IsStandingRifleHash, isStandingRifle || isAimRifle);
        animator.SetBool(IsJackHash, isJack);
        animator.SetBool(IsBurpeeHash, isBurpee);
        animator.SetBool(IsPushupHash, isPushup);
        animator.SetBool(IsAimRifle, isAimRifle);
    }

    void Update()
    {
        if (campActivity != CampActivity.Standard) return;

        bool onNavMesh = agent.isActiveAndEnabled && agent.isOnNavMesh;
        animator.SetBool(IsWalkingHash, onNavMesh && agent.velocity.sqrMagnitude > 0.01f);
        animator.SetBool(IsAimRifle, isAimRifle);
        animator.SetBool(IsStandingRifleHash, isStandingRifle || isAimRifle);
        UpdateShooting();
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

    private void UpdateShooting()
    {
        if (!muzzle) return;
        bool ready = Time.time >= nextShotTime;
        animator.SetBool(CanShootHash, ready);
        if (!ready || !isAimRifle) animator.ResetTrigger(ShootHash);

        var state = animator.GetCurrentAnimatorStateInfo(0);
        var incoming = animator.GetNextAnimatorStateInfo(0);
        bool enteringFire = animator.IsInTransition(0) && incoming.fullPathHash == FireStateHash;
        bool inFire = state.fullPathHash == FireStateHash || enteringFire;
        if (!inFire)
        {
            firing = shotDelivered = false;
            if (isFire) TryShoot();
            return;
        }

        if (!firing)
        {
            firing = true;
            shotDelivered = false;
        }
        float progress = enteringFire ? incoming.normalizedTime : state.normalizedTime;
        if (shotDelivered || progress < shotNormalizedTime) return;
        shotDelivered = true;
        if (ready && isAimRifle && muzzle) FireShot();
    }

    public bool TryShoot()
    {
        if (!isActiveAndEnabled || campActivity != CampActivity.Standard || !isAimRifle ||
            !muzzle || Time.time < nextShotTime || animator.IsInTransition(0) ||
            animator.GetCurrentAnimatorStateInfo(0).fullPathHash != AimStateHash) return false;
        animator.SetTrigger(ShootHash);
        return true;
    }

    private void FireShot()
    {
        shotsInClip++;
        bool emptyClip = shotsInClip >= shotsPerClip;
        nextShotTime = Time.time + (emptyClip ? reloadCooldown : shotCooldown);
        animator.SetBool(CanShootHash, false);
        animator.ResetTrigger(ShootHash);
        if (muzzleFlash) muzzleFlash.Play(true);
        if (muzzleSmoke) muzzleSmoke.Emit(3);
        if (shotAudio && shotAudio.clip) shotAudio.PlayOneShot(shotAudio.clip);
        if (emptyClip)
        {
            if (shotAudio && clipEjectionAudio) shotAudio.PlayOneShot(clipEjectionAudio);
            shotsInClip = 0;
        }

        Vector3 origin = muzzle.position;
        Vector3 direction = muzzle.forward;
        int count = Physics.RaycastNonAlloc(origin, direction, shotHits, shotRange, shotMask,
            QueryTriggerInteraction.Ignore);
        var hits = shotHits;
        if (count == shotHits.Length)
        {
            hits = Physics.RaycastAll(origin, direction, shotRange, shotMask, QueryTriggerInteraction.Ignore);
            count = hits.Length;
        }
        RaycastHit closest = default;
        float distance = shotRange;
        bool hasHit = false;
        for (int i = 0; i < count; i++)
        {
            if (hits[i].collider.transform.IsChildOf(transform) || hits[i].distance > distance) continue;
            closest = hits[i];
            distance = closest.distance;
            hasHit = true;
        }
        Vector3 end = hasHit ? closest.point : origin + direction * shotRange;
        if (tracerRoutine != null) StopCoroutine(tracerRoutine);
        tracerRoutine = StartCoroutine(ShowShot(origin, end, hasHit, closest.normal));
    }

    private IEnumerator ShowShot(Vector3 origin, Vector3 end, bool hasHit, Vector3 normal)
    {
        float duration = Mathf.Clamp(Vector3.Distance(origin, end) / tracerSpeed, 0.03f, 0.1f);
        if (tracer)
        {
            tracer.enabled = true;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float t = Mathf.Clamp01(elapsed / duration);
                tracer.SetPosition(0, Vector3.Lerp(origin, end, Mathf.Max(0f, t - 0.2f)));
                tracer.SetPosition(1, Vector3.Lerp(origin, end, Mathf.Min(1f, t + 0.2f)));
                yield return null;
            }
            tracer.enabled = false;
        }
        if (hasHit && impactEffect)
        {
            impactEffect.transform.SetPositionAndRotation(end + normal * 0.015f, Quaternion.LookRotation(normal));
            impactEffect.Emit(18);
        }
        tracerRoutine = null;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        tracerRoutine = null;
        firing = shotDelivered = false;
        if (tracer) tracer.enabled = false;
        if (muzzleFlash) muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (muzzleSmoke) muzzleSmoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (impactEffect) impactEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (shotAudio) shotAudio.Stop();
    }
}
