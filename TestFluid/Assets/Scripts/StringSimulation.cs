using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StringSimulation : MonoBehaviour
{
    [Header("String Physics")]
    public float stringLength = 5.0f;
    public float tension = 100.0f;      // T - String tension - 100
    public float linearDensity = 0.01f; // μ - Mass per unit length - 0.01f
    public float youngsModulus = 2e9f;  // E - Young's modulus for stiffness - 2e9f
    public float stringRadius = 0.005f; // For calculating moment of inertia - 0.005f
    public float damping = 0.01f;       // For loss mechanisms (section 2.3) - 0.01f

    [Header("Simulation Settings")]
    public int numSegments = 32;        // Resolution of the string - 32
    public int iterations = 10;         // Constraint solver iterations - 10
    public Transform leftAnchor;        // First block
    public Transform rightAnchor;       // Second block
    public float gravity = 0f;          // Set to 0 for horizontal string, >0 for sagging

    [Header("Rendering")]
    public float stringWidth = 0.05f;
    public Material stringMaterial;
    public Color stringColor = Color.white;

    [Header("Excitation")]
    public bool enableInteraction = true;
    public float interactionRadius = 0.5f;
    public float interactionStrength = 5.0f;
    
    // Internal simulation variables
    private List<Vector3> positions;
    private List<Vector3> prevPositions;
    private List<Vector3> velocities;
    private LineRenderer lineRenderer;
    private Camera mainCamera;
    private bool isDragging = false;
    private int draggedPointIndex = -1;
    
    void Start()
    {
        // Get main camera reference
        mainCamera = Camera.main;
        if (mainCamera == null)
            Debug.LogWarning("No main camera found. User interaction may not work correctly.");
        
        // Check if anchors are assigned
        if (leftAnchor == null || rightAnchor == null)
        {
            Debug.LogError("String anchors not assigned! Please assign leftAnchor and rightAnchor in the inspector.");
            enabled = false;
            return;
        }
        
        InitializeRenderer();
        InitializeString();
    }

    private void InitializeRenderer()
    {
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        if (stringMaterial != null)
        {
            lineRenderer.material = stringMaterial;
        }
        else
        {
            // Create a simple colored material that doesn't rely on finding shaders
            Material newMaterial = new Material(Shader.Find("Sprites/Default"));
            
            // Fallback options if first shader isn't found
            if (newMaterial.shader == null)
            {
                Debug.Log("Trying fallback shader...");
                newMaterial = new Material(Shader.Find("Unlit/Color"));
            }
            
            if (newMaterial.shader == null)
            {
                Debug.Log("Using built-in default material");
                // Use the Hidden/Internal-Colored shader which is guaranteed to exist
                Shader fallbackShader = Shader.Find("Hidden/InternalErrorShader");
                newMaterial = new Material(fallbackShader);
                newMaterial.color = Color.yellow;
            }
            
            newMaterial.color = stringColor;
            lineRenderer.material = newMaterial;
            stringMaterial = newMaterial; // Save for future reference
        }
        
        // Set other LineRenderer properties
        lineRenderer.startWidth = stringWidth;
        lineRenderer.endWidth = stringWidth;
        lineRenderer.positionCount = numSegments + 2; // Include anchor points
        lineRenderer.startColor = stringColor;
        lineRenderer.endColor = stringColor;
    }

    private void InitializeString()
    {
        positions = new List<Vector3>();
        prevPositions = new List<Vector3>();
        velocities = new List<Vector3>();
        
        // Create string segment positions
        Vector3 startPos = leftAnchor.position;
        Vector3 endPos = rightAnchor.position;
        float segmentLength = Vector3.Distance(startPos, endPos) / (numSegments + 1);
        
        // Add anchor points and segments
        positions.Add(startPos);
        prevPositions.Add(startPos);
        velocities.Add(Vector3.zero);
        
        for (int i = 1; i <= numSegments; i++)
        {
            float t = i / (float)(numSegments + 1);
            Vector3 pos = Vector3.Lerp(startPos, endPos, t);
            // Add initial curve
            pos.y -= 0.1f * Mathf.Sin(i * Mathf.PI / numSegments);
            
            positions.Add(pos);
            prevPositions.Add(pos);
            velocities.Add(Vector3.zero);
        }
        
        positions.Add(endPos);
        prevPositions.Add(endPos);
        velocities.Add(Vector3.zero);
    }

    void Update()
    {
        // Add null checks for common sources of NullReferenceException
        if (leftAnchor == null || rightAnchor == null)
        {
            Debug.LogError("String anchors are not assigned. Please assign both leftAnchor and rightAnchor transforms.");
            enabled = false; // Disable this component
            return;
        }
        
        if (mainCamera == null)
        {
            Debug.LogError("Main camera is null. Trying to find camera...");
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                enabled = false;
                return;
            }
        }
        
        if (positions == null || prevPositions == null || velocities == null)
        {
            Debug.LogError("String simulation data not initialized properly.");
            enabled = false;
            return;
        }
        
        HandleUserInput();
        
        float dt = Time.deltaTime;
        
        // Apply physics based on wave equation with stiffness (section 2.1 & 2.2)
        for (int i = 1; i < positions.Count - 1; i++)
        {
            // Based on the wave equation: μ ∂²u/∂t² - T ∂²u/∂x² + EI ∂⁴u/∂x⁴ = 0
            Vector3 leftPos = positions[i - 1];
            Vector3 rightPos = positions[i + 1];
            Vector3 acceleration = Vector3.zero;
            
            // Calculate second spatial derivative (for tension forces)
            Vector3 d2x = leftPos - 2 * positions[i] + rightPos;
            
            // Apply tension forces (wave equation term)
            float segmentLength = stringLength / (numSegments + 1);
            acceleration += (tension / linearDensity) * d2x / (segmentLength * segmentLength);
            
            // Apply bending resistance (stiffness term - section 2.2)
            if (i > 1 && i < positions.Count - 2)
            {
                Vector3 leftLeftPos = positions[i - 2];
                Vector3 rightRightPos = positions[i + 2];
                Vector3 d4x = leftLeftPos - 4 * leftPos + 6 * positions[i] - 4 * rightPos + rightRightPos;
                
                // Calculate moment of inertia I = πr⁴/4 for circular cross-section
                float I = Mathf.PI * Mathf.Pow(stringRadius, 4) / 4;
                
                // Apply the fourth-order term (stiffness)
                acceleration -= (youngsModulus * I / linearDensity) * d4x / Mathf.Pow(segmentLength, 4);
            }
            
            // Apply gravity if enabled
            acceleration += Vector3.down * gravity;
            
            // Apply damping (loss mechanisms - section 2.3)
            acceleration -= damping * velocities[i];
            
            // Update velocity and position using Verlet integration
            velocities[i] += acceleration * dt;
        }

        // Store current positions for next iteration
        List<Vector3> currentPositions = new List<Vector3>(positions);

        // Update positions based on velocities
        for (int i = 1; i < positions.Count - 1; i++)
        {
            if (i != draggedPointIndex || !isDragging)
            {
                positions[i] += velocities[i] * dt;
            }
        }
        
        // Apply constraints to keep string length consistent
        ApplyConstraints();
        
        // Update velocities based on position change
        for (int i = 1; i < positions.Count - 1; i++)
        {
            if (i != draggedPointIndex || !isDragging)
            {
                velocities[i] = (positions[i] - currentPositions[i]) / dt;
            }
        }
        
        // Update the line renderer to show the string
        UpdateLineRenderer();
    }
    
    private void ApplyConstraints()
    {
        // Fix the ends to the anchor points
        positions[0] = leftAnchor.position;
        positions[positions.Count - 1] = rightAnchor.position;
        
        // Ensure distance constraints between points
        float idealDistance = Vector3.Distance(leftAnchor.position, rightAnchor.position) / (numSegments + 1);
        
        for (int iter = 0; iter < iterations; iter++)
        {
            // Skip the dragged point if currently dragging
            int startIdx = (isDragging && draggedPointIndex == 1) ? 2 : 1;
            int endIdx = (isDragging && draggedPointIndex == positions.Count - 2) ? positions.Count - 3 : positions.Count - 2;
            
            for (int i = startIdx; i <= endIdx; i++)
            {
                if (isDragging && i == draggedPointIndex) continue;
                
                // Apply constraints with previous point
                if (i > 0)
                {
                    Vector3 dir = positions[i] - positions[i - 1];
                    float dist = dir.magnitude;
                    if (dist > 0)
                    {
                        Vector3 correction = dir * (1 - idealDistance / dist) * 0.5f;
                        if (i > 1 || !isDragging || draggedPointIndex != i - 1)
                            positions[i] -= correction;
                        if (i - 1 > 0 && (i - 1 != draggedPointIndex || !isDragging))
                            positions[i - 1] += correction;
                    }
                }
                
                // Apply constraints with next point
                if (i < positions.Count - 1)
                {
                    Vector3 dir = positions[i] - positions[i + 1];
                    float dist = dir.magnitude;
                    if (dist > 0)
                    {
                        Vector3 correction = dir * (1 - idealDistance / dist) * 0.5f;
                        if (i != draggedPointIndex || !isDragging)
                            positions[i] -= correction;
                        if (i + 1 < positions.Count - 1 && (i + 1 != draggedPointIndex || !isDragging))
                            positions[i + 1] += correction;
                    }
                }
            }
        }
    }
    
    private void HandleUserInput()
    {
        if (!enableInteraction) return;
        
        // Detect mouse inputs for string interaction
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            int closestPointIndex = -1;
            float minDist = interactionRadius;
            
            // Find the closest string point
            for (int i = 1; i < positions.Count - 1; i++)
            {
                float dist = Vector3.Cross(ray.direction, positions[i] - ray.origin).magnitude;
                if (dist < minDist)
                {
                    minDist = dist;
                    closestPointIndex = i;
                }
            }
            
            if (closestPointIndex != -1)
            {
                isDragging = true;
                draggedPointIndex = closestPointIndex;
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
            draggedPointIndex = -1;
        }
        
        // Apply force if dragging
        if (isDragging && draggedPointIndex >= 0)
        {
            // Convert mouse position to world position on z-plane of the string
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            float t = -ray.origin.z / ray.direction.z; // Assuming string is on z=0 plane
            Vector3 targetPos = ray.origin + ray.direction * t;
            
            // Move the dragged point
            positions[draggedPointIndex] = Vector3.Lerp(positions[draggedPointIndex], targetPos, 0.5f);
            velocities[draggedPointIndex] = Vector3.zero; // Zero velocity while dragging
        }
    }
    
    private void UpdateLineRenderer()
    {
        for (int i = 0; i < positions.Count; i++)
        {
            lineRenderer.SetPosition(i, positions[i]);
        }
    }

    // Apply an external force to the string at a specific point (for plucking/hammering - section 3.1)
    public void ApplyForce(int pointIndex, Vector3 force, float duration)
    {
        if (pointIndex > 0 && pointIndex < positions.Count - 1)
        {
            StartCoroutine(ApplyForceCoroutine(pointIndex, force, duration));
        }
    }

    private IEnumerator ApplyForceCoroutine(int pointIndex, Vector3 force, float duration)
    {
        float startTime = Time.time;
        while (Time.time - startTime < duration)
        {
            velocities[pointIndex] += force * Time.deltaTime;
            yield return null;
        }
    }

    // Method to programmatically pluck the string (section 3.1)
    public void PluckString(float pluckPosition, float amplitude)
    {
        int index = Mathf.RoundToInt(pluckPosition * numSegments) + 1;
        if (index > 0 && index < positions.Count - 1)
        {
            positions[index] += Vector3.up * amplitude;
        }
    }

    // Methods to expose string data to other components
    public List<Vector3> GetStringPositions()
    {
        return positions;
    }

    public List<Vector3> GetStringVelocities()
    {
        return velocities;
    }
    public bool GetIsDragging()
    {
        return isDragging;
    }
}