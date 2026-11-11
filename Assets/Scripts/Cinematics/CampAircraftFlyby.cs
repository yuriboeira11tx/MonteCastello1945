using System.Collections;
using UnityEngine;

namespace FEB.Cinematics
{
    [DisallowMultipleComponent]
    public sealed class CampAircraftFlyby : MonoBehaviour
    {
        [SerializeField, Tooltip("Pivô móvel separado do centro da rota. O eixo +Z aponta para a proa em voo nivelado.")]
        Transform aeronave;
        [SerializeField, Tooltip("Fonte 3D no motor da aeronave. Ajuste a curva de distância no próprio Audio Source.")]
        AudioSource motor;
        [SerializeField, Tooltip("A primeira passagem aguarda o fim da conversa no jipe.")]
        CampArrival chegada;
        [SerializeField] Camera cameraDoPesquisador;
        [SerializeField, Min(1), Tooltip("Alcance da camada dos aviões. O restante da cena conserva o alcance da câmera.")]
        float alcanceVisual = 1450f;

        [Header("Horários em segundos")]
        [SerializeField] Vector2 atrasoInicial = new(7f, 14f);
        [SerializeField, Tooltip("Espera desde a saída de um avião até a entrada do próximo.")]
        Vector2 intervaloEntrePassagens = new(48f, 85f);

        [Header("Rota — a posição deste objeto é o centro na altura de voo")]
        [SerializeField, Min(1f), Tooltip("Metros por segundo.")] float velocidade = 94f;
        [SerializeField, Min(1f), Tooltip("Metros do centro a cada extremo, além do alcance do som.")] float meiaExtensao = 1550f;
        [SerializeField, Min(0f), Tooltip("Variação lateral máxima do ponto de cruzamento, em metros.")] float desvioLateral = 35f;
        [SerializeField, Range(0f, 85f), Tooltip("Variação em graus em relação ao rumo da rota.")] float variacaoDeRumo = 27f;
        [SerializeField, Min(0f), Tooltip("Altura adicional nas duas pontas da rota, acima das copas.")] float elevacaoNoHorizonte = 120f;
        [SerializeField, Tooltip("Distâncias ao centro, em metros, onde a subida começa e termina.")] Vector2 trechoDeSubida = new(200f, 1250f);

        [Header("Formação")]
        [SerializeField, Min(14f), Tooltip("Separação lateral do líder a cada ala, em metros.")] float separacaoLateral = 29f;
        [SerializeField, Min(10f), Tooltip("Distância dos alas atrás do líder, em metros.")] float recuoDosAlas = 31f;

        const int Lotacao = 3;
        readonly Transform[] avioes = new Transform[Lotacao];
        readonly AudioSource[] motores = new AudioSource[Lotacao];
        readonly Vector3[] deslocamentos = new Vector3[Lotacao];
        readonly int[] ordem = { 1, 2, 3 };
        int proximo = Lotacao;
        float volumeOriginal, pitchOriginal, alcanceOriginal;
        float[] limitesOriginais;

        void OnEnable()
        {
            if (!aeronave || aeronave == transform || !aeronave.IsChildOf(transform) ||
                !motor || !motor.transform.IsChildOf(aeronave) || !motor.clip)
            {
                Debug.LogError("Confira a aeronave, o Audio Source no motor e o clipe do sobrevoo.", this);
                return;
            }
            PrepararFormacao();
            ConfigurarVisibilidade();
            StartCoroutine(Circular());
        }

        void PrepararFormacao()
        {
            if (avioes[0])
            {
                Recolher();
                return;
            }
            aeronave.gameObject.SetActive(false);
            avioes[0] = aeronave;
            motores[0] = motor;
            volumeOriginal = motor.volume;
            pitchOriginal = motor.pitch;
            for (int i = 1; i < Lotacao; i++)
            {
                avioes[i] = Instantiate(aeronave, transform);
                avioes[i].name = $"P-47D — Ala {i}";
                motores[i] = avioes[i].GetComponentInChildren<AudioSource>(true);
            }
        }

        void ConfigurarVisibilidade()
        {
            // Somente a camada aérea ganha distância; a caderneta usa outra câmera.
            if (!cameraDoPesquisador || aeronave.gameObject.layer == 0) return;
            alcanceOriginal = cameraDoPesquisador.farClipPlane;
            limitesOriginais = cameraDoPesquisador.layerCullDistances;
            var limites = (float[])limitesOriginais.Clone();
            for (int i = 0; i < limites.Length; i++)
                limites[i] = i == aeronave.gameObject.layer ? 0f :
                    (limites[i] > 0f ? Mathf.Min(limites[i], alcanceOriginal) : alcanceOriginal);
            cameraDoPesquisador.farClipPlane = Mathf.Max(alcanceOriginal, alcanceVisual);
            cameraDoPesquisador.layerCullDistances = limites;
        }

        IEnumerator Circular()
        {
            // CampArrival decide em Start se este save ainda tem a apresentação.
            yield return null;
            while (chegada && chegada.EmAndamento) yield return null;
            yield return Esperar(atrasoInicial);

            int sentido = Random.value < .5f ? -1 : 1;
            while (true)
            {
                Vector3 rumo = Quaternion.AngleAxis(Random.Range(-variacaoDeRumo, variacaoDeRumo), Vector3.up)
                    * RumoPrincipal() * sentido;
                Vector3 lado = Vector3.Cross(Vector3.up, rumo);
                Vector3 centro = transform.position + lado * Random.Range(-desvioLateral, desvioLateral);
                int quantidade = SortearFormacao();
                IniciarPassagem(quantidade, centro, rumo);

                float percurso = 0f;
                while (percurso < 2f * meiaExtensao)
                {
                    yield return null;
                    percurso = Mathf.Min(percurso + velocidade * Time.deltaTime, 2f * meiaExtensao);
                    Posicionar(quantidade, centro, rumo, percurso - meiaExtensao);
                }
                Recolher();
                sentido = -sentido;
                yield return Esperar(intervaloEntrePassagens);
            }
        }

        int SortearFormacao()
        {
            // Em cada bloco de três passagens, o jogador vê os três tamanhos em ordem sorteada.
            if (proximo == Lotacao)
            {
                for (int i = Lotacao - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    (ordem[i], ordem[j]) = (ordem[j], ordem[i]);
                }
                proximo = 0;
            }
            return ordem[proximo++];
        }

        void IniciarPassagem(int quantidade, Vector3 centro, Vector3 rumo)
        {
            float mao = Random.value < .5f ? -1f : 1f;
            float volume = volumeOriginal / Mathf.Sqrt(quantidade);
            for (int i = 0; i < quantidade; i++)
            {
                float sinal = i == 1 ? mao : -mao;
                deslocamentos[i] = i == 0 ? Vector3.zero :
                    new Vector3(sinal * separacaoLateral, sinal * 2f, -recuoDosAlas);
            }
            Posicionar(quantidade, centro, rumo, -meiaExtensao);
            for (int i = 0; i < quantidade; i++)
            {
                avioes[i].gameObject.SetActive(true);
                AudioSource fonte = motores[i];
                fonte.volume = volume;
                fonte.pitch = i == 0 ? pitchOriginal : pitchOriginal * Random.Range(.984f, 1.016f);
                fonte.Play();
                if (i != 0 && fonte.clip.loadState == AudioDataLoadState.Loaded)
                    fonte.timeSamples = Random.Range(0, fonte.clip.samples);
            }
        }

        void Posicionar(int quantidade, Vector3 centro, Vector3 rumo, float distancia)
        {
            float inicio = Mathf.Max(0f, trechoDeSubida.x);
            float comprimento = Mathf.Max(1f, trechoDeSubida.y - inicio);
            float t = Mathf.Clamp01((Mathf.Abs(distancia) - inicio) / comprimento);
            float altura = elevacaoNoHorizonte * t * t * (3f - 2f * t);
            // A tangente da mesma curva alinha o nariz à descida e à subida.
            float inclinacao = Mathf.Sign(distancia) * elevacaoNoHorizonte * 6f * t * (1f - t) / comprimento;
            Vector3 lider = centro + rumo * distancia + Vector3.up * altura;
            Quaternion rotacao = Quaternion.LookRotation(rumo + Vector3.up * inclinacao);
            for (int i = 0; i < quantidade; i++)
                avioes[i].SetPositionAndRotation(lider + rotacao * deslocamentos[i], rotacao);
        }

        Vector3 RumoPrincipal()
        {
            Vector3 rumo = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            return rumo.sqrMagnitude > .0001f ? rumo.normalized : Vector3.forward;
        }

        static WaitForSeconds Esperar(Vector2 faixa)
        {
            float min = Mathf.Max(0f, faixa.x);
            return new WaitForSeconds(Random.Range(min, Mathf.Max(min, faixa.y)));
        }

        void Recolher()
        {
            for (int i = 0; i < Lotacao; i++)
            {
                if (motores[i] && motores[i].isActiveAndEnabled) motores[i].Stop();
                if (avioes[i] && avioes[i].gameObject.activeSelf) avioes[i].gameObject.SetActive(false);
            }
        }

        void OnDisable()
        {
            StopAllCoroutines();
            if (gameObject.activeInHierarchy) Recolher();
            if (cameraDoPesquisador && limitesOriginais != null)
            {
                cameraDoPesquisador.layerCullDistances = limitesOriginais;
                cameraDoPesquisador.farClipPlane = alcanceOriginal;
                limitesOriginais = null;
            }
        }

        void OnDrawGizmosSelected()
        {
            Vector3 centro = transform.position, eixo = RumoPrincipal() * meiaExtensao;
            Gizmos.color = new Color(.35f, .8f, .9f, .65f);
            Gizmos.DrawLine(centro - eixo, centro + eixo);
            Gizmos.DrawWireSphere(centro, 3f);
        }
    }
}
