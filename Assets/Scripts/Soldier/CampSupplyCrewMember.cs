using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public sealed class CampSupplyCrewMember : MonoBehaviour
{
    private static readonly int Walking = Animator.StringToHash("isWalking");
    private static readonly int Idle = Animator.StringToHash("Base Layer.IDLE");

    private Animator actor;
    private Transform home;
    private NavMeshAgent agent;
    private NavMeshPath path;
    private FootstepSounds sounds;
    private CampArrivalActorRig rig;
    private Transform vehicle;
    private Vector3 seatHip;
    private bool atWheel;
    private bool walking;
    private bool hasDestination;
    private Vector3 destination;
    private float bestDistance;
    private float lastProgress;
    private bool pathFailed;

    private Transform cargo;
    private BoxCollider cargoCollider;
    private bool originalColliderEnabled;
    private Transform leftGrip;
    private Transform rightGrip;
    private Coroutine transfer;
    private bool dropping;
    private Vector3 dropPoint;
    private Quaternion dropRotation;

    public bool HasArrived => hasDestination && OnMesh &&
        Vector3.ProjectOnPlane(transform.position - destination, Vector3.up).sqrMagnitude
        <= Mathf.Pow(agent.stoppingDistance + .12f, 2);

    public bool PathFailed => pathFailed || (hasDestination && (!OnMesh ||
        agent.pathStatus != NavMeshPathStatus.PathComplete ||
        (!HasArrived && Time.time - lastProgress > 9f)));

    private bool OnMesh => agent && agent.isActiveAndEnabled && agent.isOnNavMesh;

    public void Configure(Animator animator, RuntimeAnimatorController controller, FootstepSounds footsteps = null)
    {
        actor = animator ? animator : GetComponentInChildren<Animator>(true);
        home = transform.parent;
        actor.runtimeAnimatorController = controller;
        actor.applyRootMotion = false;
        sounds = footsteps;
        path = new NavMeshPath();
        agent = GetComponent<NavMeshAgent>();
        if (agent) agent.enabled = false;
    }

    public void Board(Transform jeep, bool driver, Vector3 hipLocal)
    {
        if (cargo) FinishDrop();
        ReleaseRig();
        gameObject.SetActive(true);
        if (agent) agent.enabled = false;
        hasDestination = false;
        pathFailed = false;
        vehicle = jeep;
        atWheel = driver;
        seatHip = hipLocal;
        transform.SetParent(jeep, false);
        transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        walking = false;
        actor.SetBool(Walking, false);
        actor.Play(Idle, 0, 0f);
        actor.Update(0f);
        TryBuildRig();
    }

    public bool Disembark(Vector3 point, Quaternion facing)
    {
        if (!NavMesh.SamplePosition(point, out var hit, 1.35f, NavMesh.AllAreas))
            return false;

        ReleaseRig();
        vehicle = null;
        transform.SetParent(home, true);
        transform.SetPositionAndRotation(hit.position, facing);
        if (!agent)
        {
            agent = gameObject.AddComponent<NavMeshAgent>();
            agent.radius = .32f;
            agent.height = 1.78f;
            agent.speed = 1.42f;
            agent.acceleration = 4.5f;
            agent.angularSpeed = 220f;
            agent.stoppingDistance = .16f;
        }
        agent.enabled = true;
        if (!agent.isOnNavMesh || !agent.Warp(hit.position))
        {
            agent.enabled = false;
            return false;
        }
        EnsureFootsteps();
        agent.updateRotation = true;
        return true;
    }

    public bool WalkTo(Vector3 point)
    {
        hasDestination = false;
        pathFailed = true;
        if (!OnMesh) return false;
        agent.updateRotation = true;
        agent.ResetPath();
        if (!NavMesh.SamplePosition(point, out var hit, 1.35f, agent.areaMask) ||
            !agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete ||
            !agent.SetPath(path)) return false;

        destination = hit.position;
        bestDistance = float.PositiveInfinity;
        lastProgress = Time.time;
        pathFailed = false;
        hasDestination = true;
        return true;
    }

    public void Face(Vector3 point)
    {
        if (!OnMesh) return;
        Vector3 direction = Vector3.ProjectOnPlane(point - transform.position, Vector3.up);
        if (direction.sqrMagnitude < .0001f) return;
        agent.updateRotation = false;
        transform.rotation = Quaternion.LookRotation(direction);
    }

    private void Update()
    {
        bool moving = OnMesh && agent.velocity.sqrMagnitude > .01f;
        if (moving != walking)
        {
            walking = moving;
            actor.SetBool(Walking, moving);
        }
        if (hasDestination && OnMesh && agent.remainingDistance < bestDistance - .10f)
        {
            bestDistance = agent.remainingDistance;
            lastProgress = Time.time;
        }
        if (!rig && (vehicle || cargo)) TryBuildRig();
    }

    private void TryBuildRig()
    {
        // Dispose libera o componente e o RigBuilder temporário ao fim do frame.
        if (rig || actor.GetComponent<CampArrivalActorRig>() || !actor.isActiveAndEnabled) return;
        if (vehicle)
        {
            rig = atWheel ? CampArrivalActorRig.Begin(actor, vehicle)
                          : CampArrivalActorRig.BeginPassenger(actor, vehicle, seatHip);
        }
        else if (cargo)
        {
            rig = CampArrivalActorRig.Begin(actor);
            SetGrips();
        }
    }

    public bool TakeCrate(Transform crate)
    {
        if (vehicle || !OnMesh || cargo || !crate || !crate.TryGetComponent(out BoxCollider box))
            return false;
        Transform chest = actor.GetBoneTransform(HumanBodyBones.Chest);
        if (!chest) return false;
        cargo = crate;
        cargoCollider = box;
        originalColliderEnabled = box.enabled;
        box.enabled = false;
        cargo.SetParent(chest, true);
        // Mesmo encaixe das caixas dos carregadores já presentes no acampamento.
        Vector3 carryPosition = chest.InverseTransformPoint(actor.transform.TransformPoint(new Vector3(0f, .978f, .395f)));
        Quaternion carryRotation = Quaternion.Inverse(chest.rotation) * actor.transform.rotation;
        if (!leftGrip)
        {
            leftGrip = new GameObject("Pega esquerda").transform;
            rightGrip = new GameObject("Pega direita").transform;
        }
        PositionGrip(leftGrip, -1f);
        PositionGrip(rightGrip, 1f);
        if (rig) SetGrips();
        else TryBuildRig();
        transfer = StartCoroutine(MoveCrate(carryPosition, carryRotation));
        return true;
    }

    private void PositionGrip(Transform grip, float side)
    {
        grip.SetParent(cargo, false);
        Vector3 center = cargoCollider.center;
        Vector3 size = cargoCollider.size;
        grip.localPosition = center + new Vector3(side * (size.x * .5f + .025f), -size.y * .22f, -size.z * .11f);
    }

    private void SetGrips()
    {
        if (!rig) return;
        rig.SetHandTarget(true, leftGrip);
        rig.SetHandTarget(false, rightGrip);
    }

    public void DropCrate(Vector3 ground, Quaternion rotation)
    {
        if (!cargo) return;
        StopTransfer();
        dropping = true;
        dropPoint = ground;
        dropRotation = rotation;
        cargo.SetParent(null, true);
        transfer = StartCoroutine(MoveCrate(ground, rotation));
    }

    private IEnumerator MoveCrate(Vector3 endPosition, Quaternion endRotation)
    {
        Vector3 startPosition = cargo.localPosition;
        Quaternion startRotation = cargo.localRotation;
        const float duration = .42f;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            cargo.SetLocalPositionAndRotation(Vector3.Lerp(startPosition, endPosition, t),
                Quaternion.Slerp(startRotation, endRotation, t));
            yield return null;
        }
        cargo.SetLocalPositionAndRotation(endPosition, endRotation);
        transfer = null;
        if (dropping) FinishDrop();
    }

    private void StopTransfer()
    {
        if (transfer != null) StopCoroutine(transfer);
        transfer = null;
    }

    private void FinishDrop()
    {
        StopTransfer();
        if (rig)
        {
            rig.SetHandTarget(true, null);
            rig.SetHandTarget(false, null);
        }
        leftGrip.SetParent(transform, false);
        rightGrip.SetParent(transform, false);
        cargo.SetParent(null, true);
        if (dropping) cargo.SetPositionAndRotation(dropPoint, dropRotation);
        else cargo.SetPositionAndRotation(transform.position + transform.forward * .6f, transform.rotation);
        cargoCollider.enabled = originalColliderEnabled;
        dropping = false;
        cargo = null;
        cargoCollider = null;
    }

    public void Park()
    {
        if (cargo) FinishDrop();
        ReleaseRig();
        hasDestination = false;
        vehicle = null;
        if (agent) agent.enabled = false;
        transform.SetParent(home, true);
        gameObject.SetActive(false);
    }

    private void ReleaseRig()
    {
        if (rig) rig.Dispose();
        rig = null;
    }

    private void EnsureFootsteps()
    {
        if (actor.TryGetComponent<SoldierFootsteps>(out var steps))
        {
            if (sounds) steps.SetSounds(sounds);
            return;
        }
        if (!actor.TryGetComponent<AudioSource>(out var source))
            source = actor.gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.volume = .38f;
        source.priority = 110;
        source.minDistance = .85f;
        source.maxDistance = 20f;
        source.rolloffMode = AudioRolloffMode.Custom;
        source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, new AnimationCurve(
            new Keyframe(0, 1), new Keyframe(.09f, .96f), new Keyframe(.25f, .48f),
            new Keyframe(.45f, .18f), new Keyframe(.65f, .055f), new Keyframe(.85f, .012f), new Keyframe(1, 0)));
        steps = actor.gameObject.AddComponent<SoldierFootsteps>();
        steps.SetSounds(sounds);
    }
}
