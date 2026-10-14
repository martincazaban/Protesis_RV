// ============================================================================
//  ImuMarker.cs
//  Marcador visual de la IMU. Es una ayuda de depuracion, NO participa de la
//  cinematica.
//
//  Por que no hace falta para que funcione: el socket y el munon forman un
//  cuerpo rigido, y la orientacion de un cuerpo rigido es la misma en todos sus
//  puntos. Da igual que el sensor este pegado cerca del codo o cerca del
//  hombro: reporta la rotacion del segmento completo. Lo que define como se
//  mueve el brazo es el PIVOTE (el centro del hombro, que resuelve el ancla),
//  no donde este fisicamente el sensor.
//
//  Para que sirve igual: al ajustar la convencion de ejes del BNO080, ver una
//  terna dibujada sobre el biceps hace evidente si el mapeo quedo espejado.
//  Mirando el brazo entero se adivina; mirando los ejes se ve.
//
//  USO
//    1. GameObject vacio como hijo de "Brazo" (o del hueso del biceps).
//    2. Moverlo hasta donde va fisicamente el sensor, un poco arriba del codo.
//    3. Ponerle este componente.
//    4. Con Gizmos activados, en Scene view se ven los ejes durante el Play.
//
//  Convencion de colores, la misma que usa Unity:
//    rojo = X    verde = Y    azul = Z
// ============================================================================

using UnityEngine;

public class ImuMarker : MonoBehaviour
{
    [Header("Apariencia")]
    public float largoEjes = 0.08f;
    [Tooltip("Cubito que representa el encapsulado del sensor.")]
    public Vector3 tamañoCaja = new Vector3(0.02f, 0.006f, 0.028f);
    public bool mostrarSiempre = true;

    [Header("Referencia")]
    [Tooltip("Opcional. Si se asigna, tambien dibuja la orientacion cruda que " +
             "llega del DB, para comparar contra la del modelo. Si las dos " +
             "ternas no se mueven igual, la convencion de ejes esta mal.")]
    public ProtesisLink link;
    public bool dibujarOrientacionCruda = false;

    void Update()
    {
        if (!mostrarSiempre) return;

        // Debug.DrawRay se ve en Scene view durante el Play.
        Debug.DrawRay(transform.position, transform.right * largoEjes, Color.red);
        Debug.DrawRay(transform.position, transform.up * largoEjes, Color.green);
        Debug.DrawRay(transform.position, transform.forward * largoEjes, Color.blue);

        if (dibujarOrientacionCruda && link != null && link.SchemaReady)
        {
            // Cuaternion tal cual llega, sin conversion de sistema. Comparar
            // esta terna con la de arriba muestra que hace la conversion.
            Quaternion cruda = new Quaternion(
                link.Get("q_i"), link.Get("q_j"), link.Get("q_k"), link.Get("q_r"));

            if (cruda.x * cruda.x + cruda.y * cruda.y +
                cruda.z * cruda.z + cruda.w * cruda.w > 0.01f)
            {
                float l = largoEjes * 0.6f;
                Vector3 o = transform.position + Vector3.up * 0.03f;
                Debug.DrawRay(o, cruda * Vector3.right * l, new Color(1f, 0.5f, 0.5f));
                Debug.DrawRay(o, cruda * Vector3.up * l, new Color(0.5f, 1f, 0.5f));
                Debug.DrawRay(o, cruda * Vector3.forward * l, new Color(0.5f, 0.5f, 1f));
            }
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;

        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(Vector3.zero, tamañoCaja);

        Gizmos.color = Color.red;
        Gizmos.DrawLine(Vector3.zero, Vector3.right * largoEjes);
        Gizmos.color = Color.green;
        Gizmos.DrawLine(Vector3.zero, Vector3.up * largoEjes);
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(Vector3.zero, Vector3.forward * largoEjes);

        Gizmos.matrix = Matrix4x4.identity;
    }
}
