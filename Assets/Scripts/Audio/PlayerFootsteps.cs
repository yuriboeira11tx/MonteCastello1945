using UnityEngine;
using StarterAssets;

[RequireComponent(typeof(CharacterController), typeof(AudioSource))]
[DisallowMultipleComponent]
public sealed class PlayerFootsteps : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float distancePerStep = 2.45f;
    [SerializeField] private FootstepSounds sounds;

    private CharacterController controller;
    private AudioSource footsteps;
    private FootstepSounds.PlaybackHistory history;
    private FirstPersonController movement;
    private float distanceTravelled;
    private bool nextIsLeft = true;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        footsteps = GetComponent<AudioSource>();
        movement = GetComponent<FirstPersonController>();
    }

    private void LateUpdate()
    {
        Vector3 velocity = controller.velocity;
        velocity.y = 0f;

        if (!controller.isGrounded || velocity.sqrMagnitude < 0.01f)
        {
            distanceTravelled = distancePerStep * 0.5f;
            return;
        }

        float speed = velocity.magnitude;
        distanceTravelled += speed * Time.deltaTime;
        if (distanceTravelled < distancePerStep || sounds == null) return;

        distanceTravelled %= distancePerStep;
        float effort = movement != null ? Mathf.InverseLerp(0f, movement.SprintSpeed, speed) : 1f;
        sounds.Play(footsteps, nextIsLeft, ref history, Mathf.Lerp(0.5f, 1f, effort * effort));
        nextIsLeft = !nextIsLeft;
    }
}
