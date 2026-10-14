// ============================================================================
//  ProtesisRecenter.cs   -- v2
//  Rutina de referenciacion. Es la operacion mas importante del sistema.
//
//  POR QUE NO ALCANZA CON HACERLO UNA VEZ AL ARRANCAR
//  Los BMI160 son de 6 ejes: sin magnetometro no hay referencia absoluta de
//  rumbo, asi que el yaw sale de integrar el giroscopo y deriva. La IMU del
//  pecho cancela la parte comun de esa deriva, pero no la diferencia entre los
//  sesgos de los dos giroscopos, que se sigue acumulando. Recentrar la pone en
//  cero de nuevo. No es un bug: es como se comportan los sensores inerciales.
//
//  Ahora el tare vive en Unity, no en el firmware. El micro manda los dos
//  cuaterniones crudos y aca se guardan las referencias. Ventaja concreta: el
//  usuario puede recentrar cuantas veces quiera sin tocar el micro.
//
//  PROCEDIMIENTO PARA EL USUARIO
//    1. Mirar al frente, en la direccion de trabajo.
//    2. Brazo colgando relajado al costado.
//    3. Apretar la tecla de recentrado.
//
//  El "mirar al frente" importa: de ahi sale la alineacion de rumbo. Si
//  recentra mirando de costado, el brazo neutro va a apuntar de costado.
// ============================================================================

using UnityEngine;

public class ProtesisRecenter : MonoBehaviour
{
    [Header("Referencias")]
    public ProtesisLink link;
    public ProtesisArmDriver driver;

    [Header("Entrada")]
    public KeyCode recenterKey = KeyCode.R;
    [Tooltip("Recentrar solo la primera vez que llegan datos validos.")]
    public bool autoAlPrimerDato = true;

    [Header("Diagnóstico (solo lectura)")]
    public int veces;
    public float segundosDesdeElUltimo;

    bool _hizoAuto;
    float _tUltimo = -999f;

    void Reset()
    {
        link = FindObjectOfType<ProtesisLink>();
        driver = FindObjectOfType<ProtesisArmDriver>();
    }

    void Awake()
    {
        if (link == null) link = FindObjectOfType<ProtesisLink>();
        if (driver == null) driver = FindObjectOfType<ProtesisArmDriver>();
    }

    void Update()
    {
        if (Input.GetKeyDown(recenterKey)) Recentrar();

        if (autoAlPrimerDato && !_hizoAuto &&
            link != null && link.SchemaReady && link.Connected)
        {
            _hizoAuto = true;
            Recentrar();
        }

        segundosDesdeElUltimo = (veces > 0) ? Time.time - _tUltimo : 0f;
    }

    /// <summary>Referencia el sistema al estado actual. Enganchable a un boton
    /// de UI o a un gesto.</summary>
    public void Recentrar()
    {
        if (driver == null) return;

        driver.Recalibrar();
        veces++;
        _tUltimo = Time.time;
    }

    void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerRight };
        string extra = (veces > 0 && driver != null)
            ? $"   deriva descontada: {driver.yawDescontadoDeg:F1} deg"
            : "";
        GUI.Label(new Rect(Screen.width - 420, Screen.height - 30, 410, 20),
                  $"[{recenterKey}] recentrar   ({veces}){extra}", style);
    }
}
