// ============================================================================
//  ProtesisRig.cs
//  Arma y ubica el esqueleto virtual a partir del perfil de usuario.
//
//  JERARQUIA
//    RigRoot (USUARIO VR)
//    ├── Camera Offset → Main Camera     <- visor, independiente
//    └── Torso            (FIJO)         <- marco de referencia
//        ├── MarcadorPecho               <- solo visual
//        └── AnclaHombro                 <- centro articular (ojos + offset)
//            └── PivoteBrazo             <- recibe la orientacion (imuTarget)
//                └── PROTESIS            <- modelo, desplazado para que el
//                    │                      hueso "Brazo" caiga en el pivote
//                    └── MarcadorImuBrazo <- solo visual
//
//  POR QUE EL TORSO NO ROTA
//  Esta es la parte contraintuitiva. La IMU del pecho sirve para CANCELAR la
//  deriva de yaw del brazo, no para animar el torso. La cuenta:
//
//      medido_pecho = Rz(dC) * real_pecho
//      medido_brazo = Rz(dA) * real_brazo
//      relativo     = conj(medido_pecho) * medido_brazo
//                   = conj(real_pecho) * Rz(dA - dC) * real_brazo
//
//  Si los dos sensores derivan parecido, dA - dC se va a cero y queda la
//  orientacion real del brazo respecto del torso real. Pero si despues rotamos
//  el torso virtual con el cuaternion del pecho, el mundo nos devuelve
//  Rz(dA) * real_brazo, o sea el cuaternion crudo del brazo con toda su deriva:
//  la correccion se deshace sola.
//
//  Con el usuario sentado el torso casi no se mueve, asi que no se pierde nada
//  visual y la correccion se conserva entera.
// ============================================================================

using UnityEngine;

[DefaultExecutionOrder(50)]
public class ProtesisRig : MonoBehaviour
{
    [Header("Referencias de escena")]
    [Tooltip("La camara del visor. Vacio = Camera.main.")]
    public Transform head;
    [Tooltip("Nodo fijo que hace de torso. Padre del ancla y del marcador de pecho.")]
    public Transform torso;
    [Tooltip("Centro articular del hombro. Se ubica en ojos + offset del perfil.")]
    public Transform anclaHombro;
    [Tooltip("Hijo de AnclaHombro y padre de PROTESIS. Es el imuTarget del " +
             "ProtesisArmDriver: la orientacion del muñon se aplica aca.")]
    public Transform pivoteBrazo;
    [Tooltip("El modelo del brazo.")]
    public Transform protesis;

    [Header("Marcadores (opcionales, solo visuales)")]
    public Transform marcadorPecho;
    public Transform marcadorImuBrazo;

    [Header("Offsets respecto del centro de los ojos (metros)")]
    public Vector3 offsetHombro = new Vector3(0.18f, -0.22f, 0f);
    public Vector3 offsetPecho = new Vector3(0.00f, -0.32f, 0f);
    public Vector3 offsetImuBrazo = new Vector3(0.20f, -0.38f, 0f);

    [Header("Perfil de usuario")]
    public bool usarPerfil = true;
    [Range(0f, 1f)] public float confianzaMinima = 0.6f;

    [Header("Escala del modelo")]
    [Tooltip("Hueso donde nace el brazo (tope del socket). Es el pivote real.")]
    public string huesoPivote = "Brazo";
    [Tooltip("Hueso de la punta del dedo medio, para medir el largo total.")]
    public string huesoPunta = "Falange Up Medio_end";
    [Tooltip("Largo hueso Brazo -> punta del medio de BRAZO_2.fbx, en metros reales " +
             "(calculado de la jerarquia del FBX: 46.9 cm).")]
    public float largoEsperadoM = 0.469f;
    [Tooltip("Avisa en consola si el largo medido se desvia mas que esto.")]
    [Range(0.02f, 0.5f)] public float toleranciaEscala = 0.10f;

    [Header("Diagnóstico (solo lectura)")]
    public string origenOffsets = "inspector";
    public float largoMedidoM;
    public float distanciaPivoteAAnclaM;

    void Start()
    {
        if (head == null && Camera.main != null) head = Camera.main.transform;
        if (head == null)
        {
            Debug.LogError("[ProtesisRig] Falta la referencia a la camara.");
            enabled = false;
            return;
        }

        if (usarPerfil) CargarPerfil();
        Ubicar();
        VerificarEscala(false);

        Debug.Log($"[ProtesisRig] hombro {offsetHombro} | pecho {offsetPecho} | " +
                  $"imu brazo {offsetImuBrazo}  (fuente: {origenOffsets})");
    }

    [ContextMenu("Recargar perfil y reubicar")]
    public void RecargarYUbicar()
    {
        if (head == null && Camera.main != null) head = Camera.main.transform;
        CargarPerfil();
        Ubicar();
    }

    void CargarPerfil()
    {
        PerfilUsuario p = ProtesisPerfil.Cargar(out string ruta);

        if (p == null)
        {
            origenOffsets = "inspector (no hay perfil)";
            return;
        }
        if (!ProtesisPerfil.EsPlausible(p, out string motivo))
        {
            Debug.LogWarning($"[ProtesisRig] Perfil descartado, {motivo}. " +
                             $"Se usan los offsets del inspector.");
            origenOffsets = "inspector (perfil implausible)";
            return;
        }
        if (p.confianza < confianzaMinima)
        {
            Debug.LogWarning($"[ProtesisRig] Perfil con confianza {p.confianza:F2}, " +
                             $"por debajo de {confianzaMinima:F2}. Se usan los " +
                             $"offsets del inspector. Conviene repetir la " +
                             $"calibracion o medir con cinta.");
            origenOffsets = "inspector (confianza baja)";
            return;
        }

        offsetHombro = p.offset_hombro_m;
        if (p.offset_pecho_m != Vector3.zero) offsetPecho = p.offset_pecho_m;
        if (p.offset_imu_brazo_m != Vector3.zero) offsetImuBrazo = p.offset_imu_brazo_m;

        origenOffsets = $"perfil: {p.usuario} (conf {p.confianza:F2})";
        Debug.Log($"[ProtesisRig] {p}\n  leido de {ruta}");
    }

    void Ubicar()
    {
        if (torso == null || anclaHombro == null) return;

        Vector3 origen = head.position;

        // El torso pivota en el punto de la cabeza. No rota nunca: su rotacion
        // queda en identidad a proposito (ver el comentario de arriba).
        torso.position = origen;
        torso.rotation = Quaternion.identity;

        anclaHombro.position = origen + offsetHombro;
        anclaHombro.rotation = Quaternion.identity;

        AlinearModeloAlPivote();

        if (marcadorPecho != null)
            marcadorPecho.position = origen + offsetPecho;

        // El marcador del brazo va colgado de PROTESIS para que acompañe su
        // rotacion. InverseTransformPoint se encarga de la escala del modelo,
        // que no tiene por que ser 1.
        if (marcadorImuBrazo != null && protesis != null)
        {
            if (marcadorImuBrazo.parent != protesis)
                marcadorImuBrazo.SetParent(protesis, true);
            marcadorImuBrazo.position = origen + offsetImuBrazo;
        }
    }

    // ======================= ESCALA DEL MODELO ===============================
    // Los offsets del perfil estan en metros reales, asi que el modelo tiene que
    // medir lo mismo en la escena. El FBX (BRAZO_2) ya esta en tamaño real: desde
    // el hueso Brazo hasta la punta del dedo medio son 0.469 m. Con la
    // importacion actual (Scale Factor 100 + Convert Units) eso significa que
    // PROTESIS va en escala 1. En vez de adivinar la unidad de importacion, se
    // MIDE el largo en la escena y se corrige hasta que coincida.

    bool BuscarHuesos(out Transform pivote, out Transform punta)
    {
        pivote = null; punta = null;
        if (protesis == null) return false;
        foreach (Transform t in protesis.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == huesoPivote) pivote = t;
            else if (t.name == huesoPunta) punta = t;
        }
        return pivote != null && punta != null;
    }

    /// <summary>Mide el largo del brazo en la escena. Devuelve false si faltan huesos.</summary>
    public bool MedirLargo(out float largo)
    {
        largo = 0f;
        if (!BuscarHuesos(out Transform pivote, out Transform punta)) return false;
        largo = Vector3.Distance(pivote.position, punta.position);
        largoMedidoM = largo;
        if (anclaHombro != null)
            distanciaPivoteAAnclaM = Vector3.Distance(pivote.position, anclaHombro.position);
        return true;
    }

    void VerificarEscala(bool detallado)
    {
        if (!MedirLargo(out float largo))
        {
            if (detallado)
                Debug.LogWarning($"[ProtesisRig] No encuentro los huesos '{huesoPivote}' / " +
                                 $"'{huesoPunta}' bajo PROTESIS. Revisar los nombres.");
            return;
        }

        float ratio = largo / largoEsperadoM;
        bool bien = Mathf.Abs(ratio - 1f) <= toleranciaEscala;

        string msg = $"[ProtesisRig] Escala del modelo: largo medido {largo:F3} m, " +
                     $"esperado {largoEsperadoM:F3} m (x{ratio:F2}). " +
                     $"lossyScale de PROTESIS = {protesis.lossyScale}.";

        if (bien)
        {
            if (detallado) Debug.Log(msg + " OK.");
        }
        else
        {
            Debug.LogWarning(msg + " Fuera de tolerancia. Menu contextual de ProtesisRig: " +
                             "'Normalizar escala del modelo'.");
        }

        if (detallado)
            Debug.Log($"[ProtesisRig] El hueso '{huesoPivote}' esta a {distanciaPivoteAAnclaM:F3} m " +
                      $"del AnclaHombro. Si no es ~0, el brazo gira alrededor de otro punto " +
                      $"que no es el hombro (ver pendiente del pivote).");
    }

    [ContextMenu("Diagnosticar escala")]
    public void DiagnosticarEscala() => VerificarEscala(true);

    [ContextMenu("Normalizar escala del modelo")]
    public void NormalizarEscala()
    {
        if (!MedirLargo(out float largo) || largo < 1e-4f)
        {
            Debug.LogWarning("[ProtesisRig] No pude medir el brazo, no se toca la escala.");
            return;
        }

#if UNITY_EDITOR
        UnityEditor.Undo.RecordObject(protesis, "Normalizar escala PROTESIS");
#endif
        float factor = largoEsperadoM / largo;
        Vector3 antes = protesis.localScale;
        protesis.localScale = antes * factor;

        // El marcador cuelga de PROTESIS: se reubica para que la escala no lo mueva.
        Ubicar();
        MedirLargo(out float despues);

        Debug.Log($"[ProtesisRig] Escala de PROTESIS {antes} -> {protesis.localScale} " +
                  $"(largo {largo:F3} m -> {despues:F3} m, objetivo {largoEsperadoM:F3} m).");
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(protesis);
#endif
    }

    // ======================= PIVOTE DEL BRAZO ===============================
    // El origen del FBX queda a la altura de los dedos, no en el hombro. Si se
    // rotara PROTESIS directamente, el brazo giraria alrededor de la mano. Por
    // eso la orientacion se aplica a PivoteBrazo y PROTESIS se desplaza dentro
    // de el hasta que el hueso "Brazo" (tope del socket) coincida con el pivote.
    // Se recalcula en cada arranque: si cambia el perfil, la escala o la
    // rotacion de reposo, el hombro sigue cayendo en su lugar sin tocar nada.

    [ContextMenu("Alinear modelo al pivote")]
    public void AlinearModeloAlPivote()
    {
        if (pivoteBrazo == null || protesis == null) return;
        if (anclaHombro != null && pivoteBrazo.parent == anclaHombro)
            pivoteBrazo.localPosition = Vector3.zero;

        if (!BuscarHuesos(out Transform hueso, out _))
        {
            Debug.LogWarning($"[ProtesisRig] No encuentro el hueso '{huesoPivote}', " +
                             "no se puede alinear el modelo al pivote.");
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.Undo.RecordObject(protesis, "Alinear modelo al pivote");
#endif
        protesis.position += pivoteBrazo.position - hueso.position;
        MedirLargo(out _);
    }

    [ContextMenu("Crear nodos que falten")]
    void CrearNodos()
    {
        if (torso == null)
        {
            var go = new GameObject("Torso");
            go.transform.SetParent(transform, false);
            torso = go.transform;
        }
        if (anclaHombro == null)
        {
            var go = new GameObject("AnclaHombro");
            go.transform.SetParent(torso, false);
            anclaHombro = go.transform;
        }
        if (pivoteBrazo == null)
        {
            var go = new GameObject("PivoteBrazo");
            go.transform.SetParent(anclaHombro, false);
            pivoteBrazo = go.transform;
        }
        if (marcadorPecho == null)
        {
            var go = new GameObject("MarcadorPecho");
            go.transform.SetParent(torso, false);
            go.AddComponent<ImuMarker>();
            marcadorPecho = go.transform;
        }
        if (marcadorImuBrazo == null && protesis != null)
        {
            var go = new GameObject("MarcadorImuBrazo");
            go.transform.SetParent(protesis, false);
            go.AddComponent<ImuMarker>();
            marcadorImuBrazo = go.transform;
        }
        Debug.Log("[ProtesisRig] Nodos creados. Falta arrastrar PROTESIS bajo PivoteBrazo " +
                  "y poner PivoteBrazo como imuTarget del ProtesisArmDriver.");
    }

    void OnDrawGizmosSelected()
    {
        if (head == null) return;
        Vector3 o = head.position;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(o, o + offsetHombro);
        Gizmos.DrawWireSphere(o + offsetHombro, 0.035f);

        Gizmos.color = new Color(1f, 0.6f, 0.2f);
        Gizmos.DrawLine(o, o + offsetPecho);
        Gizmos.DrawWireSphere(o + offsetPecho, 0.025f);

        Gizmos.color = new Color(0.3f, 0.7f, 1f);
        Gizmos.DrawLine(o + offsetHombro, o + offsetImuBrazo);
        Gizmos.DrawWireSphere(o + offsetImuBrazo, 0.025f);
    }
}
