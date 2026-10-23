using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Entrenamiento
{
    public enum FaseEntrenamiento { Espera, EnCurso, Finalizado }

    /// <summary>
    /// Metricas de la prueba de traslado:
    ///  - Tiempo total (de la pulsacion de inicio a la de fin).
    ///  - Tiempo de cada pieza (tomada -> soltada sobre el destino; las perdidas no se guardan).
    ///  - Toques de mesa/barrera con una pieza agarrada.
    ///  - Soltadas: piezas "perdidas" (soltadas sin caer rapido en el destino).
    /// Lo controla BotonInicioFin (Alternar). El HUD y, mas adelante, el hub leen
    /// las propiedades o escuchan el evento Cambio.
    /// </summary>
    public class MedidorEntrenamiento : MonoBehaviour
    {
        [Tooltip("Para saber si una pieza esta dentro del destino. Vacio = se busca en la escena.")]
        public ContadorTraslados contador;
        [Tooltip("Colliders que cuentan como toque cuando una pieza agarrada los toca (tablero de la mesa y placa divisoria).")]
        public Collider[] obstaculos;
        [Tooltip("Segundos tras soltar la pieza en los que debe caer en el destino para ser valida.")]
        public float ventanaCaida = 1.5f;
        [Tooltip("Segundos tras agarrar en los que no se cuentan toques (por el propio agarre).")]
        public float graciaToque = 0.25f;
        [Tooltip("Tiempo minimo entre dos toques de la misma pieza (evita contar rebotes).")]
        public float cooldownToque = 0.3f;

        public FaseEntrenamiento Fase { get; private set; } = FaseEntrenamiento.Espera;
        public bool EnCurso => Fase == FaseEntrenamiento.EnCurso;
        public int Toques { get; private set; }
        public int Soltadas { get; private set; }
        public int Entregadas { get { int n = 0; foreach (var p in piezas) if (p != null && p.Entregada) n++; return n; } }
        public int TotalPiezas => piezas.Count;
        public float UltimaPieza { get; private set; } = -1f;
        public float TiempoTotal =>
            Fase == FaseEntrenamiento.EnCurso ? Time.time - tInicio :
            Fase == FaseEntrenamiento.Finalizado ? tFin - tInicio : 0f;

        public event Action Cambio;

        private readonly List<PiezaMedida> piezas = new List<PiezaMedida>();
        private readonly HashSet<Collider> setObstaculos = new HashSet<Collider>();
        private float tInicio, tFin;

        private void Start()
        {
            if (contador == null) contador = FindObjectOfType<ContadorTraslados>();
            foreach (var o in obstaculos) if (o != null) setObstaculos.Add(o);
            foreach (var g in FindObjectsOfType<Grabbable>())
            {
                var p = g.GetComponent<PiezaMedida>();
                if (p == null) p = g.gameObject.AddComponent<PiezaMedida>();
                p.Registrar(this);
                piezas.Add(p);
            }
            NotificarCambio();
        }

        // ---- control de la prueba -------------------------------------------------
        /// <summary>Espera/Finalizado -> inicia una prueba nueva; EnCurso -> termina.</summary>
        public void Alternar()
        {
            if (Fase == FaseEntrenamiento.EnCurso) Finalizar(); else Iniciar();
        }

        public void Iniciar()
        {
            Toques = 0; Soltadas = 0; UltimaPieza = -1f;
            foreach (var p in piezas) if (p != null) p.Reiniciar();
            tInicio = Time.time;
            Fase = FaseEntrenamiento.EnCurso;
            Debug.Log("[Medidor] Prueba iniciada.");
            NotificarCambio();
        }

        public void Finalizar()
        {
            if (Fase != FaseEntrenamiento.EnCurso) return;
            tFin = Time.time;
            foreach (var p in piezas) if (p != null) p.Cancelar();
            Fase = FaseEntrenamiento.Finalizado;
            Debug.Log("[Medidor] Prueba finalizada.\n" + Resumen());
            NotificarCambio();
        }

        // ---- consultas ------------------------------------------------------------
        public bool EsPenalizador(Collider c) { return c != null && setObstaculos.Contains(c); }

        public bool EnDestino(Rigidbody rb)
        {
            return contador != null && rb != null && contador.DentroDeZona(rb.worldCenterOfMass);
        }

        /// <summary>Tiempo de la pieza que esta en la mano ahora (-1 si no hay).</summary>
        public float TiempoPiezaEnMano()
        {
            if (!EnCurso) return -1f;
            foreach (var p in piezas) if (p != null && p.EnMano && p.TiempoActual > 0f) return p.TiempoActual;
            return -1f;
        }

        /// <summary>Promedio, minimo y maximo de los tiempos guardados. false si no hay ninguno.</summary>
        public bool EstadisticasPiezas(out float promedio, out float minimo, out float maximo)
        {
            promedio = 0f; minimo = float.MaxValue; maximo = 0f;
            int n = 0;
            foreach (var p in piezas)
            {
                if (p == null || !p.Entregada) continue;
                float t = p.TiempoGuardado;
                promedio += t; n++;
                if (t < minimo) minimo = t;
                if (t > maximo) maximo = t;
            }
            if (n == 0) { minimo = 0f; return false; }
            promedio /= n;
            return true;
        }

        public string Resumen()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Tiempo total: " + Formato(TiempoTotal));
            sb.AppendLine("Piezas entregadas: " + Entregadas + " / " + TotalPiezas);
            sb.AppendLine("Toques de mesa/barrera: " + Toques);
            sb.AppendLine("Piezas soltadas (perdidas): " + Soltadas);
            foreach (var p in piezas)
                if (p != null && p.Entregada) sb.AppendLine("  " + p.name + ": " + p.TiempoGuardado.ToString("0.00") + " s");
            return sb.ToString();
        }

        public static string Formato(float s)
        {
            int m = (int)(s / 60f);
            return m.ToString("00") + ":" + (s - m * 60f).ToString("00.0");
        }

        // ---- notificaciones desde PiezaMedida ---------------------------------------
        public void NotificarToque(PiezaMedida p, Collider con)
        {
            if (!EnCurso) return;
            Toques++;
            Debug.Log("[Medidor] Toque: " + p.name + " con " + con.name + " (total " + Toques + ")");
            NotificarCambio();
        }

        public void NotificarSoltada(PiezaMedida p)
        {
            if (!EnCurso) return;
            Soltadas++;
            Debug.Log("[Medidor] Pieza perdida: " + p.name + " (total " + Soltadas + ")");
            NotificarCambio();
        }

        public void NotificarEntrega(PiezaMedida p, float tiempo)
        {
            if (!EnCurso) return;
            UltimaPieza = tiempo;
            Debug.Log("[Medidor] Pieza entregada: " + p.name + " en " + tiempo.ToString("0.00") + " s");
            NotificarCambio();
        }

        public void NotificarCambio()
        {
            if (Cambio != null) Cambio();
        }
    }
}
