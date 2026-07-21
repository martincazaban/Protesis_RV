using UnityEngine;

public class PalmGrabber : MonoBehaviour
{
    public float grabCurl = 0.8f;
    public HandController hand;
    public Transform handRoot;

    private FixedJoint joint;
    private Rigidbody candidate;
    private Collider[] handColliders;

    private void Awake()
    {
        handColliders = handRoot.GetComponentsInChildren<Collider>();
    }

    private void OnTriggerEnter(Collider other)
    {   
        Grabbable grab = other.GetComponentInParent<Grabbable>();

        if (grab == null)
            return;

        candidate = grab.GetComponent<Rigidbody>();

        grab.OnGrab(handColliders);

        if (other.attachedRigidbody == null)
            return;

        candidate = other.attachedRigidbody;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.attachedRigidbody == candidate)
            candidate = null;
    }

    void Update()
    {
        if (candidate == null)
            return;

        if (joint == null && HandClosed())
        {
            joint = gameObject.AddComponent<FixedJoint>();
            joint.connectedBody = candidate;
            joint.breakForce = Mathf.Infinity;
            joint.breakTorque = Mathf.Infinity;
        }

        if (joint != null && !HandClosed())
        {
            Destroy(joint);
        }
    }

    bool HandClosed()
    {
        float sum = 0f;

        foreach (var finger in hand.fingers)
            sum += finger.curl;

        return (sum / hand.fingers.Length) > grabCurl;
    }
}