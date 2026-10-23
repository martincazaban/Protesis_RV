using System.Collections;
using UnityEngine;
using Unity.XR.CoreUtils;

namespace Entrenamiento
{
    /// <summary>
    /// Coloca la cabeza del usuario (camara VR) en una pose fija respecto de la mesa, sin importar
    /// donde este su espacio de juego fisico. Al iniciar (despues de que el visor empieza a trackear)
    /// y cada vez que se pide recentrar (tecla R, boton B del control derecho o Y del izquierdo):
    ///   1) gira el XR Origin alrededor de la camara para que mire hacia 'yawObjetivo';
    ///   2) mueve el XR Origin para que la camara quede en 'posicionCabeza' (mundo);
    ///   3) vuelve a ubicar el hombro virtual sobre la cabeza y reinicia el brazo fisico.
    /// Con el usuario sentado: la posicion es la de los ojos, no la de la silla.
    /// </summary>
    [DefaultExecutionOrder(60)]
    public class PosicionSesion : MonoBehaviour
    {
        public XROrigin origen;
        [Tooltip("Vacio = la camara del XR Origin.")]
        public Transform camara;
        public ProtesisRig rig;
        public Entrenamiento.BrazoFisico brazoFisico;

        [Header("Pose de los ojos (mundo)")]
        public Vector3 posicionCabeza = new Vector3(0.18f, 1.17f, -0.20f);
        [Tooltip("Rumbo (grados, 0 = hacia +Z, la mesa).")]
        public float yawObjetivo = 0f;
        public bool alinearYaw = true;

        [Header("Recentrado")]
        public bool recentrarAlIniciar = true;
        [Tooltip("Segundos de espera para que el visor tenga tracking valido antes del recentrado inicial.")]
        public float retardoInicial = 1.5f;
        public bool botonesControl = true;

        private bool prevB, prevY;

        private void Start()
        {
            if (origen == null) origen = GetComponent<XROrigin>();
            if (camara == null && origen != null) camara = origen.Camera != null ? origen.Camera.transform : null;
            if (camara == null && Camera.main != null) camara = Camera.main.transform;
            if (rig == null) rig = FindFirstObjectByType<ProtesisRig>();
            if (brazoFisico == null) brazoFisico = FindFirstObjectByType<Entrenamiento.BrazoFisico>();
            // La protesis queda desde el primer cuadro en la posicion de sesion: no se mueve al recentrar.
            if (rig != null) { rig.FijarOrigen(posicionCabeza); if (brazoFisico != null) brazoFisico.Reiniciar(); }
            if (recentrarAlIniciar) StartCoroutine(RecentrarTrasEspera());
        }

        private IEnumerator RecentrarTrasEspera()
        {
            yield return new WaitForSeconds(retardoInicial);
            Recentrar();
        }

        private void Update()
        {
            bool pedir = false;
            var kb = UnityEngine.InputSystem.Keyboard.current; if (kb != null && kb.rKey.wasPressedThisFrame) pedir = true;
            if (botonesControl)
            {
                bool b = Boton(UnityEngine.XR.XRNode.RightHand), y = Boton(UnityEngine.XR.XRNode.LeftHand);
                if ((b && !prevB) || (y && !prevY)) pedir = true;
                prevB = b; prevY = y;
            }
            if (pedir) Recentrar();
        }

        private static bool Boton(UnityEngine.XR.XRNode nodo)
        {
            var d = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(nodo);
            return d.isValid && d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool v) && v;
        }

        [ContextMenu("Recentrar ahora")]
        public void Recentrar()
        {
            if (origen == null || camara == null) return;
            if (alinearYaw)
                origen.MatchOriginUpCameraForward(Vector3.up, Quaternion.Euler(0f, yawObjetivo, 0f) * Vector3.forward);
            origen.MoveCameraToWorldLocation(posicionCabeza);
            if (rig != null) rig.ReubicarAhora();
            if (brazoFisico != null) brazoFisico.Reiniciar();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(posicionCabeza, 0.1f);
            Gizmos.DrawRay(posicionCabeza, Quaternion.Euler(0f, yawObjetivo, 0f) * Vector3.forward * 0.4f);
        }
    }
}
