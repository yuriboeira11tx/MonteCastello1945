using System.Collections;
using FEB.Archive;
using FEB.Audio;
using FEB.Persistence;
using UnityEngine;
using UnityEngine.AI;

namespace FEB.Cinematics
{
    [DisallowMultipleComponent]
    public sealed class CampArrival : MonoBehaviour
    {
        [Header("Elenco e viatura")]
        [SerializeField] ArchivePlayerInteraction pesquisador;
        [SerializeField] SoldierAI motorista, capitao;
        [SerializeField] Transform jipe, inicioDoTrajeto;
        [SerializeField] Camera cameraDoPassageiro;
        [SerializeField] SceneDialogue vozes;
        [Header("Som e diálogo")]
        [SerializeField] AudioSource motor, efeitos;
        [SerializeField] AudioClip papel;
        [SerializeField] FootstepSounds passos;
        [SerializeField] CampArrivalDialogue dialogo = new();
        [Header("Encenação")]
        [SerializeField, Min(0), Tooltip("Tempo em tela preta com o motor audível antes de revelar o trajeto.")]
        float segundosIniciaisNoEscuro = 3f;
        [SerializeField, Min(1)] float segundosDeTrajeto = 7f;
        [SerializeField] Transform credencial, pontoDeEntrega;

        Actor driver, captain;
        Transform playerParent;
        Pose parking, originalPlayer, originalCamera;
        CharacterController capsule;
        bool active, seated, parked, capsuleEnabled;
        public bool EmAndamento => active;
        float blackout, engineVolume, enginePitch, originalFov;
        Vector3 enginePosition;
        int speaking = -1;
        FootstepSounds.PlaybackHistory stepHistory;
        static readonly Vector3 PassengerEye = new(.365f, 1.48f, -.36f);
        static readonly Vector3 CaptainMark = new(-1.43f, 0, .42f);

        IEnumerator Start()
        {
            if (ResearchSaveStore.EnsureSession().campArrivalCompleted) yield break;
            if (!pesquisador || !motorista || !capitao || !jipe || !inicioDoTrajeto || !cameraDoPassageiro ||
                !vozes || !vozes.VoiceSource || !motor || !motor.clip || !efeitos || !credencial || !pontoDeEntrega)
            {
                Debug.LogError("Confira as referências da chegada ao acampamento.", this);
                yield break;
            }
            active = true;
            parking = new Pose(jipe.position, jipe.rotation);
            engineVolume = motor.volume;
            enginePitch = motor.pitch;
            enginePosition = motor.transform.localPosition;
            var player = pesquisador.transform;
            playerParent = player.parent;
            originalPlayer = new Pose(player.position, player.rotation);
            var cameraTransform = cameraDoPassageiro.transform;
            originalCamera = new Pose(cameraTransform.localPosition, cameraTransform.localRotation);
            originalFov = cameraDoPassageiro.fieldOfView;
            capsule = pesquisador.GetComponent<CharacterController>();
            capsuleEnabled = capsule.enabled;
            pesquisador.SetBlocked(true);
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            capsule.enabled = false;
            dialogo.Begin(vozes);
            SetBlack(1);

            driver = new Actor(motorista);
            captain = new Actor(capitao);
            var captainPosition = jipe.TransformPoint(CaptainMark);
            captain.Place(captainPosition - jipe.right * 1.35f, Quaternion.LookRotation(jipe.right));
            captain.Rig = CampArrivalActorRig.Begin(captain.Animator);
            motorista.transform.SetParent(jipe, false);
            motorista.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            driver.Rig = CampArrivalActorRig.Begin(driver.Animator, jipe);
            player.SetParent(jipe, false);
            player.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            cameraTransform.SetLocalPositionAndRotation(PassengerEye, Quaternion.Euler(3, -22, 0));
            cameraDoPassageiro.fieldOfView = 68;
            seated = true;

            yield return Arrive();
            driver.Rig.SetLookTarget(captain.Rig.Eyes);
            captain.Rig.SetLookTarget(driver.Rig.Eyes);
            yield return WalkUp();
            yield return Say(0, driver);
            yield return Say(1, captain);
            var exchange = StartCoroutine(PresentPapers());
            yield return Say(2, driver);
            yield return exchange;
            if (papel) efeitos.PlayOneShot(papel, .46f);
            captain.Rig.SetLookTarget(credencial);
            yield return new WaitForSeconds(1.65f);
            captain.Rig.SetLookTarget(driver.Rig.Eyes);
            yield return Say(3, captain);
            yield return Fade(1, .5f, fadeEngine: true);
            Disembark(true);
            yield return Fade(0, .95f);
            Release();
            ResearchSaveStore.CompleteCampArrival();
        }

        IEnumerator Arrive()
        {
            Vector3 a = inicioDoTrajeto.position;
            Vector3 b = a + inicioDoTrajeto.forward * 6;
            Vector3 d = parking.position;
            Vector3 c = d - parking.rotation * Vector3.forward * 5;
            var engineMount = new Vector3(0, .78f, .85f);
            jipe.SetPositionAndRotation(a, inicioDoTrajeto.rotation);
            if (jipe.TryGetComponent<WillysWheelAnimation>(out var rodas)) rodas.SincronizarPose();
            motor.transform.position = jipe.TransformPoint(engineMount);
            motor.volume = engineVolume * .65f;
            motor.pitch = enginePitch;
            motor.Play();
            float travel = Mathf.Max(1f, segundosDeTrajeto);
            float dark = Mathf.Max(0f, segundosIniciaisNoEscuro);
            // A viatura começa a rodar ainda no escuro para que já haja movimento ao abrir a imagem.
            float rollIn = Mathf.Min(.8f, dark, travel * .15f);
            if (dark > rollIn) yield return new WaitForSeconds(dark - rollIn);
            float reveal = Mathf.Min(1f, travel - rollIn);
            for (float elapsed = 0; elapsed < travel; elapsed += Time.deltaTime)
            {
                float t = Mathf.SmoothStep(0, 1, elapsed / travel);
                Vector3 ab = Vector3.Lerp(a, b, t), bc = Vector3.Lerp(b, c, t), cd = Vector3.Lerp(c, d, t);
                Vector3 lead = Vector3.Lerp(bc, cd, t), tail = Vector3.Lerp(ab, bc, t);
                jipe.SetPositionAndRotation(Vector3.Lerp(tail, lead, t), Quaternion.LookRotation(lead - tail));
                motor.transform.position = jipe.TransformPoint(engineMount);
                float underway = Mathf.Sin(t * Mathf.PI);
                motor.volume = engineVolume * Mathf.Lerp(.65f, 1, Mathf.Clamp01(elapsed / .8f));
                motor.pitch = enginePitch * (1f + .11f * underway);
                cameraDoPassageiro.transform.localRotation = Quaternion.Euler(3, Mathf.Lerp(-22, -62, underway), 0);
                SetBlack(1f - Mathf.SmoothStep(0, 1, (elapsed - rollIn) / reveal));
                yield return null;
            }
            SetBlack(0);
            jipe.SetPositionAndRotation(parking.position, parking.rotation);
            parked = true;
            motor.transform.position = jipe.TransformPoint(engineMount);
            for (float elapsed = 0; elapsed < .65f; elapsed += Time.deltaTime)
            {
                motor.volume = engineVolume * Mathf.SmoothStep(1f, .65f, elapsed / .65f);
                yield return null;
            }
            motor.pitch = enginePitch;
            motor.volume = engineVolume * .65f;
        }

        IEnumerator WalkUp()
        {
            const float blendDuration = .12f;
            var end = jipe.TransformPoint(CaptainMark);
            var start = end - jipe.right * 1.35f;
            var forward = Quaternion.LookRotation(end - start);
            var rest = Quaternion.LookRotation(jipe.TransformDirection(new Vector3(.85f, 0, -.7f)));
            captain.Animator.SetBool("isWalking", true);
            captain.Animator.CrossFadeInFixedTime("Base Layer.WALK", blendDuration, 0, 0f);
            bool secondStep = false;
            efeitos.transform.position = start;
            if (passos) passos.Play(efeitos, true, ref stepHistory, .75f);
            for (float elapsed = 0; elapsed < 1.25f; elapsed += Time.deltaTime)
            {
                float t = Mathf.SmoothStep(0, 1, elapsed / 1.25f);
                captain.Place(Vector3.Lerp(start, end, t), Quaternion.Slerp(forward, rest, t));
                efeitos.transform.position = capitao.transform.position;
                if (!secondStep && t > .55f)
                {
                    if (passos) passos.Play(efeitos, false, ref stepHistory, .8f);
                    secondStep = true;
                }
                yield return null;
            }
            captain.Place(end, rest);
            captain.Animator.SetBool("isWalking", false);
            captain.Animator.CrossFadeInFixedTime("Base Layer.IDLE", blendDuration, 0, 0f);
        }

        IEnumerator Say(int index, Actor speaker)
        {
            speaking = index;
            yield return dialogo.Play(index, speaker.Rig.Eyes);
            speaking = -1;
            yield return new WaitForSeconds(.26f);
        }

        IEnumerator PresentPapers()
        {
            Transform left = driver.Animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform right = captain.Animator.GetBoneTransform(HumanBodyBones.RightHand);
            AttachPaper(left, -jipe.right);
            credencial.gameObject.SetActive(true);
            driver.Rig.SetHandTarget(true, pontoDeEntrega);
            captain.Rig.SetHandTarget(false, pontoDeEntrega);
            yield return new WaitForSeconds(.75f);
            if (papel) efeitos.PlayOneShot(papel, .32f);
            AttachPaper(right, jipe.right);
            driver.Rig.SetHandTarget(true, null);
            // Trazer a credencial até o peito para conferir nome e assinatura.
            Vector3 from = pontoDeEntrega.position;
            Vector3 to = capitao.transform.TransformPoint(new Vector3(.15f, 1.18f, .28f));
            for (float elapsed = 0; elapsed < .6f; elapsed += Time.deltaTime)
            {
                pontoDeEntrega.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, elapsed / .6f));
                yield return null;
            }
            pontoDeEntrega.position = to;
        }

        void AttachPaper(Transform hand, Vector3 direction)
        {
            credencial.SetParent(hand, true);
            credencial.SetPositionAndRotation(hand.position + direction * .055f,
                jipe.rotation * Quaternion.Euler(11, 15, 0));
        }

        void LateUpdate()
        {
            if (!parked || driver?.Rig == null || captain?.Rig == null) return;
            var eye = cameraDoPassageiro.transform;
            // O motorista indica o passageiro ao mencioná-lo e volta a falar com o capitão.
            bool presentingPassenger = (speaking == 0 || speaking == 2) && dialogo.CaptionIndex == 1;
            driver.Rig.SetLookTarget(presentingPassenger ? eye : captain.Rig.Eyes);
            if (speaking == 3)
                captain.Rig.SetLookTarget(dialogo.CaptionIndex >= 1 ? eye : driver.Rig.Eyes);
            bool captainSpeaking = speaking == 1 || speaking == 3;
            float bias = captainSpeaking ? .82f : .22f;
            Vector3 mark = Vector3.Lerp(driver.Rig.Eyes.position, captain.Rig.Eyes.position, bias);
            mark.y -= .095f;
            Quaternion target = Quaternion.LookRotation(mark - eye.position, Vector3.up);
            eye.rotation = Quaternion.Slerp(eye.rotation, target, 1 - Mathf.Exp(-3.2f * Time.deltaTime));
        }

        void SetBlack(float value)
        {
            blackout = value;
            dialogo.SetBlackout(value);
        }

        IEnumerator Fade(float end, float duration, bool fadeEngine = false)
        {
            float from = blackout;
            float fromVolume = motor.volume;
            for (float elapsed = 0; elapsed < duration; elapsed += Time.deltaTime)
            {
                float t = Mathf.SmoothStep(0, 1, elapsed / duration);
                SetBlack(Mathf.Lerp(from, end, t));
                if (fadeEngine) motor.volume = Mathf.Lerp(fromVolume, 0f, t);
                yield return null;
            }
            if (fadeEngine) motor.volume = 0f;
            SetBlack(end);
        }

        void Disembark(bool finished)
        {
            seated = parked = false;
            if (motor) motor.Stop();
            if (efeitos) efeitos.Stop();
            credencial.gameObject.SetActive(false);
            credencial.SetParent(transform, true);
            jipe.SetPositionAndRotation(parking.position, parking.rotation);
            if (jipe.TryGetComponent<WillysWheelAnimation>(out var rodas)) rodas.SincronizarPose();
            driver?.Restore(finished ? jipe.TransformPoint(new Vector3(-1.34f, 0, -.65f)) : (Vector3?)null);
            captain?.Restore(finished ? jipe.TransformPoint(CaptainMark) : (Vector3?)null);
            driver = captain = null;
            var player = pesquisador.transform;
            player.SetParent(playerParent, true);
            var exit = jipe.TransformPoint(new Vector3(1.57f, 0, -.35f));
            var leaveAt = finished && NavMesh.SamplePosition(exit, out var hit, 1.2f, NavMesh.AllAreas)
                ? hit.position + Vector3.up * .04f : originalPlayer.position;
            player.SetPositionAndRotation(leaveAt, finished ? Quaternion.LookRotation(-jipe.forward) : originalPlayer.rotation);
            cameraDoPassageiro.transform.SetLocalPositionAndRotation(originalCamera.position, originalCamera.rotation);
            cameraDoPassageiro.fieldOfView = originalFov;
            capsule.enabled = capsuleEnabled;
        }

        void Release()
        {
            active = false;
            if (motor)
            {
                motor.Stop();
                motor.volume = engineVolume;
                motor.pitch = enginePitch;
                motor.transform.localPosition = enginePosition;
            }
            dialogo.End();
            if (pesquisador) pesquisador.SetBlocked(false);
        }

        void OnDisable()
        {
            if (!active) return;
            StopAllCoroutines();
            // During scene teardown Unity may already have destroyed the player or vehicle.
            if (seated && pesquisador && jipe && cameraDoPassageiro && credencial) Disembark(false);
            Release();
        }

        sealed class Actor
        {
            readonly SoldierAI ai;
            readonly NavMeshAgent agent;
            readonly Transform parent;
            readonly Pose home;
            readonly bool resumeAI, resumeAgent;
            public readonly Animator Animator;
            public CampArrivalActorRig Rig;

            public Actor(SoldierAI soldier)
            {
                ai = soldier;
                agent = ai.GetComponent<NavMeshAgent>();
                Animator = ai.GetComponentInChildren<Animator>();
                parent = ai.transform.parent;
                home = new Pose(ai.transform.position, ai.transform.rotation);
                resumeAI = ai.enabled;
                resumeAgent = agent.enabled;
                ai.enabled = agent.enabled = false;
                Animator.SetBool("isWalking", false);
                Animator.SetBool("isStandingRifle", false);
                Animator.Play("Base Layer.IDLE", 0, 0);
                Animator.Update(0);
            }

            public void Place(Vector3 position, Quaternion rotation) =>
                ai.transform.SetPositionAndRotation(position, rotation);

            public void Restore(Vector3? leaveAt)
            {
                if (!ai) return;
                Rig?.Dispose();
                ai.transform.SetParent(parent, true);
                Vector3 at = home.position;
                if (leaveAt.HasValue && NavMesh.SamplePosition(leaveAt.Value, out var hit, 1.25f, NavMesh.AllAreas))
                    at = hit.position;
                Place(at, home.rotation);
                Animator.SetBool("isWalking", false);
                Animator.SetBool("isStandingRifle", ai.isStandingRifle);
                agent.enabled = resumeAgent;
                if (resumeAgent && agent.isOnNavMesh && leaveAt.HasValue) agent.SetDestination(home.position);
                ai.enabled = resumeAI;
            }
        }
    }
}
