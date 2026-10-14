using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class Finger
{
    public string name;
    public Transform[] joints;

    [HideInInspector]
    public Quaternion[] initialRotations;

    [Range(0, 1)]
    public float curl;

    public bool blocked;

    [Tooltip("Dejar en (0,0,0) para usar el eje global de rotationAxis. " +
             "El pulgar normalmente necesita un eje propio.")]
    public Vector3 axisOverride = Vector3.zero;
}

public class HandController : MonoBehaviour
{
    public enum ControlMode { Hand, Wrist, Elbow }

    // NUEVO: de donde vienen los comandos.
    //   Mouse    -> comportamiento original (scroll + teclas 1/2/3)
    //   Protesis -> los angulos los empuja ProtesisArmDriver
    public enum InputSource { Mouse, Protesis }

    [Header("Fuente de Control")]
    public InputSource inputSource = InputSource.Mouse;

    [Header("Estado Actual (Solo Lectura)")]
    public ControlMode currentMode = ControlMode.Hand;

    [Header("Articulación: Muñeca")]
    public Transform wrist;
    public Vector3 wristAxis = Vector3.up;
    [Tooltip("Límite de giro de la muñeca (Mínimo, Máximo)")]
    public Vector2 wristAngleLimits = new Vector2(-45f, 45f);
    private float currentWristAngle = 0f;
    private Quaternion initialWristRotation;

    [Header("Articulación: Codo")]
    public Transform elbow;
    public Vector3 elbowAxis = Vector3.right;
    [Tooltip("Límite de flexión del codo (Mínimo, Máximo)")]
    public Vector2 elbowAngleLimits = new Vector2(0f, 90f);
    private float currentElbowAngle = 0f;
    private Quaternion initialElbowRotation;

    [Header("Configuración General de Rotación")]
    public float jointRotationSpeed = 150f;

    [Header("Configuración de Dedos")]
    public Finger[] fingers;
    public Vector3 rotationAxis = Vector3.right;
    public float maxBendAngle = 90f;
    public float scrollSensitivity = 2f;

    private float handCloseValue = 0f;
    private bool initialized = false;

    // --- Lectura del estado, para UI o debug ---
    public float WristAngle => currentWristAngle;
    public float ElbowAngle => currentElbowAngle;
    public float HandCurl => handCloseValue;

    // IMPORTANTE: esto estaba en Start(). Se movio a Awake() porque el driver
    // externo puede empezar a mandar angulos antes de que corra Start(), y
    // entonces las rotaciones iniciales todavia no estarian guardadas: el
    // brazo arranca deformado y no es nada obvio por que.
    void Awake()
    {
        currentMode = ControlMode.Hand;

        if (wrist != null) initialWristRotation = wrist.localRotation;
        if (elbow != null) initialElbowRotation = elbow.localRotation;

        foreach (Finger finger in fingers)
        {
            finger.initialRotations = new Quaternion[finger.joints.Length];
            for (int i = 0; i < finger.joints.Length; i++)
            {
                if (finger.joints[i] != null)
                {
                    finger.initialRotations[i] = finger.joints[i].localRotation;
                }
            }
        }

        initialized = true;
    }

    void Update()
    {
        // En modo Protesis no se lee el mouse: los valores los empuja el driver.
        if (inputSource != InputSource.Mouse) return;

        HandleModeSwitch();

        switch (currentMode)
        {
            case ControlMode.Hand:
                HandleHandGrip();
                break;

            case ControlMode.Wrist:
                HandleWristScroll();
                break;

            case ControlMode.Elbow:
                HandleElbowScroll();
                break;
        }
    }

    // =====================================================================
    //                    API PUBLICA (control externo)
    // =====================================================================

    /// <summary>Angulo del codo en grados. Se recorta a elbowAngleLimits.</summary>
    public void SetElbowAngle(float degrees)
    {
        if (!initialized || elbow == null) return;
        currentElbowAngle = Mathf.Clamp(degrees, elbowAngleLimits.x, elbowAngleLimits.y);
        elbow.localRotation = initialElbowRotation * Quaternion.AngleAxis(currentElbowAngle, elbowAxis);
    }

    /// <summary>Angulo de la muñeca en grados. Se recorta a wristAngleLimits.</summary>
    public void SetWristAngle(float degrees)
    {
        if (!initialized || wrist == null) return;
        currentWristAngle = Mathf.Clamp(degrees, wristAngleLimits.x, wristAngleLimits.y);
        wrist.localRotation = initialWristRotation * Quaternion.AngleAxis(currentWristAngle, wristAxis);
    }

    /// <summary>Cierre de la mano expresado como angulo de flexion, 0..maxBendAngle.</summary>
    public void SetHandAngle(float degrees)
    {
        if (maxBendAngle <= 0f) return;
        SetHandCurl(degrees / maxBendAngle);
    }

    /// <summary>Cierre de la mano normalizado, 0 = abierta, 1 = cerrada.</summary>
    public void SetHandCurl(float curl01)
    {
        if (!initialized) return;
        handCloseValue = Mathf.Clamp01(curl01);
        foreach (Finger finger in fingers)
        {
            if (!finger.blocked) finger.curl = handCloseValue;
        }
        UpdateFingersRotation();
    }

    /// <summary>Deja todo en la pose de reposo guardada al inicio.</summary>
    public void ResetPose()
    {
        SetElbowAngle(elbowAngleLimits.x);
        SetWristAngle(0f);
        SetHandCurl(0f);
    }

    // =====================================================================
    //                    Control por mouse (original)
    // =====================================================================

    private void HandleModeSwitch()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            currentMode = ControlMode.Hand;
            Debug.Log("Modo: MANO (Agarre)");
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            currentMode = ControlMode.Wrist;
            Debug.Log("Modo: MUÑECA (Rotación)");
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            currentMode = ControlMode.Elbow;
            Debug.Log("Modo: CODO (Rotación)");
        }
    }

    private void HandleHandGrip()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            SetHandCurl(handCloseValue + scroll * scrollSensitivity);
        }
    }

    private void HandleWristScroll()
    {
        if (wrist == null) return;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            SetWristAngle(currentWristAngle + scroll * jointRotationSpeed);
        }
    }

    private void HandleElbowScroll()
    {
        if (elbow == null) return;

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            SetElbowAngle(currentElbowAngle + scroll * jointRotationSpeed);
        }
    }

    // =====================================================================

    public void UpdateFingersRotation()
    {
        foreach (Finger finger in fingers)
        {
            float currentAngle = finger.curl * maxBendAngle;

            Vector3 axis = (finger.axisOverride == Vector3.zero)
                         ? rotationAxis
                         : finger.axisOverride;

            Quaternion flexRotation = Quaternion.AngleAxis(currentAngle, axis);

            for (int i = 0; i < finger.joints.Length; i++)
            {
                if (finger.joints[i] != null && finger.initialRotations != null
                    && i < finger.initialRotations.Length)
                {
                    finger.joints[i].localRotation = finger.initialRotations[i] * flexRotation;
                }
            }
        }
    }

    [ContextMenu("Autocompletar Dedos")]
    private void AutoAssignFingers()
    {
        string[] fingerNames = { "Anular", "Indice", "Medio", "Meñique", "Pulgar" };
        fingers = new Finger[fingerNames.Length];
        Transform[] allTransforms = GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < fingerNames.Length; i++)
        {
            string fName = fingerNames[i];
            fingers[i] = new Finger { name = fName };

            string lowName = "Falange Low " + fName;
            string midName = "Falange Middle " + fName;
            string upName = "Falange Up " + fName;

            Transform low = FindTransformByName(allTransforms, lowName);
            Transform mid = FindTransformByName(allTransforms, midName);
            Transform up = FindTransformByName(allTransforms, upName);

            List<Transform> foundJoints = new List<Transform>();

            if (low != null) foundJoints.Add(low);
            if (mid != null) foundJoints.Add(mid);
            if (up != null) foundJoints.Add(up);

            fingers[i].joints = foundJoints.ToArray();
        }
    }

    private Transform FindTransformByName(Transform[] transforms, string exactName)
    {
        foreach (Transform t in transforms)
        {
            if (t.name == exactName) return t;
        }
        return null;
    }
}
