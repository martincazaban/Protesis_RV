using System;
using System.Collections.Generic;
using UnityEngine;

namespace Entrenamiento
{
    /// <summary>
    /// Cuenta cuantos objetos del contenedor de origen estan apoyados dentro
    /// del contenedor de destino (no cuenta los que estan agarrados).
    /// La zona de destino es una caja en el espacio local de 'zonaDestino'.
    /// </summary>
    public class ContadorTraslados : MonoBehaviour
    {
        [Tooltip("Contenedor de origen: sus Grabbable hijos al iniciar son los objetos de la tarea.")]
        public Transform contenedorOrigen;
        [Tooltip("Contenedor de destino (la zona se mide en su espacio local).")]
        public Transform zonaDestino;
        public Vector3 centroZona = new Vector3(0f, 0.08f, 0f);
        public Vector3 tamanoZona = new Vector3(0.25f, 0.16f, 0.20f);

        public int Trasladados { get; private set; }
        public int Total => objetos.Count;
        public event Action<int, int> Cambio;

        private readonly List<Grabbable> objetos = new List<Grabbable>();

        private void Start()
        {
            if (contenedorOrigen != null)
                objetos.AddRange(contenedorOrigen.GetComponentsInChildren<Grabbable>());
            Cambio?.Invoke(Trasladados, Total);
        }

        private void FixedUpdate()
        {
            if (zonaDestino == null) return;
            int n = 0;
            foreach (var g in objetos)
            {
                if (g == null || g.IsHeld) continue;
                if (DentroDeZona(g.rb != null ? g.rb.worldCenterOfMass : g.transform.position)) n++;
            }
            if (n != Trasladados)
            {
                Trasladados = n;
                Cambio?.Invoke(Trasladados, Total);
            }
        }

        public bool DentroDeZona(Vector3 puntoMundo)
        {
            Vector3 p = zonaDestino.InverseTransformPoint(puntoMundo) - centroZona;
            Vector3 h = tamanoZona * 0.5f;
            return Mathf.Abs(p.x) <= h.x && Mathf.Abs(p.y) <= h.y && Mathf.Abs(p.z) <= h.z;
        }

        private void OnDrawGizmosSelected()
        {
            if (zonaDestino == null) return;
            Gizmos.matrix = zonaDestino.localToWorldMatrix;
            Gizmos.color = new Color(0.2f, 1f, 0.3f, 0.35f);
            Gizmos.DrawCube(centroZona, tamanoZona);
        }
    }
}
