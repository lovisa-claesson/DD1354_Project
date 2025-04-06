using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;

public class Simulation : MonoBehaviour
{

    Vector2 predictedPosition;
    float density;
    Vector2 pressureForce;
    Vector2 viscosityForce;

    float radius;
    float collisionDamping = 0.2f;
    private Rigidbody2D rb;
    // Size of the boundary
    Vector2 boundsSize;

    // Start is called before the first frame update
    void Start()
    {
        Vector3 particleSize = GetComponent<Renderer>().bounds.size;
        radius = particleSize.x;

        rb = GetComponent<Rigidbody2D>(); // Get the Rigidbody component

        //setPosition(rb.position);
        setVelocity(new Vector2(0,0));
    }


    public void ResolveCollisions()
    {
        ResolveBoundCollisions();

        GameObject[] cubes = GameObject.FindGameObjectsWithTag("CollisionObject");
        foreach(GameObject cube in cubes)
        {
            ResolveObjectCollisions(cube);
        }

    }


    public void ResolveBoundCollisions() {
        Vector2 halfBoundsSize = boundsSize / 2 - Vector2.one * radius;

        Vector2 newPosition = rb.position;
        Vector2 newVelocity = rb.velocity;

        // If bigger than right/left bound, reverse velocity and place particle at bound
        if(math.abs(newPosition.x) > halfBoundsSize.x) {

            newPosition.x = halfBoundsSize.x * math.sign(newPosition.x);
            newVelocity.x *= -1 * collisionDamping;

            //Save new values to the particle
            rb.position = newPosition;
            rb.velocity = newVelocity;
        }
        // If bigger than top/bottom bound, reverse velocity and place particle at bound
        if(math.abs(newPosition.y) > halfBoundsSize.y) {

            newPosition.y = halfBoundsSize.y * math.sign(newPosition.y);
            newVelocity.y *= -1 * collisionDamping;

            //Save new values to the particle
            rb.position = newPosition;
            rb.velocity = newVelocity;
        }

    }

    public void ResolveObjectCollisions(GameObject cube)
    {
        //Get info on the size and position of the cube
        Vector3 cubeSize = cube.GetComponent<Renderer>().bounds.size;
        Vector3 cubeVelocity = cube.GetComponent<Rigidbody>().velocity;

        Vector2 halfCubeSize = cubeSize / 2;
        Vector3 cubePosition = cube.transform.position;

        // Get the particles current position and velocity
        Vector2 newPosition = rb.position;
        Vector2 newVelocity = rb.velocity;

        // Check the distance from particle to outer edges of cube
        float diffToLeft = newPosition.x + radius - (cubePosition.x - halfCubeSize.x);
        float diffToRight = - newPosition.x + radius + (cubePosition.x + halfCubeSize.x);
        float diffToBottom = newPosition.y + radius - (cubePosition.y - halfCubeSize.y);
        float diffToTop = - newPosition.y + radius + (cubePosition.y + halfCubeSize.y);

        // If all distances are positive then the particle is within the cube
        bool withinCube = (diffToLeft >= 0) && (diffToRight >= 0) && (diffToBottom >= 0) && (diffToTop >= 0);

        if(withinCube)
        {
            // If left if the smallest distance, push out the particle to the left
            if (diffToLeft < diffToRight && diffToLeft < diffToTop && diffToLeft < diffToBottom)
            {
                newPosition.x = (cubePosition.x - halfCubeSize.x) - radius;
            } 
            // If right if the smallest distance, push out the particle to the right
            else if (diffToRight < diffToLeft && diffToRight < diffToTop && diffToRight < diffToBottom)
            {
                newPosition.x = (cubePosition.x + halfCubeSize.x) + radius;
            }
            // If bottom if the smallest distance, push out the particle to the bottom
            else if (diffToBottom < diffToTop && diffToBottom < diffToRight && diffToBottom < diffToLeft)
            {
                newPosition.y = (cubePosition.y - halfCubeSize.y) - radius;
            }
            // If top if the smallest distance, push out the particle to the top
            else 
            {
                newPosition.y = (cubePosition.y + halfCubeSize.y) + radius;
            }
            // Change to velocity to the opposite direction
            newVelocity.x *= -1 * collisionDamping;
            newVelocity.y *= -1 * collisionDamping;

            // Update the particle velocity
            rb.position = newPosition;
            rb.velocity = newVelocity;
        }

    }

    public void setBounds(float x, float y)
    {
        boundsSize = new Vector2(x, y);
    }



    public Vector2 getPosition()
    {
        return rb.position;
    }
    public void setPosition(Vector2 newPosition)
    {
        rb.position = newPosition;
    }

    public Vector2 getVelocity()
    {
        return rb.velocity;
    }
    public void setVelocity(Vector2 newVelocity)
    {
        rb.velocity = newVelocity;
    }

    public Vector2 getPredictedPosition()
    {
        return predictedPosition;
    }
    public void setPredictedPosition(Vector2 newPosition)
    {
        predictedPosition = newPosition;
    }

    public float getDensity()
    {
        return density;
    }
    public void setDensity(float newDensity)
    {
        density = newDensity;
    }

    public Vector2 getPressureForce()
    {
        return pressureForce;
    }
    public void setPressureForce(Vector2 newPressureForce)
    {
        pressureForce = newPressureForce;
    }

    public Vector2 getViscosityForce()
    {
        return viscosityForce;
    }
    public void setViscosityForce(Vector2 newViscosityForce)
    {
        viscosityForce = newViscosityForce;
    }
}
