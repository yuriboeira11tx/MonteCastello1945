using System.Collections;
using FEB.Persistence;
using UnityEngine;
using UnityEngine.AI;

namespace FEB.Cinematics
{
    [DisallowMultipleComponent]
    public sealed class CampPassingJeep : MonoBehaviour
    {
        [SerializeField] Transform jipe;
        [SerializeField] AudioSource motor;
        [SerializeField] GameObject modeloMotorista;
        [SerializeField] RuntimeAnimatorController animacoes;
        [SerializeField] Transform pesquisador;
        [SerializeField] CampJeepRoute via;
        [Tooltip("Tempo de jogo em segundos, contado a partir do término da conversa com o capitão.")]
        [SerializeField] Vector2 atrasoInicial = new(3f, 8f);
        [Tooltip("Segundos entre o fim de uma passagem e o começo da próxima.")]
        [SerializeField] Vector2 intervaloEntrePassagens = new(8f, 18f);
        [Tooltip("Velocidade máxima em metros por segundo.")]
        [SerializeField, Min(.1f)] float velocidade = 3f;

        CampSupplyCrewMember motorista;

        void OnEnable() => StartCoroutine(Circular());

        IEnumerator Circular()
        {
            if (!jipe || !via || !via.HasPassage || !motor || !motor.clip || !modeloMotorista ||
                !modeloMotorista.GetComponentInChildren<Animator>(true) || !animacoes)
            {
                Debug.LogError("Confira a viatura de passagem, o motorista, o motor e a via.", this);
                yield break;
            }

            yield return null;
            OcultarViatura();
            if (jipe.TryGetComponent<NavMeshObstacle>(out var obstaculo))
                obstaculo.enabled = true;
            while (!ResearchSaveStore.EnsureSession().campArrivalCompleted)
                yield return new WaitForSeconds(.5f);
            yield return Esperar(atrasoInicial);
            while (true)
            {
                yield return via.DriveThrough(jipe, velocidade, pesquisador, IniciarPassagem);
                OcultarViatura();
                yield return Esperar(intervaloEntrePassagens);
            }
        }

        void IniciarPassagem()
        {
            jipe.gameObject.SetActive(true);
            if (!motorista)
            {
                var soldado = Instantiate(modeloMotorista, transform);
                soldado.name = "Motorista da viatura de passagem";
                motorista = soldado.AddComponent<CampSupplyCrewMember>();
                motorista.Configure(soldado.GetComponentInChildren<Animator>(true), animacoes);
            }
            motorista.Board(jipe, true, Vector3.zero);
            motor.Play();
        }

        static WaitForSeconds Esperar(Vector2 intervalo)
        {
            float minimo = Mathf.Max(0f, intervalo.x);
            return new WaitForSeconds(Random.Range(minimo, Mathf.Max(minimo, intervalo.y)));
        }

        void OcultarViatura()
        {
            if (motor) motor.Stop();
            if (jipe && jipe.gameObject.activeSelf) jipe.gameObject.SetActive(false);
        }

        void OnDisable()
        {
            StopAllCoroutines();
            if (gameObject.activeInHierarchy) OcultarViatura();
        }
    }
}
