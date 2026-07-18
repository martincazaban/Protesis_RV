using UnityEngine;

[System.Serializable]
public class Finger
{
    public string name;

    public Transform[] joints;

    [HideInInspector]
    public Quaternion[] initialRotations;

    [Range(0,1)]
    public float curl;

    public bool blocked;
}

public class HandController : MonoBehaviour
{
    [Header("Configuración de Dedos")]
    [Tooltip("Arrastrá aquí todos los GameObjects que representan los dedos")]
    public Finger[] fingers;
    
    [Tooltip("El eje local sobre el cual se doblan los dedos (ej. 1,0,0 para X)")]
    public Vector3 rotationAxis = Vector3.left; 
    
    [Tooltip("Ángulo máximo de rotación cuando la mano está totalmente cerrada")]
    public float maxBendAngle = 90f; 
    
    [Tooltip("Velocidad a la que se cierra la mano con la rueda")]
    public float scrollSensitivity = 2f;

    // Valor normalizado del estado de la mano: 0 (abierta) a 1 (cerrada)
    private float handCloseValue = 0f; 
    
    void Start()
    {
        // Inicializamos el arreglo y guardamos la postura de reposo de cada falange
        
        foreach (Finger finger in fingers)
        {
            finger.initialRotations = new Quaternion[finger.joints.Length];

            for (int i = 0; i < finger.joints.Length; i++)
            {
                finger.initialRotations[i] = finger.joints[i].localRotation;
            }
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
            foreach (Finger finger in fingers)
            {
                finger.curl = handCloseValue;
            }
            UpdateFingersRotation();
        }
    }

    void UpdateFingersRotation()
    {
        // Aplicamos la rotación a cada falange de manera isocrónica
        foreach (Finger finger in fingers)
        {   
            // Calculamos el ángulo actual en base al estado de cierre (0% a 100%)
                float currentAngle = finger.curl * maxBendAngle;

                // Creamos la rotación que representa la flexión pura
                Quaternion flexRotation = Quaternion.AngleAxis(currentAngle, rotationAxis);

            for (int i = 0; i < finger.joints.Length; i++)
            {
                
                // Multiplicar Quaternions equivale a sumar sus rotaciones
                // Así mantenemos la orientación original de la pieza intacta
                finger.joints[i].localRotation = finger.initialRotations[i] * flexRotation;
            }

        }
    }
}

public class FingerCollision : MonoBehaviour
{
    public Finger finger;

    private int contacts;

    public bool IsTouching => contacts > 0;

    void OnTriggerEnter(Collider other)
    {
        contacts++;
    }

    void OnTriggerExit(Collider other)
    {
        contacts--;
    }
}