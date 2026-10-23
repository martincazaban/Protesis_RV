using System.Collections;
using UnityEngine;

namespace Entrenamiento
{
    /// <summary>
    /// Mide una pieza (Grabbable) para el MedidorEntrenamiento:
    ///  - Tiempo desde que se toma hasta que se suelta. Si al soltarla cae sobre el
    ///    destino dentro de 'ventanaCaida', se guarda ese tiempo; si no, es una
    ///    pieza "perdida" (suma una soltada y no se guarda tiempo).
    ///  - Toques de mesa/barrera mientras esta agarrada.
    /// Al volver a tomarla se reinicia su temporizador.
    /// </summary>
    [RequireComponent(typeof(Grabbable))]
    public class PiezaMedida : MonoBehaviour
    {
        /// <summary>Tiempo guardado de la ultima entrega valida (-1 = ninguno).</summary>
        public float TiempoGuardado { get; private set; } = -1f;
        public bool Entregada => TiempoGuardado >= 0f;
        public bool EnMano => grabbable != null && grabbable.IsHeld;
        /// <summary>Tiempo transcurrido de la pieza que esta en la mano (0 si no se esta midiendo).</summary>
        public float TiempoActual => (midiendo && EnMano) ? Time.time - tAgarre : 0f;

        private MedidorEntrenamiento medidor;
        private Grabbable grabbable;
        private bool midiendo;
        private bool pendiente;      // soltada, esperando ver si cae en el destino
        private float tAgarre;
        private float tUltimoToque = -999f;
        private Coroutine resolver;

        public void Registrar(MedidorEntrenamiento m)
        {
            if (grabbable == null) grabbable = GetComponent<Grabbable>();
            if (medidor == null) grabbable.EstadoCambiado += AlCambiarEstado;
            medidor = m;
        }

        private void OnDestroy()
        {
            if (grabbable != null) grabbable.EstadoCambiado -= AlCambiarEstado;
        }

        /// <summary>Nueva sesion: borra los datos; si ya esta en la mano empieza a medirse ahora.</summary>
        public void Reiniciar()
        {
            Cancelar();
            TiempoGuardado = -1f;
            if (EnMano) { midiendo = true; tAgarre = Time.time; }
        }

        /// <summary>Fin de sesion o reinicio: descarta mediciones en curso sin contarlas.</summary>
        public void Cancelar()
        {
            if (resolver != null) { StopCoroutine(resolver); resolver = null; }
            pendiente = false;
            midiendo = false;
        }

        private bool eraAgarrada;

        private void AlCambiarEstado(Grabbable g, Grabbable.Estado estado)
        {
            if (estado == Grabbable.Estado.Agarrado)
            {
                eraAgarrada = true;
                if (pendiente)
                {
                    // la volvio a tomar antes de que cayera en el destino: se considera perdida
                    if (resolver != null) { StopCoroutine(resolver); resolver = null; }
                    pendiente = false;
                    medidor.NotificarSoltada(this);
                }
                TiempoGuardado = -1f;                        // vuelve a estar "en proceso"
                midiendo = medidor.EnCurso;
                tAgarre = Time.time;                         // reinicia el temporizador
                medidor.NotificarCambio();
            }
            else if (eraAgarrada)
            {
                eraAgarrada = false;
                if (!midiendo || !medidor.EnCurso) { midiendo = false; return; }
                midiendo = false;
                float tiempo = Time.time - tAgarre;
                pendiente = true;
                if (isActiveAndEnabled) resolver = StartCoroutine(Resolver(tiempo));
            }
        }

        private IEnumerator Resolver(float tiempo)
        {
            float limite = Time.time + medidor.ventanaCaida;
            var espera = new WaitForFixedUpdate();
            while (Time.time < limite)
            {
                if (medidor.EnDestino(grabbable.rb))
                {
                    pendiente = false; resolver = null;
                    TiempoGuardado = tiempo;
                    medidor.NotificarEntrega(this, tiempo);
                    yield break;
                }
                yield return espera;
            }
            pendiente = false; resolver = null;
            medidor.NotificarSoltada(this);
        }

        private void OnCollisionEnter(Collision c)
        {
            if (medidor == null || !midiendo || !EnMano || !medidor.EnCurso) return;
            if (Time.time - tAgarre < medidor.graciaToque) return;
            if (!medidor.EsPenalizador(c.collider)) return;
            if (Time.time - tUltimoToque < medidor.cooldownToque) return;
            tUltimoToque = Time.time;
            medidor.NotificarToque(this, c.collider);
        }
    }
}
