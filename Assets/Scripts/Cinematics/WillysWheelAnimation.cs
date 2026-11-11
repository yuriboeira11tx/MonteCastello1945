using UnityEngine;

namespace FEB.Cinematics
{
    [DisallowMultipleComponent]
    public sealed class WillysWheelAnimation : MonoBehaviour
    {
        [Tooltip("Pivôs nos cubos das rodas dianteiras. Inclua todos os níveis de detalhe.")]
        [SerializeField] Transform[] rodasDianteiras;
        [Tooltip("Pivôs nos cubos das rodas traseiras. Inclua todos os níveis de detalhe.")]
        [SerializeField] Transform[] rodasTraseiras;
        [Tooltip("Raio do pneu em unidades locais do jipe.")]
        [SerializeField, Min(.01f)] float raioDoPneu = .36f;
        [SerializeField, Range(1f, 60f)] float anguloMaximoDeDirecao = 32f;
        [Tooltip("Fonte 3D em loop junto aos eixos. O volume da fonte define o máximo na estrada.")]
        [SerializeField] AudioSource rolagemNaTerra;

        struct Roda
        {
            public Transform pivo;
            public Quaternion orientacaoInicial;
            public float posicaoLateral, giro, direcao;
        }

        Roda[] dianteiras, traseiras;
        float entreEixos;
        float volumeDeRolagem, pitchDeRolagem;
        Vector3 posicaoAnterior;
        Quaternion rotacaoAnterior;

        void Awake()
        {
            if (!LerEixo(rodasDianteiras, out dianteiras, out float frente) ||
                !LerEixo(rodasTraseiras, out traseiras, out float tras))
            {
                Debug.LogWarning("Configure os pivôs das rodas dianteiras e traseiras do Willys.", this);
                enabled = false;
                return;
            }
            entreEixos = Mathf.Abs(frente - tras);
            if (rolagemNaTerra)
            {
                volumeDeRolagem = rolagemNaTerra.volume;
                pitchDeRolagem = rolagemNaTerra.pitch;
            }
        }

        bool LerEixo(Transform[] pivos, out Roda[] rodas, out float centroZ)
        {
            centroZ = 0f;
            rodas = new Roda[pivos == null ? 0 : pivos.Length];
            if (rodas.Length == 0) return false;
            for (int i = 0; i < rodas.Length; i++)
            {
                var pivo = pivos[i];
                if (!pivo || pivo == transform || !pivo.IsChildOf(transform)) return false;
                Vector3 cubo = transform.InverseTransformPoint(pivo.position);
                centroZ += cubo.z;
                rodas[i] = new Roda
                {
                    pivo = pivo,
                    orientacaoInicial = Quaternion.Inverse(transform.rotation) * pivo.rotation,
                    posicaoLateral = cubo.x
                };
            }
            centroZ /= rodas.Length;
            return true;
        }

        void OnEnable() => SincronizarPose();
        void OnDisable() => SilenciarRolagem();

        // Chamar depois de reposicionar o jipe instantaneamente, antes de retomar a marcha.
        public void SincronizarPose()
        {
            posicaoAnterior = transform.position;
            rotacaoAnterior = transform.rotation;
            SilenciarRolagem();
        }

        void LateUpdate()
        {
            Vector3 deslocamento = transform.position - posicaoAnterior;
            Quaternion anterior = rotacaoAnterior;
            posicaoAnterior = transform.position;
            rotacaoAnterior = transform.rotation;
            float dt = Time.deltaTime;
            // Acima da velocidade das rotas do acampamento, um salto é um reposicionamento.
            float passoMaximo = Mathf.Max(.75f, 14f * dt);
            if (dt <= 0f || deslocamento.sqrMagnitude > passoMaximo * passoMaximo)
            {
                SilenciarRolagem();
                return;
            }
            if (deslocamento.sqrMagnitude < 1e-10f)
            {
                AtualizarRolagem(0f, dt);
                return;
            }

            Quaternion meiaRotacao = Quaternion.Slerp(anterior, rotacaoAnterior, .5f);
            float avanco = Vector3.Dot(deslocamento, meiaRotacao * Vector3.forward);
            AtualizarRolagem(Mathf.Abs(avanco) / dt, dt);
            if (Mathf.Abs(avanco) < .00001f) return;

            Vector3 normal = meiaRotacao * Vector3.up;
            Vector3 antes = Vector3.ProjectOnPlane(anterior * Vector3.forward, normal);
            Vector3 depois = Vector3.ProjectOnPlane(rotacaoAnterior * Vector3.forward, normal);
            float guinada = Vector3.SignedAngle(antes, depois, normal) * Mathf.Deg2Rad;
            Vector3 escala = transform.lossyScale;
            float raio = Mathf.Max(.01f, raioDoPneu * Mathf.Abs(escala.y));
            GirarEixo(dianteiras, avanco, guinada, entreEixos * Mathf.Abs(escala.z), escala.x, raio);
            GirarEixo(traseiras, avanco, guinada, 0f, escala.x, raio);
        }

        void AtualizarRolagem(float metrosPorSegundo, float dt)
        {
            if (!rolagemNaTerra || !rolagemNaTerra.isActiveAndEnabled || !rolagemNaTerra.clip) return;
            float ritmo = Mathf.InverseLerp(.09f, 5.5f, metrosPorSegundo);
            if (ritmo <= 0f && !rolagemNaTerra.isPlaying) return;
            float alvo = volumeDeRolagem * Mathf.Sqrt(ritmo);
            float taxa = alvo > rolagemNaTerra.volume ? 3f : 5f;
            rolagemNaTerra.volume = Mathf.MoveTowards(rolagemNaTerra.volume, alvo, volumeDeRolagem * taxa * dt);
            float pitch = pitchDeRolagem * Mathf.Lerp(.80f, 1.10f, ritmo);
            rolagemNaTerra.pitch = Mathf.MoveTowards(rolagemNaTerra.pitch, pitch, 1.2f * dt);
            if (rolagemNaTerra.volume > .001f)
            {
                if (!rolagemNaTerra.isPlaying) rolagemNaTerra.Play();
            }
            else if (alvo <= 0f) SilenciarRolagem();
        }

        void SilenciarRolagem()
        {
            if (!rolagemNaTerra) return;
            if (rolagemNaTerra.isPlaying) rolagemNaTerra.Stop();
            rolagemNaTerra.volume = 0f;
            rolagemNaTerra.pitch = pitchDeRolagem * .80f;
        }

        void GirarEixo(Roda[] rodas, float avanco, float guinada, float distanciaAoEixoTraseiro,
            float escalaLateral, float raio)
        {
            for (int i = 0; i < rodas.Length; i++)
            {
                ref Roda roda = ref rodas[i];
                // A roda de dentro da curva percorre um arco menor que a de fora.
                float longitudinal = avanco - guinada * roda.posicaoLateral * escalaLateral;
                float percurso = longitudinal;
                if (distanciaAoEixoTraseiro > 0f)
                {
                    float lateral = guinada * distanciaAoEixoTraseiro;
                    float sentido = Mathf.Sign(avanco);
                    float alvo = Mathf.Atan2(lateral * sentido, longitudinal * sentido) * Mathf.Rad2Deg;
                    roda.direcao = Mathf.MoveTowards(roda.direcao, Mathf.Clamp(alvo, -anguloMaximoDeDirecao, anguloMaximoDeDirecao),
                        180f * Time.deltaTime);
                    float angulo = roda.direcao * Mathf.Deg2Rad;
                    percurso = longitudinal * Mathf.Cos(angulo) + lateral * Mathf.Sin(angulo);
                }
                roda.giro = Mathf.Repeat(roda.giro + percurso / raio * Mathf.Rad2Deg, 360f);
                roda.pivo.rotation = rotacaoAnterior * Quaternion.AngleAxis(roda.direcao, Vector3.up)
                    * Quaternion.AngleAxis(roda.giro, Vector3.right) * roda.orientacaoInicial;
            }
        }
    }
}
