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

    [Header("Movimiento")]
    public float moveSpeed = 2f;

    [Header("Rotación")]
    public float rotationSpeed = 100f;

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

    private bool rotating = false;
    
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

        /*MoveHand();*/
        HandleRotationMode();
    }

    private void MoveHand()
    {
        Vector3 movement = Vector3.zero;

        // Adelante / atrás
        if (Input.GetKey(KeyCode.W))
            movement += Vector3.forward;

        if (Input.GetKey(KeyCode.S))
            movement += Vector3.back;

        // Izquierda / derecha
        if (Input.GetKey(KeyCode.A))
            movement += Vector3.left;

        if (Input.GetKey(KeyCode.D))
            movement += Vector3.right;

        // Arriba / abajo
        if (Input.GetKey(KeyCode.E))
            movement += Vector3.up;

        if (Input.GetKey(KeyCode.Q))
            movement += Vector3.down;

        // Normalizar para evitar que diagonal sea más rápida
        if (movement.magnitude > 1f)
            movement.Normalize();

        transform.position += movement * moveSpeed * Time.deltaTime;
    }

    private void HandleRotationMode()
    {
        bool mousePressed =
            Input.GetMouseButton(1);

        // ALT acaba de presionarse
        if (mousePressed && !rotating)
        {
            rotating = true;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // ALT acaba de soltarse
        else if (!mousePressed && rotating)
        {
            rotating = false;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Rotar mientras ALT está presionado
        if (rotating)
        {
            RotateHand();
        }
    }

    private void RotateHand()
    {
        
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");
        
        bool shiftPressed =
        Input.GetKey(KeyCode.LeftShift) ||
        Input.GetKey(KeyCode.RightShift);

        if (shiftPressed)
        {
            // ROLL
            // Rotación sobre el eje longitudinal de la mano/brazo
            transform.Rotate(
                Vector3.up,
                -mouseX * rotationSpeed * Time.deltaTime,
                Space.Self
            );
        }
        /*else
        {
            // YAW
            // Giro horizontal
            transform.Rotate(
                Vector3.right,
                mouseX * rotationSpeed * Time.deltaTime,
                Space.Self
            );

            // PITCH
            // Inclinación vertical
            transform.Rotate(
                Vector3.forward,
                -mouseY * rotationSpeed * Time.deltaTime,
                Space.Self
            );
        }*/
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

    private void OnDisable()
    {
        // Asegurarse de liberar el cursor
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
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