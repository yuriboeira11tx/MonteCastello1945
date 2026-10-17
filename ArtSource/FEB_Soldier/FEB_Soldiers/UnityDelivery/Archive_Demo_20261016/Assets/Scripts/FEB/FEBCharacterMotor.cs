using UnityEngine;
using UnityEngine.InputSystem;

namespace MonteCastello.FEB
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FEBCharacterMotor : MonoBehaviour
    {
        public Animator animator;
        public float walkSpeed = 1.2f;
        public float runSpeed = 3.4f;
        public bool playerControlled;
        [Range(0, 3.4f)] public float previewSpeed;
        public Camera movementCamera;
        public float CurrentSpeed { get; private set; }
        public bool IsGrounded => controller != null && controller.isGrounded;
        static readonly int SpeedParameter = Animator.StringToHash("Speed");
        CharacterController controller;
        float verticalSpeed;
        bool simulatedInput;
        Vector2 simulation;
        bool simulationRun;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (!animator) animator = GetComponentInChildren<Animator>();
            if (animator) animator.applyRootMotion = false;
        }

        public void SetSimulatedInput(Vector2 input, bool running)
        {
            simulatedInput = true;
            simulation = Vector2.ClampMagnitude(input, 1);
            simulationRun = running;
        }

        public void ClearSimulatedInput() { simulatedInput = false; }

        public void ResetPosition(Vector3 position, Quaternion rotation)
        {
            if (!controller) controller = GetComponent<CharacterController>();
            controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            controller.enabled = true;
            verticalSpeed = 0;
            CurrentSpeed = 0;
        }

        void Update()
        {
            Vector3 motion = Vector3.zero;
            float targetSpeed = previewSpeed;
            if (playerControlled)
            {
                Vector2 input = Vector2.zero;
                bool running = false;
                if (simulatedInput) { input = simulation; running = simulationRun; }
                else if (Keyboard.current != null)
                {
                    var k = Keyboard.current;
                    input.x = (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1 : 0) - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1 : 0);
                    input.y = (k.wKey.isPressed || k.upArrowKey.isPressed ? 1 : 0) - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1 : 0);
                    input = Vector2.ClampMagnitude(input, 1);
                    running = k.leftShiftKey.isPressed || k.rightShiftKey.isPressed;
                }
                Camera cam = movementCamera ? movementCamera : Camera.main;
                Vector3 forward = cam ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : Vector3.forward;
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                Vector3 direction = forward * input.y + right * input.x;
                targetSpeed = input.magnitude * (running ? runSpeed : walkSpeed);
                motion = direction.normalized * targetSpeed;
                if (direction.sqrMagnitude > .001f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 540 * Time.deltaTime);
            }
            if (controller.isGrounded && verticalSpeed < 0) verticalSpeed = -2;
            verticalSpeed = Mathf.Max(verticalSpeed - 20 * Time.deltaTime, -30);
            Vector3 before = transform.position;
            controller.Move((motion + Vector3.up * verticalSpeed) * Time.deltaTime);
            CurrentSpeed = playerControlled
                ? Vector3.ProjectOnPlane(transform.position - before, Vector3.up).magnitude / Mathf.Max(Time.deltaTime, .0001f)
                : targetSpeed;
            if (animator) animator.SetFloat(SpeedParameter, CurrentSpeed, .12f, Time.deltaTime);
        }
    }
}
