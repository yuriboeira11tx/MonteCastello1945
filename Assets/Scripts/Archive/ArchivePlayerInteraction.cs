using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FEB.Archive
{
    [DisallowMultipleComponent]
    public sealed class ArchivePlayerInteraction : MonoBehaviour
    {
        [SerializeField] Camera viewCamera;
        [SerializeField] FirstPersonController movement;
        [SerializeField] StarterAssetsInputs inputs;
        [SerializeField] ArchiveExperience experience;
        [SerializeField] ArchiveView view;
        [SerializeField] float reach = 2.8f;
        [SerializeField] LayerMask interactionMask = ~0;
        ArchiveInteractable focused;
        bool blocked;
        int unblockedFrame;

        void Update()
        {
            if (blocked || Time.frameCount <= unblockedFrame) return;
            focused = null;
            if (Physics.Raycast(viewCamera.transform.position, viewCamera.transform.forward, out var hit, reach, interactionMask, QueryTriggerInteraction.Collide))
                focused = hit.collider.GetComponentInParent<ArchiveInteractable>();
            view.SetInteraction(focused != null ? "[E]  " + focused.Prompt : "");
            if (Keyboard.current == null) return;
            if (Keyboard.current.eKey.wasPressedThisFrame && focused != null) experience.Interact(focused);
            else if (Keyboard.current.tabKey.wasPressedThisFrame) experience.OpenNotebook();
            else if (Keyboard.current.escapeKey.wasPressedThisFrame) experience.OpenPause();
        }

        public void SetBlocked(bool value)
        {
            blocked = value;
            if (!value) unblockedFrame = Time.frameCount;
            movement.enabled = !value;
            inputs.cursorLocked = !value;
            inputs.cursorInputForLook = !value;
            inputs.move = Vector2.zero;
            inputs.look = Vector2.zero;
            inputs.jump = inputs.sprint = false;
            Cursor.lockState = value ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = value;
            view.SetInteraction("");
        }

        void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
