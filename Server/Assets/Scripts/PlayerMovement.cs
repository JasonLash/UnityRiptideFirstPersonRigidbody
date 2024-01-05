using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class PlayerMovement : MonoBehaviour
{
    [HideInInspector]
    public Rigidbody rb;

    //Asignables
    public Transform orientation;
    [SerializeField] private Transform checkSphere;

    //Ground check
    [SerializeField] private float checkSphereRadius;
    [SerializeField] private LayerMask groundLayerMask;
    public bool grounded;
    private bool crouching;
    private bool jumping;

    //Player stats
    public float moveSpeed = 4500;
    public float maxSpeed = 10;
    public float counterMovement = 0.175f;
    private float threshold = 0.01f;
    public float maxSlopeAngle = 35f;

    //Jumping
    private bool readyToJump = true;
    private float jumpCooldown = 0.25f;
    public float jumpForce = 400f;
    private Vector3 normalVector = Vector3.up;

    //Crouch & Slide
    private Vector3 crouchScale = new Vector3(1, 0.5f, 1);
    private Vector3 playerScale;
    public float slideForce = 400;
    public float slideCounterMovement = 0.2f;
    private bool startedCrouch = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = false;
        playerScale = transform.localScale;
        startedCrouch = false;
    }


    public void PhysicsStep(bool[] inputs, Quaternion lookDirection)
    {
        grounded = Physics.CheckSphere(checkSphere.position, checkSphereRadius, groundLayerMask);
        jumping = inputs[4];
        crouching = inputs[5];

        Vector2 inputDirection = getDirectionFromInputs(inputs);
        rb.AddForce(Vector3.down * NetworkManager.Singleton.TickRate * 10);

        if (crouching && !startedCrouch)
        {
            StartCrouch(lookDirection);
            startedCrouch = true;
        }else if (!crouching && startedCrouch)
        {
            StopCrouch();
            startedCrouch = false;
        }

        Vector2 mag = FindVelRelativeToLook(lookDirection);
        float xMag = mag.x, yMag = mag.y;

        //Counteract sliding and sloppy movement
        CounterMovement(inputDirection.x, inputDirection.y, mag, jumping, crouching, lookDirection);

        //If holding jump && ready to jump, then jump
        if (readyToJump && jumping) Jump();


        //If sliding down a ramp, add force down so player stays grounded and also builds speed
        if (crouching && grounded && readyToJump)
        {
            rb.AddForce(Vector3.down * NetworkManager.Singleton.TickRate * 3000);
            return;
        }

        //If speed is larger than maxspeed, cancel out the input so you don't go over max speed
        if (inputDirection.x > 0 && xMag > maxSpeed) inputDirection.x = 0;
        if (inputDirection.x < 0 && xMag < -maxSpeed) inputDirection.x = 0;
        if (inputDirection.y > 0 && yMag > maxSpeed) inputDirection.y = 0;
        if (inputDirection.y < 0 && yMag < -maxSpeed) inputDirection.y = 0;

        //Some multipliers
        float multiplier = 1f, multiplierV = 1f;

        // Movement in air
        if (!grounded)
        {
            multiplier = 0.5f;
            multiplierV = 0.5f;
        }

        // Movement while sliding
        if (grounded && crouching) multiplierV = 0f;

        //Apply forces to move player
        rb.AddForce((lookDirection * Vector3.forward) * inputDirection.y * moveSpeed * NetworkManager.Singleton.TickRate * multiplier * multiplierV);
        rb.AddForce((lookDirection * Vector3.right) * inputDirection.x * moveSpeed * NetworkManager.Singleton.TickRate * multiplier);
    }

    private void StartCrouch(Quaternion lookDirection)
    {
        transform.localScale = crouchScale;
        transform.position = new Vector3(transform.position.x, transform.position.y - 0.5f, transform.position.z);
        if (rb.velocity.magnitude > 0.5f)
        {
            if (grounded)
            {
                rb.AddForce((lookDirection * Vector3.forward) * slideForce);
            }
        }
    }

    private void StopCrouch()
    {
        transform.localScale = playerScale;
        transform.position = new Vector3(transform.position.x, transform.position.y + 0.5f, transform.position.z);
    }

    private void Jump()
    {
        if (grounded && readyToJump)
        {
            readyToJump = false;

            //Add jump forces
            rb.AddForce(Vector2.up * jumpForce * 1.5f);
            rb.AddForce(normalVector * jumpForce * 0.5f);

            //If jumping while falling, reset y velocity.
            Vector3 vel = rb.velocity;
            if (rb.velocity.y < 0.5f)
                rb.velocity = new Vector3(vel.x, 0, vel.z);
            else if (rb.velocity.y > 0)
                rb.velocity = new Vector3(vel.x, vel.y / 2, vel.z);

            Invoke(nameof(ResetJump), jumpCooldown);
        }
    }

    private void ResetJump()
    {
        readyToJump = true;
    }

    private void CounterMovement(float x, float y, Vector2 mag, bool Jumping, bool crouching, Quaternion lookDirection)
    {
        if (!grounded || Jumping) return;

        //Slow down sliding
        if (crouching)
        {
            rb.AddForce(moveSpeed * NetworkManager.Singleton.TickRate * -rb.velocity.normalized * slideCounterMovement);
            return;
        }

        //Counter movement
        if (Math.Abs(mag.x) > threshold && Math.Abs(x) < 0.05f || (mag.x < -threshold && x > 0) || (mag.x > threshold && x < 0))
        {
            rb.AddForce(moveSpeed * (lookDirection * Vector3.right) * NetworkManager.Singleton.TickRate * -mag.x * counterMovement);
        }
        if (Math.Abs(mag.y) > threshold && Math.Abs(y) < 0.05f || (mag.y < -threshold && y > 0) || (mag.y > threshold && y < 0))
        {
            rb.AddForce(moveSpeed * (lookDirection * Vector3.forward) * NetworkManager.Singleton.TickRate * -mag.y * counterMovement);
        }

        //Limit diagonal running. This will also cause a full stop if sliding fast and un-crouching, so not optimal.
        if (Mathf.Sqrt((Mathf.Pow(rb.velocity.x, 2) + Mathf.Pow(rb.velocity.z, 2))) > maxSpeed)
        {
            float fallspeed = rb.velocity.y;
            Vector3 n = rb.velocity.normalized * maxSpeed;
            rb.velocity = new Vector3(n.x, fallspeed, n.z);
        }
    }

    public Vector2 FindVelRelativeToLook(Quaternion lookDirection)
    {
        float lookAngle = lookDirection.eulerAngles.y;
        float moveAngle = Mathf.Atan2(rb.velocity.x, rb.velocity.z) * Mathf.Rad2Deg;

        float u = Mathf.DeltaAngle(lookAngle, moveAngle);
        float v = 90 - u;

        float magnitue = rb.velocity.magnitude;
        float yMag = magnitue * Mathf.Cos(u * Mathf.Deg2Rad);
        float xMag = magnitue * Mathf.Cos(v * Mathf.Deg2Rad);

        return new Vector2(xMag, yMag);
    }

    private Vector2 getDirectionFromInputs(bool[] inputs)
    {
        Vector2 inputDirection = Vector2.zero;
        if (inputs[0])
            inputDirection.y += 1;

        if (inputs[1])
            inputDirection.y -= 1;

        if (inputs[2])
            inputDirection.x -= 1;

        if (inputs[3])
            inputDirection.x += 1;

        return inputDirection;
    }
}
