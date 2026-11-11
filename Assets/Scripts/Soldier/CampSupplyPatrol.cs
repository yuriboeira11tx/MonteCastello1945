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
    [Tooltip("Distribuição compartilhada pelas viaturas que atendem o mesmo acampamento.")]
    [SerializeField] CampSupplyDistribution distribuicao;
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
    int[] proximaVaga;
    int ultimaEntrada = -1;
    bool recolherAoReativar;

    static readonly Vector3 BancoDianteiro = new(.355f, .835f, -.46f);
    static readonly Vector3 BancoTraseiro = new(-.20f, .870f, -1.29f);
    static Vector3 Banco(int indice) => indice == 1 ? BancoDianteiro : BancoTraseiro;
    static Vector3 Porta(int ajudante) => ajudante == 0 ? new Vector3(1.35f, 0, -.44f) : new Vector3(-1.35f, 0, -1.07f);
    static Vector3 CargaLocal(int ajudante) => new(.40f, .928f + (1 - ajudante) * .381f, -1.17f);

    void OnEnable() => StartCoroutine(Patrulhar());

    IEnumerator Patrulhar()
    {
        if (recolherAoReativar)
        {
            yield return null;
            InterromperVisita();
            recolherAoReativar = false;
        }
        if (!jipe || !motor || !obstaculoEstacionado || !animacoes || !caixaDeSuprimentos || !distribuicao || !distribuicao.Pronta ||
            modelosDosSoldados == null || modelosDosSoldados.Length != 3 ||
            System.Array.Exists(modelosDosSoldados, modelo => !modelo || !modelo.GetComponentInChildren<Animator>(true)) ||
            vias == null || vias.Length == 0 || System.Array.Exists(vias, v => !v || !v.IsValid(true) || !v.IsValid(false)) ||
            pontosDeEntrega == null || pontosDeEntrega.Length < 2 || System.Array.Exists(pontosDeEntrega, ponto => !ponto))
        {
            Debug.LogError("Confira a viatura, a tripulação, as vias e os pontos de entrega da patrulha.", this);
            yield break;
        }

        proximaVaga ??= new int[pontosDeEntrega.Length];
        while (!ResearchSaveStore.EnsureSession().campArrivalCompleted) yield return new WaitForSeconds(.5f);
        while (true)
        {
            float minimo = Mathf.Max(0f, intervaloEntreVisitas.x);
            yield return new WaitForSeconds(Random.Range(minimo, Mathf.Max(minimo, intervaloEntreVisitas.y)));
            // Se os setores ainda estiverem ocupados, o novo carregamento aguarda fora do acampamento.
            while (!TentarReservarDescarga()) yield return new WaitForSeconds(1f);
            yield return Visitar();
        }
    }

    IEnumerator Visitar()
    {
        int entrada = EscolherVia(ultimaEntrada);
        // Sorteios separados permitem regressar pela mesma estrada ou seguir para a outra.
        int saida = Random.value < .67f ? EscolherVia(entrada) : entrada;
        ultimaEntrada = entrada;
        yield return vias[entrada].Drive(jipe, true, velocidadeDoJipe, pesquisador, PrepararChegada);
        if (!jipe.gameObject.activeSelf)
        {
            distribuicao.CancelarReservas(this);
            yield break;
        }
        motor.pitch = .93f;
        yield return new WaitForSeconds(1f);
        Coroutine primeiraEntrega = StartCoroutine(Descarregar(0));
        yield return new WaitForSeconds(1.1f);
        yield return Descarregar(1);
        yield return primeiraEntrega;
        distribuicao.CancelarReservas(this);
        yield return new WaitForSeconds(.75f);
        motor.pitch = 1.07f;
        yield return vias[saida].Drive(jipe, false, velocidadeDoJipe, pesquisador);
        RecolherViatura();
    }

    void PrepararChegada()
    {
        jipe.gameObject.SetActive(true);
        obstaculoEstacionado.carveOnlyStationary = true;
        obstaculoEstacionado.enabled = true;
        PrepararEquipe();
        for (int i = 0; i < carga.Length; i++)
        {
            if (!carga[i]) carga[i] = distribuicao.PrepararCaixa(caixaDeSuprimentos, jipe);
            AcomodarCaixa(i);
        }
        motor.pitch = 1.07f;
        motor.Play();
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
            if ((outro >= 0 && pontosDeEntrega[i] == pontosDeEntrega[outro]) || BuscarVaga(i) < 0) continue;
            int valor = i != anteriorA && i != anteriorB ? 1 : 0;
            if (valor < prioridade) continue;
            if (valor > prioridade) { prioridade = valor; candidatos = 0; }
            if (Random.Range(0, ++candidatos) == 0) melhor = i;
        }
        return melhor;
    }

    int BuscarVaga(int setor)
    {
        var ponto = pontosDeEntrega[setor];
        int vaga = proximaVaga[setor];
        if (distribuicao.VagaLivre(ponto, vaga)) return vaga;
        return distribuicao.VagaLivre(ponto, 1 - vaga) ? 1 - vaga : -1;
    }

    bool TentarReservarDescarga()
    {
        int primeiro = SortearEntrega(-1, destinos[0], destinos[1]);
        if (primeiro < 0) return false;
        int segundo = SortearEntrega(primeiro, destinos[0], destinos[1]);
        if (segundo < 0) return false;
        int vagaPrimeira = BuscarVaga(primeiro);
        int vagaSegunda = BuscarVaga(segundo);
        if (!distribuicao.ReservarDescarga(this, pontosDeEntrega[primeiro], vagaPrimeira, pontosDeEntrega[segundo], vagaSegunda))
            return false;
        destinos[0] = primeiro;
        destinos[1] = segundo;
        prateleira[0] = vagaPrimeira;
        prateleira[1] = vagaSegunda;
        return true;
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
            distribuicao.CancelarReservas(this, pontosDeEntrega[destinos[i]]);
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
                    soldado.DropCrate(deposito, ponto.rotation, ponto);
                    yield return new WaitForSeconds(.65f);
                    carga[i].GetComponent<BoxCollider>().enabled = true;
                    if (distribuicao.Receber(this, carga[i], ponto, prateleira[i]))
                    {
                        proximaVaga[destinos[i]] = 1 - prateleira[i];
                        carga[i] = null;
                    }
                    else soldado.TakeCrate(carga[i]);
                }
            }
        }
        // Se um ponto ficar obstruído, a carga volta com a equipe para a próxima visita.
        if (carga[i]) distribuicao.CancelarReservas(this, pontosDeEntrega[destinos[i]]);
        do
        {
            yield return Caminhar(soldado, porta);
            if (soldado.HasArrived) break;
            yield return Caminhar(soldado, coleta);
            if (soldado.HasArrived) break;
            yield return Caminhar(soldado, jipe.TransformPoint(-Porta(i).x, 0, .5f));
            if (!soldado.HasArrived) yield return new WaitForSeconds(1f);
        } while (!soldado.HasArrived);
        soldado.Face(jipe.position);
        if (carga[i])
        {
            soldado.DropCrate(jipe.TransformPoint(CargaLocal(i)), jipe.rotation, jipe);
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
        if (distribuicao) distribuicao.CancelarReservas(this);
        if (!jipe || !motor || !obstaculoEstacionado) return;
        if (gameObject.activeInHierarchy) InterromperVisita();
        else recolherAoReativar = true;
    }

    void InterromperVisita()
    {
        RecolherViatura();
        for (int i = 0; i < carga.Length; i++)
            if (carga[i]) AcomodarCaixa(i);
    }
}

