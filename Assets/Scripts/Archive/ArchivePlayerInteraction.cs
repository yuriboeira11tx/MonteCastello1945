using StarterAssets;
using FEB.Persistence;
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
        FieldEvidenceView fieldView;
        HistoricalEvidence focusedEvidence;
        bool blocked;
        int unblockedFrame;
        FieldNotebook notebook;
        public ArchiveExperience Experience => experience;

        void Awake()
        {
            if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
            if (movement == null) movement = GetComponent<FirstPersonController>();
            if (inputs == null) inputs = GetComponent<StarterAssetsInputs>();
            notebook = GetComponentInChildren<FieldNotebook>(true);
            fieldView = GetComponentInChildren<FieldEvidenceView>(true);
        }

        void Update()
        {
            // O pesquisador também pode ser usado em cenas sem o arquivo histórico.
            if (blocked || Time.frameCount <= unblockedFrame) return;
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
            {
                OpenNotebook();
                return;
            }
            if (viewCamera == null) return;
            ArchiveInteractable archiveTarget = null;
            HistoricalEvidence fieldTarget = null;
            if (Physics.Raycast(viewCamera.transform.position, viewCamera.transform.forward, out var hit, reach, interactionMask, QueryTriggerInteraction.Collide))
            {
                if (fieldView != null) fieldTarget = hit.collider.GetComponentInParent<FieldEvidenceTarget>()?.Evidence;
                if (experience != null && fieldTarget == null) archiveTarget = hit.collider.GetComponentInParent<ArchiveInteractable>();
            }
            SetFieldTarget(fieldTarget);
            if (view != null) view.SetInteraction(archiveTarget != null ? "[E]  " + archiveTarget.Prompt : "");
            if (Keyboard.current == null) return;
            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                if (fieldTarget != null) Inspect(fieldTarget);
                else if (archiveTarget != null) experience.Interact(archiveTarget);
            }
            else if (experience != null && Keyboard.current.escapeKey.wasPressedThisFrame) experience.OpenPause();
        }

        void SetFieldTarget(HistoricalEvidence evidence)
        {
            if (focusedEvidence == evidence) return;
            focusedEvidence = evidence;
            if (fieldView != null) fieldView.SetPrompt(evidence != null ? "[E]  Examinar · " + evidence.title : null);
        }

        void Inspect(HistoricalEvidence evidence)
        {
            var save = ResearchSaveStore.EnsureSession();
            SetReading(true);
            fieldView.Open(evidence, save.fieldEvidence.Contains(evidence.id),
                () => ResearchSaveStore.RegisterFieldEvidence(evidence.id), () => SetReading(false));
        }

        public void OpenNotebook()
        {
            if (notebook == null) return;
            if (view != null && view.IsOpen) view.Close();
            notebook.Open();
        }

        public void SetReading(bool value)
        {
            if (view != null) view.SetHudVisible(!value);
            SetBlocked(value);
        }

        public void SetBlocked(bool value)
        {
            blocked = value;
            if (value) SetFieldTarget(null);
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
