using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations.Rigging;

[RequireComponent(typeof(SoldierAI), typeof(NavMeshAgent))]
[DisallowMultipleComponent]
public sealed class CampDepotPorter : MonoBehaviour
{
    [Tooltip("Encaixe no peito, com os alvos originais das mãos e dos cotovelos.")]
    [SerializeField] Transform encaixeDaCaixa;
    [SerializeField] TwoBoneIKConstraint maoEsquerda, maoDireita;
    [SerializeField, Range(0, 99)] int prioridadeDeNavegacao = 39;

    NavMeshAgent agent;
    NavMeshPath path;
    Pose posto, encaixeEmRepouso;
    Transform caixa;
    BoxCollider caixaCollider;
    CampCrateGrip pega;
    Action<Transform, bool> aoConcluir;
    bool ocupado, chegou, nasMaos, colliderOriginal;
    static readonly float[] AngulosDeAcesso = { 0f, 60f, -60f, 180f };

    public bool Disponivel => isActiveAndEnabled && !ocupado && encaixeDaCaixa && maoEsquerda && maoDireita;
    bool NaMalha => agent && agent.isActiveAndEnabled && agent.isOnNavMesh;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        path = new NavMeshPath();
        posto = new Pose(transform.position, transform.rotation);
        if (!encaixeDaCaixa || !maoEsquerda || !maoDireita)
        {
            Debug.LogError("Configure o encaixe e os dois apoios de mão do carregador.", this);
            enabled = false;
            return;
        }

        encaixeEmRepouso = new Pose(encaixeDaCaixa.localPosition, encaixeDaCaixa.localRotation);
        agent.avoidancePriority = prioridadeDeNavegacao;
        PesoDosBracos(0f);
    }

    public bool TryAssign(Transform carga, Transform destino, Action<Transform, bool> conclusao)
    {
        if (!Disponivel || !NaMalha || !carga || !destino || !carga.TryGetComponent(out BoxCollider col))
            return false;

        ocupado = true;
        caixa = carga;
        caixaCollider = col;
        colliderOriginal = col.enabled;
        aoConcluir = conclusao;
        StartCoroutine(Entregar(destino));
        return true;
    }

    IEnumerator Entregar(Transform destino)
    {
        yield return Caminhar(caixa.position, caixa.forward);
        if (chegou && caixa && destino)
        {
            yield return VirarPara(caixa.position);
            if (caixa)
            {
                // Ao abaixar o encaixe, as pegas originais acompanham a caixa real até o peito.
                encaixeDaCaixa.SetPositionAndRotation(caixa.position, caixa.rotation);
                caixaCollider.enabled = false;
                caixa.SetParent(encaixeDaCaixa, false);
                caixa.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                pega ??= new CampCrateGrip(GetComponentInChildren<Animator>());
                pega.PositionTargets(caixaCollider, maoEsquerda.data.target, maoDireita.data.target);
                nasMaos = true;
                yield return MoverEncaixe(encaixeEmRepouso, 0f, 1f);
            }
        }

        if (nasMaos && caixa && destino)
        {
            yield return Caminhar(destino.position, destino.forward);
            if (chegou && caixa && destino)
            {
                yield return VirarPara(destino.position);
                if (destino)
                {
                    Transform peito = encaixeDaCaixa.parent;
                    var colocar = new Pose(peito.InverseTransformPoint(destino.position),
                        Quaternion.Inverse(peito.rotation) * destino.rotation);
                    yield return MoverEncaixe(colocar, 1f, 0f);
                }
            }
        }
        bool entregue = nasMaos && chegou && caixa && destino;
        if (nasMaos && caixa)
        {
            if (entregue)
            {
                caixa.SetParent(destino, true);
                caixa.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            }
            else SoltarNoChao();
        }
        Concluir(entregue);

        // Se outra patrulha estiver no posto, espera e tenta voltar sem teletransportar o ator.
        do
        {
            yield return Caminhar(posto.position, Vector3.zero);
            if (!chegou) yield return new WaitForSeconds(2f);
        } while (!chegou);
        yield return VirarPara(transform.position + posto.rotation * Vector3.forward);
        ocupado = false;
    }

    IEnumerator Caminhar(Vector3 centro, Vector3 frente)
    {
        chegou = false;
        bool aproximar = frente.sqrMagnitude > .01f;
        for (int tentativa = 0; tentativa < AngulosDeAcesso.Length && !chegou; tentativa++)
        {
            if (NaMalha)
            {
                Parar();
                agent.updateRotation = true;
                // Um acesso ocupado pode ser contornado por qualquer um dos dois lados da caixa.
                var lado = Quaternion.Euler(0, AngulosDeAcesso[tentativa], 0);
                Vector3 alvo = centro;
                if (aproximar) alvo -= lado * frente * .73f;
                else if (tentativa > 0) alvo += posto.rotation * lado * Vector3.forward * .85f;
                if (NavMesh.SamplePosition(alvo, out var hit, aproximar ? .38f : .8f, agent.areaMask) &&
                    agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete &&
                    agent.SetPath(path))
                {
                    agent.isStopped = false;
                    Vector3 ultimaPosicao = transform.position;
                    float ultimoAvanco = Time.time;
                    float prazo = Time.time + 15f;
                    var curvas = path.corners;
                    for (int i = 1; i < curvas.Length; i++)
                        prazo += 3f * Vector3.Distance(curvas[i - 1], curvas[i]) / Mathf.Max(.2f, agent.speed);
                    while (NaMalha)
                    {
                        Vector3 restante = Vector3.ProjectOnPlane(hit.position - transform.position, Vector3.up);
                        if (restante.magnitude <= Mathf.Max(.19f, agent.stoppingDistance + .1f))
                        {
                            chegou = true;
                            break;
                        }
                        if ((!agent.pathPending && agent.pathStatus != NavMeshPathStatus.PathComplete) ||
                            Time.time - ultimoAvanco > 6f || Time.time > prazo) break;
                        if ((transform.position - ultimaPosicao).sqrMagnitude > .05f)
                        {
                            ultimaPosicao = transform.position;
                            ultimoAvanco = Time.time;
                        }
                        yield return null;
                    }
                }
                Parar();
            }
            if (!chegou) yield return new WaitForSeconds(.45f + .01f * prioridadeDeNavegacao);
        }
    }

    IEnumerator VirarPara(Vector3 ponto)
    {
        Vector3 direcao = Vector3.ProjectOnPlane(ponto - transform.position, Vector3.up);
        if (direcao.sqrMagnitude < .001f) yield break;
        Quaternion inicial = transform.rotation, final = Quaternion.LookRotation(direcao);
        agent.updateRotation = false;
        for (float t = 0f; t < 1f; t += Time.deltaTime * 3.5f)
        {
            transform.rotation = Quaternion.Slerp(inicial, final, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        transform.rotation = final;
    }

    IEnumerator MoverEncaixe(Pose fim, float pesoInicial, float pesoFinal)
    {
        var inicio = new Pose(encaixeDaCaixa.localPosition, encaixeDaCaixa.localRotation);
        for (float t = 0f; t < 1f; t += Time.deltaTime / .72f)
        {
            float peso = Mathf.SmoothStep(0f, 1f, t);
            encaixeDaCaixa.SetLocalPositionAndRotation(Vector3.Lerp(inicio.position, fim.position, peso),
                Quaternion.Slerp(inicio.rotation, fim.rotation, peso));
            PesoDosBracos(Mathf.Lerp(pesoInicial, pesoFinal, peso));
            yield return null;
        }
        encaixeDaCaixa.SetLocalPositionAndRotation(fim.position, fim.rotation);
        PesoDosBracos(pesoFinal);
    }

    void PesoDosBracos(float peso)
    {
        if (nasMaos && peso > 0f)
            peso *= pega.ContactWeight(maoEsquerda.data.target, maoDireita.data.target);
        maoEsquerda.weight = peso;
        maoDireita.weight = peso;
    }

    void LateUpdate()
    {
        if (nasMaos) pega.CloseFingers(maoEsquerda.weight);
    }

    void Parar()
    {
        if (!NaMalha) return;
        agent.isStopped = true;
        agent.ResetPath();
    }

    void SoltarNoChao()
    {
        Vector3 chao = transform.position + transform.forward * .7f;
        if (NavMesh.SamplePosition(chao, out var hit, .7f, NavMesh.AllAreas)) chao = hit.position;
        caixa.SetParent(transform.parent, true);
        caixa.SetPositionAndRotation(chao, transform.rotation);
    }

    void Concluir(bool entregue)
    {
        if (caixaCollider && nasMaos) caixaCollider.enabled = colliderOriginal;
        nasMaos = false;
        PesoDosBracos(0f);
        encaixeDaCaixa.SetLocalPositionAndRotation(encaixeEmRepouso.position, encaixeEmRepouso.rotation);
        var carga = caixa;
        var retorno = aoConcluir;
        caixa = null;
        caixaCollider = null;
        aoConcluir = null;
        retorno?.Invoke(carga, entregue);
    }

    void OnDisable()
    {
        if (!ocupado) return;
        StopAllCoroutines();
        Parar();
        // A distribuição recolhe a caixa no próximo Update, após a desativação da hierarquia.
        if (encaixeDaCaixa && maoEsquerda && maoDireita) Concluir(false);
        ocupado = false;
    }
}
