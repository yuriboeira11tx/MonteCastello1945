using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Animator), typeof(AudioSource))]
[DisallowMultipleComponent]
public sealed class SoldierFootsteps : MonoBehaviour
{
    [SerializeField] private FootstepSounds sounds;

    private AudioSource footsteps;
    private FootstepSounds.PlaybackHistory history;
    private NavMeshAgent agent;
    private float nextLeftContactTime;
    private float nextRightContactTime;

    private void Awake()
    {
        footsteps = GetComponent<AudioSource>();
        agent = GetComponentInParent<NavMeshAgent>();
    }

    public void PlayLeftFootstep(AnimationEvent contact)
    {
        if (IsWalking()) PlayContact(contact, true, 1f);
    }

    public void PlayRightFootstep(AnimationEvent contact)
    {
        if (IsWalking()) PlayContact(contact, false, 1f);
    }

    public void PlayLeftLanding(AnimationEvent contact)
    {
        PlayContact(contact, true, contact.floatParameter);
    }

    public void PlayRightLanding(AnimationEvent contact)
    {
        PlayContact(contact, false, contact.floatParameter);
    }

    private bool IsWalking()
    {
        return agent && agent.isActiveAndEnabled && agent.isOnNavMesh && agent.velocity.sqrMagnitude >= 0.01f;
    }

    private void PlayContact(AnimationEvent contact, bool isLeftFoot, float intensity)
    {
        if (!isActiveAndEnabled || sounds == null || contact.animatorClipInfo.weight < 0.45f) return;

        // Filtra eventos duplicados de um pé durante transições, permitindo pousos com os dois.
        if (Time.time < (isLeftFoot ? nextLeftContactTime : nextRightContactTime)) return;
        float nextContactTime = Time.time + 0.15f;
        if (isLeftFoot) nextLeftContactTime = nextContactTime;
        else nextRightContactTime = nextContactTime;

        sounds.Play(footsteps, isLeftFoot, ref history, Mathf.Clamp01(intensity));
    }
}
