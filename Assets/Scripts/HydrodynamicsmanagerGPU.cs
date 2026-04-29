using System.Runtime.InteropServices;
using UnityEngine;

public class HydrodynamicsManagerGPU : MonoBehaviour
{
    // Must match HLSL Particle struct exactly — same fields, same order
    // Total size: 3+3+3 floats (12+12+12) + 2 floats (4+4) = 44 bytes
    [StructLayout(LayoutKind.Sequential)]
    private struct GPUParticle
    {
        public Vector3 position;      // 12 bytes
        public Vector3 velocity;      // 12 bytes
        public Vector3 acceleration;  // 12 bytes
        public float   density;       //  4 bytes
        public float   pressure;      //  4 bytes
    }

    [SerializeField] private int           particleCount  = 12000;
    [SerializeField] private ComputeShader computeShader;
    [SerializeField] private Material      particleMaterial;

    [SerializeField] private Vector3 boundsCenter = Vector3.zero;
    [SerializeField] private Vector3 boundsSize   = new Vector3(10f, 10f, 10f);
    [SerializeField] private float   bounce       = 0.5f;

    [Header("SPH Settings")]
    [SerializeField] private float restDensity     = 2.5f;
    [SerializeField] private float gasConstant     = 50f;
    [SerializeField] private float viscosity       = 0.8f;
    [SerializeField] private float smoothingRadius = 0.3f;
    [SerializeField] private float particleMass    = 0.02f;

    [Header("Stability — proposed improvements over baseline")]
    [SerializeField] private float fixedDt  = 1f / 120f;
    [SerializeField] private int   substeps = 3;

    [Header("Visualization")]
    [SerializeField] private float maxSpeed     = 10f;
    [SerializeField] private float particleSize = 0.2f;

    // GPU buffers
    private ComputeBuffer particleBuffer;
    private ComputeBuffer predictedPosBuffer;
    private ComputeBuffer argsBuffer;

    // Kernel IDs — integers Unity uses to identify each kernel
    private int kernelPredict;
    private int kernelDensity;
    private int kernelForces;
    private int kernelIntegrate;

    // How many thread groups to dispatch
    private int threadGroups;

    // Mesh for rendering
    private Mesh particleMesh;

    private void Start()
    {
        // --- Step A: Get kernel IDs from the compute shader ---
        kernelPredict   = computeShader.FindKernel("PredictPositions");
        kernelDensity   = computeShader.FindKernel("CalculateDensities");
        kernelForces    = computeShader.FindKernel("CalculateForces");
        kernelIntegrate = computeShader.FindKernel("Integrate");

        // --- Step B: Allocate GPU buffers ---
        int stride         = Marshal.SizeOf(typeof(GPUParticle)); // = 44
        particleBuffer     = new ComputeBuffer(particleCount, stride);
        predictedPosBuffer = new ComputeBuffer(particleCount, sizeof(float) * 3);

        // --- Step C: Spawn particles in a grid and upload to GPU ---
        GPUParticle[] initial = new GPUParticle[particleCount];
        int   gridSize = Mathf.CeilToInt(Mathf.Pow(particleCount, 1f / 3f));
        float spacing  = smoothingRadius * 0.65f;
        int   idx      = 0;

        for (int x = 0; x < gridSize && idx < particleCount; x++)
        for (int y = 0; y < gridSize && idx < particleCount; y++)
        for (int z = 0; z < gridSize && idx < particleCount; z++)
        {
            initial[idx].position     = new Vector3(
                x * spacing - gridSize * spacing * 0.5f,
                y * spacing + 2f,
                z * spacing - gridSize * spacing * 0.5f
            );
            initial[idx].velocity     = Vector3.zero;
            initial[idx].acceleration = Vector3.zero;
            initial[idx].density      = restDensity;
            initial[idx].pressure     = 0f;
            idx++;
        }

        particleBuffer.SetData(initial);  // CPU → GPU, happens only once

        // --- Step D: Build the sphere mesh for rendering ---
        particleMesh = IcosphereGenerator.Create(0.5f, 0);

        // --- Step E: Allocate the indirect args buffer for rendering ---
        uint[] args = new uint[5];
        args[0] = particleMesh.GetIndexCount(0);   // indices per instance
        args[1] = (uint)particleCount;             // instance count
        args[2] = particleMesh.GetIndexStart(0);   // start index
        args[3] = particleMesh.GetBaseVertex(0);   // base vertex
        args[4] = 0;

        argsBuffer = new ComputeBuffer(
            1, args.Length * sizeof(uint),
            ComputeBufferType.IndirectArguments
        );
        argsBuffer.SetData(args);

        // --- Step F: Calculate thread groups ---
        // 256 threads per group matches [numthreads(256,1,1)] in the shader
        threadGroups = Mathf.CeilToInt(particleCount / 256f);

        // --- Step G: Bind everything to the shader ---
        BindBuffersAndParams();
    }

    private void BindBuffersAndParams()
    {
        int[] allKernels = { kernelPredict, kernelDensity,
                             kernelForces,  kernelIntegrate };

        // Bind buffers to every kernel that needs them
        foreach (int k in allKernels)
        {
            computeShader.SetBuffer(k, "particles",          particleBuffer);
            computeShader.SetBuffer(k, "predictedPositions", predictedPosBuffer);
        }

        // Bind scalar parameters — these don't change so set them once
        computeShader.SetInt(   "particleCount",   particleCount);
        
    }

    private void Update()
    {
        // Fixed timestep divided across substeps — core stability fix
        float subDt = fixedDt / substeps;
        computeShader.SetFloat("deltaTime", subDt);

        computeShader.SetFloat( "smoothingRadius", smoothingRadius);
        computeShader.SetFloat( "restDensity",     restDensity);
        computeShader.SetFloat( "gasConstant",     gasConstant);
        computeShader.SetFloat( "viscosityCoeff",  viscosity);
        computeShader.SetFloat( "particleMass",    particleMass);
        computeShader.SetFloat( "bounce",          bounce);
        computeShader.SetVector("boundsMin", boundsCenter - boundsSize * 0.5f);
        computeShader.SetVector("boundsMax", boundsCenter + boundsSize * 0.5f);

        // Run multiple substeps per frame for stability
        for (int s = 0; s < substeps; s++)
        {
            computeShader.Dispatch(kernelPredict,   threadGroups, 1, 1);
            computeShader.Dispatch(kernelDensity,   threadGroups, 1, 1);
            computeShader.Dispatch(kernelForces,    threadGroups, 1, 1);
            computeShader.Dispatch(kernelIntegrate, threadGroups, 1, 1);
        }

        // Pass buffer to material — GPU reads positions directly, no CPU readback
        particleMaterial.SetBuffer("_ParticleBuffer", particleBuffer);
        particleMaterial.SetFloat( "_MaxSpeed",       maxSpeed);
        particleMaterial.SetFloat( "_ParticleSize",   particleSize);

        // Draw all particles in one call — no CPU involved in rendering
        Graphics.DrawMeshInstancedIndirect(
            particleMesh,
            submeshIndex:   0,
            material:       particleMaterial,
            bounds:         new Bounds(boundsCenter, boundsSize * 2f),
            bufferWithArgs: argsBuffer
        );
    }

    private void OnDestroy()
    {
        particleBuffer?.Release();
        predictedPosBuffer?.Release();
        argsBuffer?.Release();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(boundsCenter, boundsSize);
    }
}