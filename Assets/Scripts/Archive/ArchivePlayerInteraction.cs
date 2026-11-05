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
        [Tooltip("Vincule nas cenas que possuem a investigação do arquivo histórico.")]
        [SerializeField] ArchiveExperience experience;
        [Tooltip("Interface opcional do arquivo histórico nesta cena.")]
        [SerializeField] ArchiveView view;
        [SerializeField] float reach = 2.8f;
        [SerializeField] LayerMask interactionMask = ~0;
        ArchiveInteractable focused;
        bool blocked;
        int unblockedFrame;

        void Awake()
        {
            if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
            if (movement == null) movement = GetComponent<FirstPersonController>();
            if (inputs == null) inputs = GetComponent<StarterAssetsInputs>();
        }

        void Update()
        {
            // O pesquisador também pode ser usado em cenas sem o arquivo histórico.
            if (experience == null || view == null || viewCamera == null) return;
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
            if (movement != null) movement.enabled = !value;
            if (inputs != null)
            {
                inputs.cursorLocked = !value;
                inputs.cursorInputForLook = !value;
                inputs.move = Vector2.zero;
                inputs.look = Vector2.zero;
                inputs.jump = inputs.sprint = false;
            }
            Cursor.lockState = value ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = value;
            if (view != null) view.SetInteraction("");
        }

        void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
