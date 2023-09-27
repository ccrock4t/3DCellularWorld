 using UnityEngine;
using static WorldAutomaton;

public class Player : MonoBehaviour
{
    [SerializeField] float movementSpeed = 0.1f;
    [SerializeField] float mouseLookSensitivity = 1000.0f;
    [SerializeField] float rotateSensitivity = 1.0f;

    Camera cam;
    Vector3 anchorPoint;
    Quaternion anchorRot;


    private void Start()
    {
        cam = GetComponentInChildren<Camera>();
        Cursor.lockState = CursorLockMode.Locked;
    }

    void FixedUpdate()
    {
        Vector3 move = Vector3.zero;
        float zRot = 0;
        if (Input.GetKey(KeyCode.W)) // forward
            move += Vector3.forward;
        if (Input.GetKey(KeyCode.S)) // backward
            move -= Vector3.forward;
        if (Input.GetKey(KeyCode.D)) // left
            move += Vector3.right;
        if (Input.GetKey(KeyCode.A)) // right
            move -= Vector3.right;
        if (Input.GetKey(KeyCode.E)) // rotate clockwise
            zRot -= 1;
        if (Input.GetKey(KeyCode.Q)) // rotate counterclockwise
            zRot += 1;
        if (Input.GetKey(KeyCode.Space)) // up
            move += Vector3.up;
        if (Input.GetKey(KeyCode.LeftShift)) // down
            move -= Vector3.up;
        transform.Translate(move * movementSpeed);

        transform.Rotate(0, Input.GetAxis("Mouse X") * mouseLookSensitivity, zRot * rotateSensitivity);
        transform.Rotate(-Input.GetAxis("Mouse Y") * mouseLookSensitivity, 0, zRot * rotateSensitivity);

    }



}
