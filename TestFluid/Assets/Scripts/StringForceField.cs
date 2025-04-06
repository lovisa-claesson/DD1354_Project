using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StringForceField : MonoBehaviour
{
    [Header("String-Fluid Coupling")]
    public StringSimulation stringSimulation;
    public ParticleManager particleManager;
    public float influenceRadius = 1.0f;
    public float forceMultiplier = 2.0f;
    public float velocityThreshold = 0.1f; // Minimum velocity to create force
    public bool visualizeForces = false;

    void Update()
    {
        if (stringSimulation == null || particleManager == null)
            return;

        ApplyStringForcesToFluid();
    }

    void ApplyStringForcesToFluid()
    {
        List<Vector3> stringPositions = stringSimulation.GetStringPositions();
        List<Vector3> stringVelocities = stringSimulation.GetStringVelocities();

        // Skip if data is invalid
        if (stringPositions == null || stringVelocities == null || 
            stringPositions.Count == 0 || stringPositions.Count != stringVelocities.Count)
            return;

        // For each fluid particle
        for (int i = 0; i < particleManager.GetParticleCount(); i++)
        {
            Vector2 particlePos = particleManager.GetParticlePosition(i);
            
            // Calculate force from each string segment
            Vector2 totalForce = Vector2.zero;
            
            for (int j = 1; j < stringPositions.Count - 1; j++)
            {
                // Convert string position to 2D for fluid comparison
                Vector2 stringPos = new Vector2(stringPositions[j].x, stringPositions[j].y);
                Vector2 stringVel = new Vector2(stringVelocities[j].x, stringVelocities[j].y);
                
                // Skip segments that are barely moving
                if (stringVel.magnitude < velocityThreshold)
                    continue;
                
                float distance = Vector2.Distance(particlePos, stringPos);
                
                // Apply force only if within influence radius
                if (distance < influenceRadius)
                {
                    Vector2 direction = (particlePos - stringPos).normalized;
                    // Force decreases with distance (linear falloff)
                    float influence = particleManager.SmoothingKernel(distance, influenceRadius);
                    
                    // Force direction is in the direction of string velocity
                    Vector2 force = direction * stringVel.magnitude * influence * forceMultiplier;
                    totalForce += force;
                }
            }
            
            // Apply the accumulated force to this particle
            if (totalForce.sqrMagnitude > 0.001f)
            {
                particleManager.ApplyExternalForceToParticle(i, totalForce);
            }
        }
    }

    // Visualize the force field in the editor (optional)
    void OnDrawGizmos()
    {
        if (!visualizeForces || stringSimulation == null)
            return;

        List<Vector3> positions = stringSimulation.GetStringPositions();
        List<Vector3> velocities = stringSimulation.GetStringVelocities();
        
        if (positions == null || velocities == null)
            return;
        
        for (int i = 1; i < positions.Count - 1; i++)
        {
            if (velocities[i].magnitude > velocityThreshold)
            {
                Gizmos.color = new Color(1, 0, 0, 0.5f);
                Gizmos.DrawWireSphere(positions[i], influenceRadius);
                
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(positions[i], positions[i] + velocities[i]);
            }
        }
    }
}