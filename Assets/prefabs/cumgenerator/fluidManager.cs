using UnityEngine;
using System.Runtime.InteropServices;

public class FluidManager : MonoBehaviour
{
    // 1. Define the data structure (Must match the HLSL struct exactly)
    [StructLayout(LayoutKind.Sequential)]
    public struct Particle
    {
        public Vector3 position;
        public Vector3 velocity;
    }

    [Header("Core References")]
    public ComputeShader fluidCompute;
    public Material fluidMaterial;
    public Mesh particleMesh; // Assign a default Sphere mesh in the Inspector

    [Header("Simulation Settings")]
    public int particleCount = 20000;
    public Vector3 spawnVolume = new Vector3(2, 2, 2);
    
    // The coordinate where your wall is omitted/ended so fluid can drain
    public Vector2 blacklistedDrainCoord = new Vector2(0, -5);

    // Buffers
    private ComputeBuffer particleBuffer;
    private ComputeBuffer argsBuffer;
    private int kernelID;

    // Arguments for indirect rendering
    private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };

    void Start()
    {
        kernelID = fluidCompute.FindKernel("CSMain");

        // 2. Initialize the particle array on the CPU
        Particle[] particles = new Particle[particleCount];
        for (int i = 0; i < particleCount; i++)
        {
            // Spawn particles in a random block
            particles[i].position = transform.position + new Vector3(
                Random.Range(-spawnVolume.x / 2, spawnVolume.x / 2),
                Random.Range(-spawnVolume.y / 2, spawnVolume.y / 2),
                Random.Range(-spawnVolume.z / 2, spawnVolume.z / 2)
            );
            particles[i].velocity = Vector3.zero;
        }

        // 3. Create the ComputeBuffer (Size is particle count, Stride is 24 bytes)
        // 3 floats (pos) + 3 floats (vel) = 6 floats * 4 bytes per float = 24 bytes
        particleBuffer = new ComputeBuffer(particleCount, 24);
        particleBuffer.SetData(particles);

        // 4. Bind the buffer and variables to the Compute Shader
        fluidCompute.SetBuffer(kernelID, "particleBuffer", particleBuffer);
        fluidCompute.SetInt("particleCount", particleCount);
        
        // Pass your custom Vector2 directly to the GPU so the fluid knows where the wall is omitted
        fluidCompute.SetVector("blacklistedDrainCoord", blacklistedDrainCoord);

        // 5. Bind the buffer to the Material so the shader can render them
        fluidMaterial.SetBuffer("particleBuffer", particleBuffer);

        // 6. Setup the Arguments Buffer for DrawMeshInstancedIndirect
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        args[0] = (uint)particleMesh.GetIndexCount(0);
        args[1] = (uint)particleCount;
        args[2] = (uint)particleMesh.GetIndexStart(0);
        args[3] = (uint)particleMesh.GetBaseVertex(0);
        argsBuffer.SetData(args);
    }

    void Update()
    {
        // 7. Tell the GPU to run the physics math
        // We divide by 256 because that is a standard thread group size in HLSL
        int threadGroups = Mathf.CeilToInt(particleCount / 256f);
        fluidCompute.Dispatch(kernelID, threadGroups, 1, 1);

        // 8. Render the particles without sending data back to the CPU
        Bounds renderBounds = new Bounds(transform.position, new Vector3(100, 100, 100));
        Graphics.DrawMeshInstancedIndirect(particleMesh, 0, fluidMaterial, renderBounds, argsBuffer);
    }

    void OnDestroy()
    {
        // IMPORTANT: Always release ComputeBuffers to prevent memory leaks!
        if (particleBuffer != null) particleBuffer.Release();
        if (argsBuffer != null) argsBuffer.Release();
    }
}