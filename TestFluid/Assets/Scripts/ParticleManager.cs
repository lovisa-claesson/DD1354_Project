using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Mathematics;
using System.Linq;

public class ParticleManager : MonoBehaviour
{
    public GameObject simPrefab; // Reference to object prefab
    public GameObject solidPrefab; // Reference to object prefab
    private Simulation[] objects;
    private Solid[] objects2; 

    public float xBound = 8f;
    public float yBound = 6f;
    public int numParticles;
    public int numSolid = 30;
    public float solidSpacingRadius = 0.20f;
    //float radius = 0.5f;
    public float radiusSolid = 0.15f;
    const float mass = 1;
    public float gravity = 0f;

    public float targetDensity = 2.75f;
    public float smoothingRadius = 0.5f;
    public float pressureMultiplier = 0.5f;
    public float viscosityStrength = 0.2f;

    private List<KeyValuePair<int, uint>> spatialLookup;
    private int[] startIndices;

    // List of the 9 cells in the grid (3x3) that surround a particle
    static int2[] cellOffsets = new int2[9]
    {
        new int2(-1, 1),
        new int2(0, 1),
        new int2(1, 1),
        new int2(-1, 0),
        new int2(0, 0),
        new int2(1, 0),
        new int2(-1, -1),
        new int2(0, -1),
        new int2(1, -1)
    };

    // Start is called before the first frame update
    void Start()
    {
        FluidParticlesInit();
        SolidParticlesInit();

        // Initialize the list with a length
        spatialLookup = Enumerable.Repeat(new KeyValuePair<int, uint>(0, 0), numParticles).ToList();;
        startIndices = new int[numParticles];
    }

    // Update is called once per frame
    void Update()
    {
        // Store all particles placement in the grid
        Vector2[] positions = objects.Select(particle => particle.getPosition()).ToArray();
        UpdateSpatialLookup(positions, smoothingRadius);

        // Density calculation from predicted position
        for(int i = 0 ; i < numParticles; i++) 
        {
            // Predict position to use in density calculation
            Vector2 position = objects[i].getPosition();
            Vector2 velocity = objects[i].getVelocity();
            Vector2 predictedPosition = position + velocity * Time.deltaTime;
            objects[i].setPredictedPosition(predictedPosition);

            // Density around a point
            float density = CalculateDensity(predictedPosition);
            objects[i].setDensity(density);
        }
        // Add viscosity force and update velocity
        for(int i = 0 ; i < numParticles; i++) 
        {
            Vector2 viscosityForce = CalculateViscosityForce(i);
            Vector2 viscosityAcceleration = viscosityForce / objects[i].getDensity();

            Vector2 velocity = objects[i].getVelocity();
            velocity += viscosityAcceleration * Time.deltaTime;
            objects[i].setVelocity(velocity);

        }
        // Add pressure force
        for(int i = 0 ; i < numParticles; i++) 
        {
            Vector2 pressureForce = CalculatePressureForce(i);
            objects[i].setPressureForce(pressureForce);
        }
        // Update velocity and position and handle collisions
        for(int i = 0 ; i < numParticles; i++) 
        {
            Vector2 pressureAcceleration = objects[i].getPressureForce() / objects[i].getDensity();

            Vector2 velocity = objects[i].getVelocity();
            Vector2 position = objects[i].getPosition();

            velocity += pressureAcceleration * Time.deltaTime;
            position += velocity * Time.deltaTime;
            objects[i].setPosition(position);
            objects[i].setVelocity(velocity);

            objects[i].ResolveCollisions();
        }
        
    }

    


    void FluidParticlesInit()
    {
        // Skapa objekten och placera ut dem i ett grid i början av simuleringen
        objects = new Simulation[numParticles];

        int particlesPerRow = (int) math.sqrt(numParticles);
        int particlesPerCol = (numParticles - 1) / particlesPerRow + 1;

        for(int i = 0; i < numParticles; i++) 
        {
            float x = ((i % (float)particlesPerRow) - ((float)particlesPerRow / 2f)) * 0.3f;
            float y = ((i / (float)particlesPerRow) - ((float)particlesPerCol / 2f)) * 0.3f;

            GameObject clone = Instantiate(simPrefab, new Vector2(x, y), Quaternion.identity);
            objects[i] = clone.GetComponent<Simulation>(); // Store reference
            objects[i].setBounds(xBound, yBound);
        }
    }

    void SolidParticlesInit()
    {
        // Skapa partiklarna till väggarna 
        objects2 = new Solid[numSolid];

        for(int i = 0; i < numSolid; i++)
        {
            // Placera ut partiklarna 
            float x;
            float y;
            if (i <= xBound/(solidSpacingRadius*2))
            {
                x = -xBound/2 + solidSpacingRadius * 2 * i;
                y = yBound/2 + solidSpacingRadius;
            }
            else if (i <= 2 * xBound/(solidSpacingRadius*2))
            {
                x = -3*xBound/2 + solidSpacingRadius * 2 * i;
                y = -yBound/2 - solidSpacingRadius;
            }
            else if (i <= 2 * xBound/(solidSpacingRadius*2) + yBound/(solidSpacingRadius*2))
            {
                x = -xBound/2 - solidSpacingRadius;
                y = yBound/2 + solidSpacingRadius * 2 * (i-(2 * xBound/(solidSpacingRadius*2) + yBound/(solidSpacingRadius*2)));
            }
            else 
            {
                x = xBound/2 + solidSpacingRadius;
                y = -yBound/2 + solidSpacingRadius * 2 * (i-(2 * xBound/(solidSpacingRadius*2) + yBound/(solidSpacingRadius*2)));
            }
            GameObject clone2 = Instantiate(solidPrefab, new Vector2(x, y), Quaternion.identity);
            objects2[i] = clone2.GetComponent<Solid>(); // Store reference
            objects2[i].setDensity(targetDensity);  // Density behövs för att kunna räkna ut pressure
        }

    }





    float CalculateDensity(Vector2 particlePosition) 
    {
        float density = 0;

        // Get the particles in the cells surrounding the current particle
        (int centreX, int centreY) = PositionToCellCoord(particlePosition, smoothingRadius);
        float sqrRadius = smoothingRadius * smoothingRadius;

        foreach(int2 offset in cellOffsets)
        {
            uint key = getKeyFromHash(HashCell(centreX + offset.x, centreY + offset.y));
            int cellStartIndex = startIndices[key];

            for(int i = cellStartIndex; i < spatialLookup.Count; i++)
            {
                if(spatialLookup[i].Value != key) break;

                int particleIndex = spatialLookup[i].Key;
                float sqrDist = (objects[particleIndex].getPosition() - particlePosition).magnitude;

                if(sqrDist <= sqrRadius)
                {
                    // Once the neighbour is found, calculate density
                    float dist = math.sqrt(sqrDist);
                    float influence = SmoothingKernel(dist, smoothingRadius);
                    density += mass * influence;
                }
            }
        }

        //Loop through all the solid particles for the density calculation
        foreach (Solid obj in objects2)
        {
            Vector2 position = obj.getPosition();
            float dist = (position - particlePosition).magnitude;
            float influence = SmoothingKernel(dist, radiusSolid);
            density += mass * influence;
        }
        return density;
    }

    Vector2 CalculatePressureForce(int particleIndex)
    {
        Vector2 pressureForce = Vector2.zero;

        Vector2 samplePoint = objects[particleIndex].getPosition();

        // Get the particles in the cells surrounding the current particle
        (int centreX, int centreY) = PositionToCellCoord(samplePoint, smoothingRadius);
        float sqrRadius = smoothingRadius * smoothingRadius;

        foreach(int2 offset in cellOffsets)
        {
            uint key = getKeyFromHash(HashCell(centreX + offset.x, centreY + offset.y));
            int cellStartIndex = startIndices[key];

            for(int i = cellStartIndex; i < spatialLookup.Count; i++)
            {
                if(spatialLookup[i].Value != key) break;

                int otherParticleIndex = spatialLookup[i].Key;
                float sqrDist = (objects[otherParticleIndex].getPosition() - samplePoint).magnitude;

                if(sqrDist <= sqrRadius)
                {
                    // Once the neighbour is found, calculate pressure force
                    //Skip the current particle
                    if(particleIndex == otherParticleIndex) continue;

                    Vector2 diff = objects[otherParticleIndex].getPosition() - samplePoint;
                    float dist = math.sqrt(sqrDist);
                    // Make sure we don't divide by 0
                    Vector2 dir = (dist <= 0) ? getRandomDir() : (diff / dist);

                    float slope = SmoothingKernelDerivative(dist, smoothingRadius);
                    float otherDensity = objects[otherParticleIndex].getDensity();
                    float density = objects[particleIndex].getDensity();
                    float sharedPressure = CalculateSharedPressure(otherDensity, density);

                    // Pushes the particles away from each other with the same force
                    pressureForce += -sharedPressure * dir * slope * mass / otherDensity;
                }
            }
        }
        //Loop through all the solid particles for the pressure calculation
        for(int i = 0; i < numSolid; i++)
        {
            Vector2 offset = objects2[i].getPosition() - objects[particleIndex].getPosition();
            float dist = offset.magnitude;
            // Make sure we don't divide by 0
            Vector2 dir = (dist <= 0) ? getRandomDir() : (offset / dist);

            float slope = SmoothingKernelDerivative(dist, radiusSolid);
            float otherDensity = objects2[i].getDensity();
            float density = objects[particleIndex].getDensity();
            float sharedPressure = CalculateSharedPressure(otherDensity, density);

            // Pushes the particles away from each other with the same force
            pressureForce += -sharedPressure * dir * slope * mass / otherDensity;
        }

        return pressureForce;
    }

    Vector2 CalculateViscosityForce(int particleIndex)
    {
        Vector2 viscostityForce = Vector2.zero;
        Vector2 samplePoint = objects[particleIndex].getPosition();

        // Get the particles in the cells surrounding the current particle
        (int centreX, int centreY) = PositionToCellCoord(samplePoint, smoothingRadius);
        float sqrRadius = smoothingRadius * smoothingRadius;

        foreach(int2 offset in cellOffsets)
        {
            uint key = getKeyFromHash(HashCell(centreX + offset.x, centreY + offset.y));
            int cellStartIndex = startIndices[key];

            for(int i = cellStartIndex; i < spatialLookup.Count; i++)
            {
                if(spatialLookup[i].Value != key) break;

                int otherParticleIndex = spatialLookup[i].Key;
                float sqrDist = (objects[otherParticleIndex].getPosition() - samplePoint).magnitude;

                if(sqrDist <= sqrRadius)
                {
                    // Once the neighbour is found, calculate viscosity force
                    float dst = math.sqrt(sqrDist);
                    float influence = ViscositySmoothingKernel(dst, smoothingRadius);
                    viscostityForce += (objects[otherParticleIndex].getVelocity() - objects[particleIndex].getVelocity()) * influence;
                }
            }
        }

        return viscostityForce * viscosityStrength;
    }

    Vector2 getRandomDir() 
    {
        float x = UnityEngine.Random.Range(-1, 1);
        float y = UnityEngine.Random.Range(-1, 1);
        return new Vector2(x, y);
    }

    // Makes sure newtons second law is in effect by pushing both particles with the same force
    float CalculateSharedPressure(float densityA, float densityB)
    {
        float pressureA = ConvertDensityToPressure(densityA);
        float pressureB = ConvertDensityToPressure(densityB);
        return (pressureA + pressureB) / 2;
    }

    // The kernel for the density
    public float SmoothingKernel(float dist, float radius) 
    {
        if(dist >= radius) return 0;

        float volume = math.PI * math.pow(radius, 4) / 6;
        float value = radius - dist;
        return value * value / volume;
    }

    // The kernel for the pressure
    static float SmoothingKernelDerivative(float dist, float radius) 
    {
        if(dist >= radius) return 0;

        float scale = 12 / (math.pow(radius, 4) * math.PI);
        float value = radius - dist;
        return value * scale;
    }


    // The smoothing kernel for the viscosity
    static float ViscositySmoothingKernel(float dist, float radius) 
    {
        if(dist >= radius) return 0;

        float volume = math.PI * math.pow(radius, 4) / 6;
        float value = radius * radius - dist * dist;
        return value * value / volume;
    }

    // Fitting for gasses, state equation
    float ConvertDensityToPressure(float density)
    {
        float densityError = density - targetDensity;
        float pressure = densityError * pressureMultiplier;
        return pressure;
    }




    // Method to get number of particles
    public int GetParticleCount()
    {
        return numParticles;
    }

    // Method to get a particle position
    public Vector2 GetParticlePosition(int index)
    {
        if (index >= 0 && index < numParticles)
            return objects[index].getPosition();
        return Vector2.zero;
    }

    // Method to apply external force to a specific particle
    public void ApplyExternalForceToParticle(int index, Vector2 force)
    {
        if (index >= 0 && index < numParticles)
        {
            // Convert force to acceleration 
            Vector2 acceleration = force / objects[index].getDensity();
            
            // Apply acceleration to velocity
            Vector2 velocity = objects[index].getVelocity();
            velocity += acceleration * Time.deltaTime;
            objects[index].setVelocity(velocity);
        }
    }




    // Store which particles are within the grid from each other
    public void UpdateSpatialLookup(Vector2[] points, float radius)
    {
        for(int i = 0; i < points.Length; i++)
        {
            (int cellX, int cellY) = PositionToCellCoord(points[i],radius);
            uint cellKey = getKeyFromHash(HashCell(cellX, cellY));
            spatialLookup[i] = new KeyValuePair<int, uint>(i, cellKey);
            startIndices[i] = int.MaxValue;
        }
        spatialLookup.Sort((p1, p2) => p1.Value.CompareTo(p2.Value));
        for(int i = 0; i < points.Length; i++)
        {
            uint key = spatialLookup[i].Value;
            uint keyPrev = i == 0 ? uint.MaxValue : spatialLookup[i-1].Value;
            if(key != keyPrev)
            {
                startIndices[key] = i; 
            }
        }
    }

    // Translate a (x, y) psotion to a cell in the grid
    public (int x, int y) PositionToCellCoord(Vector2 point, float radius)
    {
        int cellX = (int)(point.x / radius);
        int cellY = (int)(point.y / radius);
        return (cellX, cellY);
    }

    // Get unique hash value for each cell in the grid
    public uint HashCell(int cellX, int cellY)
    {
        uint a = (uint)cellX * 15823;
        uint b = (uint)cellY * 9737333;
        return a + b;
    }

    // Translate grid hash value into a smaller value
    public uint getKeyFromHash(uint hash)
    {
        return hash % (uint) spatialLookup.Count;
    }

    // Template for getting all points within the grid area

    // public void ForeachPointWithinRadius(Vector2 samplePoint)
    // {
    //     (int centreX, int centreY) = PositionToCellCoord(samplePoint, smoothingRadius);
    //     float sqrRadius = smoothingRadius * smoothingRadius;

    //     foreach(int2 offset in cellOffsets)
    //     {
    //         uint key = getKeyFromHash(HashCell(centreX + offset.x, centreY + offset.y));
    //         int cellStartIndex = startIndices[key];

    //         for(int i = cellStartIndex; i < spatialLookup.Count; i++)
    //         {
    //             if(spatialLookup[i].Value != key) break;

    //             int particleIndex = spatialLookup[i].Key;
    //             float sqrDist = (objects[particleIndex].getPosition() - samplePoint).magnitude;

    //             if(sqrDist <= sqrRadius)
    //             {
    //                 Debug.Log("point "+particleIndex+" within radius of "+samplePoint);
    //             }
    //         }
    //     }

    // }

}
