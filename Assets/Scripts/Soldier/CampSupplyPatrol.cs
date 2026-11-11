using System.Collections;
using FEB.Cinematics;
using FEB.Persistence;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public sealed class CampSupplyPatrol : MonoBehaviour
{
    [Header("Viatura e guarnição")]
    [SerializeField] Transform jipe;
    [SerializeField] GameObject[] modelosDosSoldados;
    [SerializeField] RuntimeAnimatorController animacoes;
    [SerializeField] FootstepSounds passos;
    [SerializeField] AudioSource motor;
    [SerializeField] NavMeshObstacle obstaculoEstacionado;
    [SerializeField] Transform pesquisador;

    [Header("Abastecimento")]
    [SerializeField] GameObject caixaDeSuprimentos;
    [Tooltip("A seta azul aponta do lugar onde o soldado para para o lugar da caixa.")]
    [SerializeField] Transform[] pontosDeEntrega;
    [SerializeField] CampJeepRoute[] vias;
    [Tooltip("Tempo de jogo, em segundos, entre a saída da patrulha e a próxima visita.")]
    [SerializeField] Vector2 intervaloEntreVisitas = new(35f, 75f);
    [SerializeField, Min(1f)] float velocidadeDoJipe = 5.5f;

    readonly CampSupplyCrewMember[] equipe = new CampSupplyCrewMember[3];
    readonly Transform[] carga = new Transform[2];
    readonly int[] destinos = { -1, -1 };
    readonly int[] prateleira = new int[2];
    Transform[,] estoque;
    int[] proximaVaga;
    int ultimaEntrada = -1;

    static readonly Vector3 BancoDianteiro = new(.355f, .835f, -.46f);
    static readonly Vector3 BancoTraseiro = new(-.20f, .870f, -1.29f);
    static Vector3 Banco(int indice) => indice == 1 ? BancoDianteiro : BancoTraseiro;
    static Vector3 Porta(int ajudante) => ajudante == 0 ? new Vector3(1.35f, 0, -.44f) : new Vector3(-1.35f, 0, -1.07f);
    static Vector3 CargaLocal(int ajudante) => new(.40f, .928f + (1 - ajudante) * .381f, -1.17f);

    void OnEnable() => StartCoroutine(Patrulhar());

    IEnumerator Patrulhar()
    {
        if (!jipe || !motor || !obstaculoEstacionado || !animacoes || !caixaDeSuprimentos ||
            modelosDosSoldados == null || modelosDosSoldados.Length != 3 ||
            System.Array.Exists(modelosDosSoldados, modelo => !modelo || !modelo.GetComponentInChildren<Animator>(true)) ||
            vias == null || vias.Length == 0 || System.Array.Exists(vias, v => !v || !v.IsValid(true) || !v.IsValid(false)) ||
            pontosDeEntrega == null || pontosDeEntrega.Length < 2 || System.Array.Exists(pontosDeEntrega, ponto => !ponto))
        {
            Debug.LogError("Confira a viatura, a tripulação, as vias e os pontos de entrega da patrulha.", this);
            yield break;
        }

        if (estoque == null)
        {
            estoque = new Transform[pontosDeEntrega.Length, 2];
            proximaVaga = new int[pontosDeEntrega.Length];
        }
        while (!ResearchSaveStore.EnsureSession().campArrivalCompleted) yield return new WaitForSeconds(.5f);
        while (true)
        {
            float minimo = Mathf.Max(0f, intervaloEntreVisitas.x);
            yield return new WaitForSeconds(Random.Range(minimo, Mathf.Max(minimo, intervaloEntreVisitas.y)));
            yield return Visitar();
        }
    }

    IEnumerator Visitar()
    {
        int entrada = EscolherVia(ultimaEntrada);
        // Sorteios separados permitem regressar pela mesma estrada ou seguir para a outra.
        int saida = Random.value < .67f ? EscolherVia(entrada) : entrada;
        ultimaEntrada = entrada;
        if (!vias[entrada].TryGetStartPose(true, out Pose inicio)) yield break;
        jipe.SetPositionAndRotation(inicio.position, inicio.rotation);
        obstaculoEstacionado.enabled = false;
        jipe.gameObject.SetActive(true);
        PrepararEquipe();
        CarregarViatura();
        motor.pitch = 1.07f;
        motor.Play();

        yield return vias[entrada].Drive(jipe, true, velocidadeDoJipe, pesquisador);
        motor.pitch = .93f;
        obstaculoEstacionado.enabled = true;
        yield return new WaitForSeconds(1f);
        Coroutine primeiraEntrega = StartCoroutine(Descarregar(0));
        yield return new WaitForSeconds(1.1f);
        yield return Descarregar(1);
        yield return primeiraEntrega;
        yield return new WaitForSeconds(.75f);
        obstaculoEstacionado.enabled = false;
        motor.pitch = 1.07f;
        yield return vias[saida].Drive(jipe, false, velocidadeDoJipe, pesquisador);
        RecolherViatura();
    }

    int EscolherVia(int anterior)
    {
        if (anterior < 0 || vias.Length == 1) return Random.Range(0, vias.Length);
        int escolha = Random.Range(0, vias.Length - 1);
        return escolha >= anterior ? escolha + 1 : escolha;
    }

    void PrepararEquipe()
    {
        for (int i = 0; i < equipe.Length; i++)
        {
            if (!equipe[i])
            {
                var soldado = Instantiate(modelosDosSoldados[i], transform);
                soldado.name = i == 0 ? "Motorista do abastecimento" : "Auxiliar de descarga " + i;
                equipe[i] = soldado.AddComponent<CampSupplyCrewMember>();
                equipe[i].Configure(soldado.GetComponentInChildren<Animator>(true), animacoes, passos);
            }
            equipe[i].Board(jipe, i == 0, Banco(i));
        }
    }

    int SortearEntrega(int outro, int anteriorA, int anteriorB)
    {
        int melhor = -1, prioridade = -1, candidatos = 0;
        for (int i = 0; i < pontosDeEntrega.Length; i++)
        {
            if (i == outro) continue;
            int valor = i != anteriorA && i != anteriorB ? 2 : 0;
            if (!estoque[i, proximaVaga[i]]) valor++;
            if (valor < prioridade) continue;
            if (valor > prioridade) { prioridade = valor; candidatos = 0; }
            if (Random.Range(0, ++candidatos) == 0) melhor = i;
        }
        return melhor;
    }

    void CarregarViatura()
    {
        int anteriorA = destinos[0], anteriorB = destinos[1];
        for (int i = 0; i < carga.Length; i++)
        {
            int setor = SortearEntrega(i == 0 ? -1 : destinos[0], anteriorA, anteriorB);
            destinos[i] = setor;
            int vaga = prateleira[i] = proximaVaga[setor];
            Transform caixa = estoque[setor, vaga];
            estoque[setor, vaga] = null;
            if (!caixa) caixa = carga[i] ? carga[i] : Instantiate(caixaDeSuprimentos, jipe).transform;
            else if (carga[i]) Destroy(carga[i].gameObject);
            carga[i] = caixa;
            AcomodarCaixa(i);
        }
    }

    void AcomodarCaixa(int i)
    {
        carga[i].SetParent(jipe, false);
        carga[i].SetLocalPositionAndRotation(CargaLocal(i), Quaternion.identity);
        carga[i].GetComponent<BoxCollider>().enabled = false;
    }

    IEnumerator Descarregar(int i)
    {
        CampSupplyCrewMember soldado = equipe[i + 1];
        Vector3 porta = jipe.TransformPoint(Porta(i));
        if (!soldado.Disembark(porta, jipe.rotation * Quaternion.Euler(0, i == 0 ? 90 : -90, 0)))
        {
            soldado.Board(jipe, false, Banco(i + 1));
            yield break;
        }
        // Um soldado tira a caixa de cima pela lateral. O outro alcança a de baixo pela traseira.
        Vector3 coleta = jipe.TransformPoint(i == 0 ? new Vector3(1.38f, 0, -1.28f) : new Vector3(.28f, 0, -2.30f));
        yield return Caminhar(soldado, coleta);
        if (soldado.HasArrived)
        {
            soldado.Face(carga[i].position);
            yield return new WaitForSeconds(.38f);
            if (soldado.TakeCrate(carga[i]))
            {
                yield return new WaitForSeconds(.65f);
                Transform ponto = pontosDeEntrega[destinos[i]];
                float lateral = prateleira[i] == 0 ? -.37f : .37f;
                Vector3 deposito = ponto.TransformPoint(new Vector3(lateral, 0, 0));
                Vector3 aproximacao = deposito - ponto.forward * .78f;
                yield return Caminhar(soldado, aproximacao);
                if (soldado.HasArrived)
                {
                    soldado.Face(deposito);
                    yield return new WaitForSeconds(.3f);
                    soldado.DropCrate(deposito, ponto.rotation);
                    yield return new WaitForSeconds(.65f);
                    carga[i].SetParent(ponto, true);
                    carga[i].GetComponent<BoxCollider>().enabled = true;
                    estoque[destinos[i], prateleira[i]] = carga[i];
                    proximaVaga[destinos[i]] = 1 - prateleira[i];
                    carga[i] = null;
                }
            }
        }
        // Se um ponto ficar obstruído, a carga volta com a equipe para a próxima visita.
        do
        {
            yield return Caminhar(soldado, porta);
            if (soldado.HasArrived) break;
            yield return Caminhar(soldado, coleta);
            if (!soldado.HasArrived) yield return new WaitForSeconds(1f);
        } while (!soldado.HasArrived);
        soldado.Face(jipe.position);
        if (carga[i])
        {
            soldado.DropCrate(jipe.TransformPoint(CargaLocal(i)), jipe.rotation);
            yield return new WaitForSeconds(.65f);
            AcomodarCaixa(i);
        }
        yield return new WaitForSeconds(.5f);
        soldado.Board(jipe, false, Banco(i + 1));
    }

    static IEnumerator Caminhar(CampSupplyCrewMember soldado, Vector3 ponto)
    {
        for (int tentativa = 0; tentativa < 2; tentativa++)
        {
            if (soldado.WalkTo(ponto))
            {
                while (!soldado.HasArrived && !soldado.PathFailed) yield return null;
                if (soldado.HasArrived) yield break;
            }
            yield return new WaitForSeconds(.5f);
        }
    }

    void RecolherViatura()
    {
        motor.Stop();
        foreach (var soldado in equipe)
            if (soldado) soldado.Park();
        obstaculoEstacionado.enabled = false;
        jipe.gameObject.SetActive(false);
    }

    void OnDisable()
    {
        StopAllCoroutines();
        if (jipe && motor && obstaculoEstacionado && gameObject.activeInHierarchy)
        {
            RecolherViatura();
            for (int i = 0; i < carga.Length; i++)
                if (carga[i]) AcomodarCaixa(i);
        }
    }
}

