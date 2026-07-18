using UnityEngine;


public class HandController : MonoBehaviour
{
    [Header("Configuración de Dedos")]
    [Tooltip("Arrastrá aquí todos los GameObjects que representan las falanges")]
    public Transform[] phalanges; 
    
    [Tooltip("El eje local sobre el cual se doblan los dedos (ej. 1,0,0 para X)")]
    public Vector3 rotationAxis = Vector3.right; 
    
    [Tooltip("Ángulo máximo de rotación cuando la mano está totalmente cerrada")]
    public float maxBendAngle = 90f; 
    
    [Tooltip("Velocidad a la que se cierra la mano con la rueda")]
    public float scrollSensitivity = 2f;

    // Valor normalizado del estado de la mano: 0 (abierta) a 1 (cerrada)
    private float handCloseValue = 0f; 
    
    // Almacenamos las rotaciones iniciales (offset) de cada pieza
    private Quaternion[] initialRotations;

    void Start()
    {
        // Inicializamos el arreglo y guardamos la postura de reposo de cada falange
        initialRotations = new Quaternion[phalanges.Length];
        for (int i = 0; i < phalanges.Length; i++)
        {
            initialRotations[i] = phalanges[i].localRotation;
        }
    }

    void Update()
    {
        // 1. Leer el input de la rueda del mouse (Sistema Clásico)
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        
        if (scroll != 0)
        {
            // Acumular el valor y limitarlo estrictamente entre 0 y 1
            handCloseValue += scroll * scrollSensitivity;
            handCloseValue = Mathf.Clamp(handCloseValue, 0f, 1f);

            // 2. Actualizar la rotación de la mano
            UpdateFingersRotation();
        }
    }

    void UpdateFingersRotation()
    {
        // Calculamos el ángulo actual en base al estado de cierre (0% a 100%)
        float currentAngle = handCloseValue * maxBendAngle;
        
        // Creamos la rotación que representa la flexión pura
        Quaternion flexRotation = Quaternion.AngleAxis(currentAngle, rotationAxis);

        // Aplicamos la rotación a cada falange de manera isocrónica
        for (int i = 0; i < phalanges.Length; i++)
        {
            // Multiplicar Quaternions equivale a sumar sus rotaciones
            // Así mantenemos la orientación original de la pieza intacta
            phalanges[i].localRotation = initialRotations[i] * flexRotation;
        }
    }
}