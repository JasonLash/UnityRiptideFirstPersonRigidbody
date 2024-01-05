using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraControl : MonoBehaviour
{
    public PlayerMovement Move;
    public Transform mainCamera;

    //Rotation and look
    private float xRotation;
    private float desiredX;
    private float sensitivity = 50f;
    private float sensMultiplier = 1f;

	private void Start()
	{
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

	private void Update()
    {
        Look();
    }

    private void Look()
    {
        float mouseX = Input.GetAxis("Mouse X") * sensitivity * Time.fixedDeltaTime * sensMultiplier;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivity * Time.fixedDeltaTime * sensMultiplier;

        //Find current look rotation
        Vector3 rot = mainCamera.transform.localRotation.eulerAngles;
        desiredX = rot.y + mouseX;

        //Rotate, and also make sure we dont over- or under-rotate.
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        //Perform the rotations
        mainCamera.transform.localRotation = Quaternion.Euler(xRotation, desiredX, 0);
        Move.orientation.transform.localRotation = Quaternion.Euler(0, desiredX, 0);
    }
}
