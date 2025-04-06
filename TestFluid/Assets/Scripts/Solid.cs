using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Solid : MonoBehaviour
{
    private Rigidbody2D rb;
    float density;

    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody2D>(); // Get the Rigidbody component

        //setPosition(rb.position);
        setVelocity(new Vector2(0,0));
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


    public float getDensity()
    {
        return density;
    }
    public void setDensity(float newDensity)
    {
        density = newDensity;
    }
}
