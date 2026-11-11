using UnityEngine;

namespace FEB.Cinematics
{
    [DisallowMultipleComponent]
    public sealed class AircraftPropeller : MonoBehaviour
    {
        [SerializeField] Transform helice;
        [Tooltip("Eixo de giro no espaço local do cubo da hélice.")]
        [SerializeField] Vector3 eixoLocal = Vector3.forward;
        [Tooltip("Velocidade da hélice em rotações por minuto.")]
        [SerializeField, Min(0f)] float rpm = 480f;

        public void SetRunning(bool running) => enabled = running;

        void Update()
        {
            if (helice)
                helice.Rotate(eixoLocal, rpm * 6f * Time.deltaTime, Space.Self);
        }
    }
}
