using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CampSupplyDistribution : MonoBehaviour
{
    [SerializeField] CampDepotPorter[] carregadores;
    [SerializeField] Transform enfermaria;
    [SerializeField] Transform cozinha;
    [Tooltip("Segundos entre a entrega no setor e o consumo da remessa.")]
    [SerializeField] Vector2 tempoDeConsumo = new(8f, 14f);

    sealed class Remessa
    {
        public CampSupplyPatrol viatura;
        public Transform caixa, recebimento, destino;
        public int vaga;
        public bool emAtendimento;
        public bool devolverAoRecebimento;
        public Pose posicaoNoRecebimento;
        public float tentarDepois, consumirEm;
    }

    readonly List<Remessa> remessas = new();
    readonly Stack<Transform> caixasVazias = new();
    int proximoCarregador;
    bool paraEnfermaria = true;
    float proximaConferencia;

    public bool Pronta => enfermaria && cozinha && carregadores != null && carregadores.Length > 0 &&
        System.Array.TrueForAll(carregadores, c => c);

    public bool VagaLivre(Transform recebimento, int vaga)
    {
        if (!recebimento || vaga < 0 || vaga > 1) return false;
        foreach (var remessa in remessas)
            if (remessa.recebimento == recebimento && remessa.vaga == vaga) return false;
        return true;
    }

    public bool ReservarDescarga(CampSupplyPatrol viatura, Transform primeiro, int vagaPrimeira, Transform segundo, int vagaSegunda)
    {
        if (!viatura || !primeiro || !segundo || primeiro == segundo ||
            !VagaLivre(primeiro, vagaPrimeira) || !VagaLivre(segundo, vagaSegunda))
            return false;

        remessas.Add(new Remessa { viatura = viatura, recebimento = primeiro, vaga = vagaPrimeira });
        remessas.Add(new Remessa { viatura = viatura, recebimento = segundo, vaga = vagaSegunda });
        return true;
    }

    public void CancelarReservas(CampSupplyPatrol viatura, Transform ponto = null)
    {
        if (!viatura) return;
        for (int i = remessas.Count - 1; i >= 0; i--)
            if (remessas[i].viatura == viatura && (!ponto || remessas[i].recebimento == ponto))
                remessas.RemoveAt(i);
    }

    public Transform PrepararCaixa(GameObject modelo, Transform jipe)
    {
        if (caixasVazias.Count == 0) return Instantiate(modelo, jipe).transform;
        var caixa = caixasVazias.Pop();
        caixa.SetParent(jipe, false);
        caixa.gameObject.SetActive(true);
        return caixa;
    }

    public bool Receber(CampSupplyPatrol viatura, Transform caixa, Transform ponto, int vaga)
    {
        if (!viatura || !caixa) return false;
        foreach (var remessa in remessas)
        {
            if (remessa.viatura != viatura || remessa.recebimento != ponto || remessa.vaga != vaga) continue;
            remessa.viatura = null;
            remessa.caixa = caixa;
            remessa.posicaoNoRecebimento = new Pose(ponto.InverseTransformPoint(caixa.position),
                Quaternion.Inverse(ponto.rotation) * caixa.rotation);
            remessa.destino = paraEnfermaria ? enfermaria : cozinha;
            paraEnfermaria = !paraEnfermaria;
            return true;
        }
        return false;
    }

    void Update()
    {
        if (remessas.Count == 0 || Time.time < proximaConferencia) return;
        proximaConferencia = Time.time + .4f;
        for (int i = remessas.Count - 1; i >= 0; i--)
        {
            var r = remessas[i];
            if (!r.caixa)
            {
                if (!r.viatura) remessas.RemoveAt(i);
                continue;
            }
            if (r.devolverAoRecebimento)
            {
                r.caixa.SetParent(r.recebimento, false);
                r.caixa.SetLocalPositionAndRotation(r.posicaoNoRecebimento.position, r.posicaoNoRecebimento.rotation);
                r.devolverAoRecebimento = false;
            }
            if (r.consumirEm <= 0f || Time.time < r.consumirEm) continue;
            r.caixa.gameObject.SetActive(false);
            r.caixa.SetParent(transform, false);
            caixasVazias.Push(r.caixa);
            remessas.RemoveAt(i);
        }

        foreach (var r in remessas)
        {
            if (!r.caixa || r.emAtendimento || Time.time < r.tentarDepois || SetorOcupado(r.destino)) continue;
            for (int passo = 0; passo < carregadores.Length; passo++)
            {
                int i = (proximoCarregador + passo) % carregadores.Length;
                var soldado = carregadores[i];
                if (!soldado || !soldado.Disponivel) continue;
                r.emAtendimento = true;
                if (soldado.TryAssign(r.caixa, r.destino, (caixa, entregue) => Concluir(r, entregue)))
                {
                    proximoCarregador = (i + 1) % carregadores.Length;
                    break;
                }
                r.emAtendimento = false;
            }
        }
    }

    bool SetorOcupado(Transform destino)
    {
        foreach (var r in remessas)
            if (r.destino == destino && r.emAtendimento) return true;
        return false;
    }

    void Concluir(Remessa remessa, bool entregue)
    {
        if (!entregue)
        {
            remessa.emAtendimento = false;
            remessa.tentarDepois = Time.time + 2f;
            remessa.devolverAoRecebimento = remessa.caixa && remessa.caixa.GetComponentInParent<CampDepotPorter>(true);
            return;
        }
        float minimo = Mathf.Max(.1f, tempoDeConsumo.x);
        remessa.consumirEm = Time.time + Random.Range(minimo, Mathf.Max(minimo, tempoDeConsumo.y));
    }
}
