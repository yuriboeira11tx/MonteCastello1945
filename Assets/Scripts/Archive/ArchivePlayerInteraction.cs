using System;
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
        [SerializeField] float reach = 2.8f;
        [SerializeField] LayerMask interactionMask = ~0;
        FieldEvidenceView fieldView;
        ArchiveInteractable focusedArchive;
        HistoricalEvidence focusedEvidence;
        bool blocked;
        int unblockedFrame;
        FieldNotebook notebook;
        public ArchiveExperience Experience => experience;
        public FieldNotebook Notebook => notebook;
        public bool IsReading => blocked;
        public event Action<HistoricalEvidence> FieldEvidenceRegistered;

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
            SetTarget(fieldTarget, archiveTarget);
            if (Keyboard.current == null) return;
            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                if (fieldTarget != null) Inspect(fieldTarget);
                else if (archiveTarget != null) experience.Interact(archiveTarget);
            }
            else if (experience != null && Keyboard.current.escapeKey.wasPressedThisFrame) experience.OpenPause();
        }

        void SetTarget(HistoricalEvidence evidence, ArchiveInteractable archiveTarget)
        {
            if (focusedEvidence == evidence && focusedArchive == archiveTarget) return;
            focusedEvidence = evidence;
            focusedArchive = archiveTarget;
            if (fieldView == null) return;
            string prompt = archiveTarget != null ? archiveTarget.Prompt : null;
            if (archiveTarget != null && archiveTarget.Action == ArchiveAction.Evidence) evidence = archiveTarget.Evidence;
            if (evidence != null) prompt = "Examinar · " + evidence.title;
            fieldView.SetPrompt(prompt == null ? null : "[E]  " + prompt);
        }

        void Inspect(HistoricalEvidence evidence)
        {
            var save = ResearchSaveStore.EnsureSession();
            OpenInspection(evidence, save.fieldEvidence.Contains(evidence.id), () =>
            {
                if (!ResearchSaveStore.RegisterFieldEvidence(evidence.id)) return false;
                FieldEvidenceRegistered?.Invoke(evidence);
                return true;
            });
        }

        public void OpenInspection(HistoricalEvidence evidence, bool registered, Func<bool> onRegister,
            bool alreadyRead = false, Func<bool> onRead = null, Action onClosed = null)
        {
            if (fieldView.IsOpen) return;
            SetReading(true);
            fieldView.Open(evidence, registered, onRegister, notebook != null ? notebook.PlayPageTurn : (Action)null,
                () =>
                {
                    if (!isActiveAndEnabled || !fieldView.isActiveAndEnabled) return;
                    SetReading(false);
                    onClosed?.Invoke();
                }, alreadyRead, onRead,
                experience != null ? "ARQUIVO HISTÓRICO" : "EVIDÊNCIA DE CAMPO",
                notebook != null ? notebook.PlayCloseSound : (Action)null);
        }

        public void OpenNotebook()
        {
            if (notebook == null) return;
            notebook.Open();
        }

        public void SetReading(bool value)
        {
            SetBlocked(value);
        }

        public void SetBlocked(bool value)
        {
            blocked = value;
            if (value) SetTarget(null, null);
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
        }

        void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
