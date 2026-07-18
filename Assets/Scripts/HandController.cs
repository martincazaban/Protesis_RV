using UnityEngine;
using System.Collections.Generic; // Necesario para el Dictionary

[System.Serializable]
public class Finger
{
    public string name;
    public Transform[] joints;

    [HideInInspector]
    public Quaternion[] initialRotations;

    [HideInInspector]
    public FingerCollision[] collisionSensors; // Arreglo para leer los sensores de este dedo

    [Range(0, 1)]
    public float curl;

    public bool blocked;
}

public class HandController : MonoBehaviour
{
    [Header("Configuración de Dedos")]
    [Tooltip("Arrastrá aquí todos los GameObjects que representan los dedos")]
    public Finger[] fingers;

    [Tooltip("El eje local sobre el cual se doblan los dedos")]
    public Vector3 rotationAxis = Vector3.left;

    [Tooltip("Ángulo máximo de rotación cuando la mano está totalmente cerrada")]
    public float maxBendAngle = 90f;

    [Tooltip("Velocidad a la que se cierra la mano con la rueda")]
    public float scrollSensitivity = 2f;

    [Header("Sistema de Agarre (Joints)")]
    [Tooltip("El punto central de la palma donde se creará el Joint")]
    public Transform palmPoint;

    [Tooltip("Porcentaje mínimo de cápsulas que deben tocar el objeto (ej. 0.4 = 40%)")]
    [Range(0.1f, 1f)]
    public float contactThreshold = 0.4f;

    [Tooltip("Nivel mínimo de cierre de la mano para permitir agarrar (0 = abierta, 1 = cerrada)")]
    [Range(0.1f, 0.9f)]
    public float minimumCurlToGrab = 0.2f;

    private float handCloseValue = 0f;
    private int totalCapsules = 0;
    
    // Variables para el Joint
    private FixedJoint currentJoint;
    private Rigidbody grabbedObject;

    void Start()
    {
        // Inicializamos arreglos, guardamos posturas y contamos cápsulas
        foreach (Finger finger in fingers)
        {
            finger.initialRotations = new Quaternion[finger.joints.Length];
            finger.collisionSensors = new FingerCollision[finger.joints.Length];

            for (int i = 0; i < finger.joints.Length; i++)
            {
                finger.initialRotations[i] = finger.joints[i].localRotation;
                
                // Obtenemos el script de colisión que debe estar en el mismo GameObject que el joint
                finger.collisionSensors[i] = finger.joints[i].GetComponent<FingerCollision>();
                
                if (finger.collisionSensors[i] != null)
                {
                    totalCapsules++;
                }
            }
        }
    }

    void Update()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (scroll != 0)
        {
            handCloseValue += scroll * scrollSensitivity;
            handCloseValue = Mathf.Clamp(handCloseValue, 0f, 1f);

            foreach (Finger finger in fingers)
            {
                finger.curl = handCloseValue;
            }
            UpdateFingersRotation();
        }

        // Lógica de evaluación continua para agarrar o soltar
        EvaluateGrabLogic();
    }

    void UpdateFingersRotation()
    {
        foreach (Finger finger in fingers)
        {
            float currentAngle = finger.curl * maxBendAngle;
            Quaternion flexRotation = Quaternion.AngleAxis(currentAngle, rotationAxis);

            for (int i = 0; i < finger.joints.Length; i++)
            {
                finger.joints[i].localRotation = finger.initialRotations[i] * flexRotation;
            }
        }
    }

    void EvaluateGrabLogic()
    {
        // 1. Si NO tenemos nada agarrado y la mano está lo suficientemente cerrada
        if (currentJoint == null && handCloseValue >= minimumCurlToGrab)
        {
            // Diccionario para contar cuántas cápsulas tocan cada Rigidbody
            Dictionary<Rigidbody, int> contactCounts = new Dictionary<Rigidbody, int>();

            // Recorremos todos los sensores de todos los dedos
            foreach (Finger finger in fingers)
            {
                foreach (FingerCollision sensor in finger.collisionSensors)
                {
                    if (sensor != null && sensor.touchedRigidbody != null)
                    {
                        Rigidbody targetRb = sensor.touchedRigidbody;
                        if (!contactCounts.ContainsKey(targetRb))
                        {
                            contactCounts[targetRb] = 0;
                        }
                        contactCounts[targetRb]++;
                    }
                }
            }

            // Verificamos si algún objeto superó el porcentaje requerido
            foreach (KeyValuePair<Rigidbody, int> contact in contactCounts)
            {
                float contactPercentage = (float)contact.Value / totalCapsules;

                if (contactPercentage >= contactThreshold)
                {
                    GrabObject(contact.Key);
                    break; // Agarramos el primero que cumpla la condición y salimos
                }
            }
        }
        // 2. Si TENEMOS algo agarrado y abrimos la mano por debajo del límite
        else if (currentJoint != null && handCloseValue < minimumCurlToGrab)
        {
            DropObject();
        }
    }

    void GrabObject(Rigidbody targetRb)
    {
        grabbedObject = targetRb;

        // Creamos el Joint en la palma y lo conectamos al objeto
        currentJoint = palmPoint.gameObject.AddComponent<FixedJoint>();
        currentJoint.connectedBody = grabbedObject;
    }

    void DropObject()
    {
        if (currentJoint != null)
        {
            Destroy(currentJoint);
            currentJoint = null;
            grabbedObject = null;
        }
    }
}