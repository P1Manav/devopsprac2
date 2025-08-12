using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;          
    public Vector3 offset = new Vector3(0f, 5f, -10f); 
    public float followSpeed = 10f;   
    public float rotationSpeed = 5f; 

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + target.rotation * offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);

        Quaternion desiredRotation = Quaternion.LookRotation(target.position - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSpeed * Time.deltaTime);
    }
}
