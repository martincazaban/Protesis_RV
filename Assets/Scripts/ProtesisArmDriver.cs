// ============================================================================
//  ProtesisArmDriver.cs   -- v3, dos cuaterniones
//
//  Aplica al brazo:
//    - los 3 grados de libertad (codo, muñeca, mano) en grados
//    - la orientacion del munon, corregida con la IMU del pecho
//
//  LA CUENTA
//    Los dos cuaterniones llegan crudos y absolutos, tal como los reporta cada
//    sensor. Toda la calibracion vive aca, que es lo correcto: recentrar es una
//    accion del usuario y no tiene que depender de reflashear el micro.
//
//      dA = qA * conj(qA0)          delta de mundo del brazo desde la calibracion
//      dC = qC * conj(qC0)          delta de mundo del pecho
//      dCyaw = solo el yaw de dC
//      corregido = conj(dCyaw) * dA
//      final = A * corregido * conj(A)      A = alineacion de rumbo
//      brazo.localRotation = final * reposo
//
//    Son deltas de MUNDO (multiplicacion por izquierda), no de cuerpo. Es lo que
//    hace que la deriva se cancele: la deriva de yaw aparece como una rotacion
//    por izquierda en los dos sensores, y conj(dCyaw)*dA se come la parte comun.
//
//  POR QUE SOLO EL YAW DEL PECHO
//    El acelerometro referencia roll y pitch contra la gravedad, que es una
//    referencia absoluta y permanente: esos dos ejes NO derivan. El unico que se
//    va es el yaw. Descontarle al brazo la orientacion completa del pecho le
//    sacaria tambien la inclinacion real, que es informacion buena y gratis.
//
//  Va en el GameObject PROTESIS, junto al HandController.
// ============================================================================

using UnityEngine;

public class ProtesisArmDriver : MonoBehaviour
{
    public enum Convencion { FLU_a_Unity, Directa, SwapYZ, SwapXZ }

    [Header("Referencias")]
    public ProtesisLink link;
    public HandController hand;
    [Tooltip("Que transform recibe la orientacion del munon. Normalmente este mismo.")]
    public Transform imuTarget;

    [Header("Claves del DB")]
    public string keyElbow = "ang_elbow";
    public string keyWrist = "ang_wrist";
    public string keyHand = "ang_hand";
    public string keyArmQuat = "qa";      // espera qa_i, qa_j, qa_k, qa_r
    public string keyChestQuat = "qc";    // espera qc_i, qc_j, qc_k, qc_r

    [Header("Grados de libertad")]
    public bool driveJoints = true;
    [Tooltip("Suavizado de los angulos. 0 = crudo. Con potes reales conviene " +
             "10-20 para filtrar el ruido del ADC sin retardo notorio.")]
    public float smoothing = 15f;
    public bool invertElbow = false;
    public bool invertWrist = false;
    public bool invertHand = false;

    [Header("Orientación del muñón")]
    public bool applyImuOrientation = true;
    [Tooltip("FLU_a_Unity es la correcta para el simulador (X adelante, " +
             "Y izquierda, Z arriba, mano derecha). Con sensores reales depende " +
             "del filtro de fusion y de como queden pegados.")]
    public Convencion convencion = Convencion.FLU_a_Unity;
    public float imuSmoothing = 0f;

    [Header("Corrección de deriva")]
    [Tooltip("Descontar el yaw del pecho. Apagarlo muestra la deriva cruda, " +
             "sirve para comprobar que la correccion hace algo.")]
    public bool corregirConPecho = true;

    [Header("Alineación de rumbo")]
    [Tooltip("Se calcula solo al recentrar, a partir del rumbo del pecho.")]
    public bool autoAlinearYaw = true;
    public float yawAlignDeg = 0f;

    [Header("Diagnóstico (solo lectura)")]
    public float elbowDeg;
    public float wristDeg;
    public float handDeg;
    public bool calibrado;
    [Tooltip("Yaw acumulado que se le esta descontando al brazo. Con SIM_DRIFT " +
             "activo tiene que ir creciendo lentamente.")]
    public float yawDescontadoDeg;
    public bool receiving;

    Quaternion _rest = Quaternion.identity;
    Quaternion _qA0 = Quaternion.identity;
    Quaternion _qC0 = Quaternion.identity;
    Quaternion _current = Quaternion.identity;
    bool _currentInit;
    bool _warned;

    void Reset()
    {
        link = FindObjectOfType<ProtesisLink>();
        hand = GetComponent<HandController>();
        imuTarget = transform;
    }

    void Awake()
    {
        if (link == null) link = FindObjectOfType<ProtesisLink>();
        if (hand == null) hand = GetComponent<HandController>();
        if (imuTarget == null) imuTarget = transform;

        // Pose de reposo tal como quedo armada en la escena. El brazo rota A
        // PARTIR de aca, asi no se pierde el calce que ajustaste a mano.
        _rest = imuTarget.localRotation;

        if (hand != null) hand.inputSource = HandController.InputSource.Protesis;
    }

    void Update()
    {
        if (link == null) return;

        receiving = link.Connected;
        if (!link.SchemaReady) return;

        CheckKeys();

        if (driveJoints && hand != null) AplicarArticulaciones();
        if (applyImuOrientation && imuTarget != null) AplicarOrientacion();
    }

    // ========================= GRADOS DE LIBERTAD ===========================
    void AplicarArticulaciones()
    {
        float e = link.Get(keyElbow) * (invertElbow ? -1f : 1f);
        float w = link.Get(keyWrist) * (invertWrist ? -1f : 1f);
        float h = link.Get(keyHand) * (invertHand ? -1f : 1f);

        if (smoothing > 0f)
        {
            float k = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            elbowDeg = Mathf.Lerp(elbowDeg, e, k);
            wristDeg = Mathf.Lerp(wristDeg, w, k);
            handDeg = Mathf.Lerp(handDeg, h, k);
        }
        else
        {
            elbowDeg = e; wristDeg = w; handDeg = h;
        }

        hand.SetElbowAngle(elbowDeg);
        hand.SetWristAngle(wristDeg);
        hand.SetHandAngle(handDeg);
    }

    // ========================== ORIENTACION =================================
    void AplicarOrientacion()
    {
        if (!TryLeerQuat(keyArmQuat, out Quaternion qA)) return;
        if (!TryLeerQuat(keyChestQuat, out Quaternion qC)) qC = Quaternion.identity;

        if (!calibrado) CapturarCalibracion(qA, qC);

        // Deltas de mundo desde la calibracion.
        Quaternion dA = qA * Quaternion.Inverse(_qA0);
        Quaternion dC = qC * Quaternion.Inverse(_qC0);

        Quaternion dCyaw = corregirConPecho ? SoloYaw(dC) : Quaternion.identity;
        yawDescontadoDeg = Mathf.DeltaAngle(0f, dCyaw.eulerAngles.y);

        Quaternion corregido = Quaternion.Inverse(dCyaw) * dA;

        Quaternion A = Quaternion.Euler(0f, yawAlignDeg, 0f);
        Quaternion objetivo = (A * corregido * Quaternion.Inverse(A)) * _rest;

        if (!_currentInit) { _current = objetivo; _currentInit = true; }
        else if (imuSmoothing > 0f)
            _current = Quaternion.Slerp(_current, objetivo,
                          1f - Mathf.Exp(-imuSmoothing * Time.deltaTime));
        else
            _current = objetivo;

        imuTarget.localRotation = _current;
    }

    /// <summary>Toma la orientacion fisica actual como referencia. La llama
    /// ProtesisRecenter; el usuario tiene que estar con el brazo colgando al
    /// costado y mirando al frente.</summary>
    public void Recalibrar()
    {
        if (link == null || !link.SchemaReady) return;
        if (!TryLeerQuat(keyArmQuat, out Quaternion qA)) return;
        if (!TryLeerQuat(keyChestQuat, out Quaternion qC)) qC = Quaternion.identity;
        CapturarCalibracion(qA, qC);
    }

    void CapturarCalibracion(Quaternion qA, Quaternion qC)
    {
        _qA0 = qA;
        _qC0 = qC;
        calibrado = true;
        _currentInit = false;

        if (autoAlinearYaw)
        {
            // A lleva el rumbo que tenia el pecho al calibrar hasta el frente de
            // Unity. Asume que el usuario miraba al frente en ese momento y que
            // el eje Z del sensor del pecho apunta hacia adelante. En simulacion
            // ambas cosas se cumplen y A sale identidad.
            Vector3 f = qC * Vector3.forward;
            f.y = 0f;
            if (f.sqrMagnitude > 1e-6f)
                yawAlignDeg = -Quaternion.LookRotation(f.normalized, Vector3.up).eulerAngles.y;
            else
                yawAlignDeg = 0f;
        }

        Debug.Log($"[ProtesisArmDriver] Calibrado. alineacion yaw = {yawAlignDeg:F1} deg");
    }

    // =========================== UTILIDADES =================================
    bool TryLeerQuat(string prefijo, out Quaternion q)
    {
        q = Quaternion.identity;

        float x = link.Get(prefijo + "_i");
        float y = link.Get(prefijo + "_j");
        float z = link.Get(prefijo + "_k");
        float w = link.Get(prefijo + "_r");

        // Cuaternion nulo = todavia no llego nada valido
        if (x * x + y * y + z * z + w * w < 0.01f) return false;

        q = Convertir(x, y, z, w);
        return true;
    }

    /// <summary>Convierte del marco del sensor al de Unity.
    /// FLU_a_Unity: sensor derecho con X adelante, Y izquierda, Z arriba.
    /// El mapa de ejes es (x,y,z) -> (-y, z, x) y la w se niega para invertir
    /// la mano. Verificado numericamente contra el simulador.</summary>
    Quaternion Convertir(float x, float y, float z, float w)
    {
        switch (convencion)
        {
            case Convencion.FLU_a_Unity: return new Quaternion(-y, z, x, -w);
            case Convencion.SwapYZ: return new Quaternion(-x, -z, -y, w);
            case Convencion.SwapXZ: return new Quaternion(-z, -y, -x, w);
            default: return new Quaternion(x, y, z, w);
        }
    }

    /// <summary>Componente de yaw de una rotacion, sin pasar por angulos de
    /// Euler (que se rompen cerca de la vertical).</summary>
    static Quaternion SoloYaw(Quaternion q)
    {
        Vector3 f = q * Vector3.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 1e-6f)
        {
            f = q * Vector3.up;          // mirando casi vertical
            f.y = 0f;
            if (f.sqrMagnitude < 1e-6f) return Quaternion.identity;
        }
        return Quaternion.LookRotation(f.normalized, Vector3.up);
    }

    void CheckKeys()
    {
        if (_warned) return;
        bool falta = !link.Has(keyElbow) || !link.Has(keyWrist) || !link.Has(keyHand)
                  || !link.Has(keyArmQuat + "_r");
        if (falta)
        {
            Debug.LogWarning($"[ProtesisArmDriver] Al esquema le falta alguna clave: " +
                             $"{keyElbow}, {keyWrist}, {keyHand}, {keyArmQuat}_i..r. " +
                             $"Revisar la tabla DB[] del firmware.");
            _warned = true;
        }
    }

    // --- Para enganchar a botones de UI ---
    public void CongelarSimulacion() => link?.Send("SWEEP 0");
    public void ReanudarSimulacion() => link?.Send("SWEEP 1");

    public void UsarMouse()
    {
        if (hand != null) hand.inputSource = HandController.InputSource.Mouse;
        driveJoints = false;
    }

    public void UsarProtesis()
    {
        if (hand != null) hand.inputSource = HandController.InputSource.Protesis;
        driveJoints = true;
    }
}
