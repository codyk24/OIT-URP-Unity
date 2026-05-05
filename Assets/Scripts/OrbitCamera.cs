using UnityEngine;

public class OrbitCamera : MonoBehaviour
{
    [SerializeField]
    private Transform lookAtTransform;

    [SerializeField]
    private float orbitSpeed = 10f;

    private float orbitDistance = 5f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (lookAtTransform != null)
        {
            // Rotate around the lookAtTransform at the specified orbit speed
            transform.RotateAround(lookAtTransform.position, Vector3.up, orbitSpeed * Time.deltaTime);

            // Maintain a constant distance from the lookAtTransform
            Vector3 desiredPosition = (transform.position - lookAtTransform.position).normalized * orbitDistance + lookAtTransform.position;
            transform.position = desiredPosition;

            // Look at the lookAtTransform
            transform.LookAt(lookAtTransform);
        }
    }
}
