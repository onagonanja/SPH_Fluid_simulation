using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Claims;
using System.Runtime.InteropServices;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class SPH_Compute_solver : MonoBehaviour {
    public GameObject particlePrefab;   // Prefab for the particles
    [HideInInspector] public Particle[] particles;        // Array of particles in the fluid
                                                          // Number of particles in the fluid
    public float GRAVITY = -0.81f;
    public float BOX_SIZE = 3.0f;

    private int numParticles;
    private ComputeShader sphComputeShader; // Compute shader for SPH calculations
    private ComputeBuffer R_particleBuffer; // Buffer for particle data in the compute shader
    private ComputeBuffer W_particleBuffer; // Buffer for particle data in the compute shader

    [SerializeField] NumParticlesEnum _numParticles = NumParticlesEnum.numParticles_1x; // Number of particles in the fluid
    [SerializeField] float targetDensity = 5.0f;            // Target density for the fluid
    [SerializeField] float pressureCoefficient = 0.57f;     // Coefficient for the pressure term
    [SerializeField] float viscosityCoefficient = 0.1f;     // Coefficient for the viscosity term
    [SerializeField] float WallStiffness = 100.0f;          // Stiffness for the wall penalty
    [SerializeField] public float particleRadius = 0.12f;   // Radius of the particles
    [SerializeField] int iterations = 4;                    // Number of iterations for the solver
    [SerializeField] float startSize = 0.8f;                // Number of iterations for the solver
    [SerializeField] float particleMass = 0.04f;            // Mass of the particles

    const int BASE_THREAD_SIZE = 1024;

    enum NumParticlesEnum {
        numParticles_1x = BASE_THREAD_SIZE,
        numParticles_2x = BASE_THREAD_SIZE * 2,
        numParticles_4x = BASE_THREAD_SIZE * 4,
        numParticles_8x = BASE_THREAD_SIZE * 8,
    }

    struct ParticleBuffer {
        public Vector3 position;
        public Vector3 velocity;
        public Vector3 acceleration;
        public float density;
        public float pressure;
    }

    struct DebugBuffer {
        public Vector3 viscosity;
        public Vector3 pressureGradient;
    }
    private ComputeBuffer debugBufferList;

    void Start() {
        numParticles = (int)_numParticles;
        particles = new Particle[numParticles];
        R_particleBuffer = new ComputeBuffer(numParticles, Marshal.SizeOf(typeof(ParticleBuffer)));
        W_particleBuffer = new ComputeBuffer(numParticles, Marshal.SizeOf(typeof(ParticleBuffer)));

        ParticleBuffer[] particleBufferList = new ParticleBuffer[numParticles];
        debugBufferList = new ComputeBuffer(numParticles, Marshal.SizeOf(typeof(DebugBuffer)));

        for (int i = 0; i < numParticles; i++) {
            GameObject particleObject = Instantiate(particlePrefab, UnityEngine.Random.insideUnitSphere * 5, Quaternion.identity);
            particles[i] = particleObject.GetComponent<Particle>();
            particles[i].transform.position = new Vector3(
                UnityEngine.Random.Range(-BOX_SIZE * startSize, BOX_SIZE * startSize),
                UnityEngine.Random.Range(-BOX_SIZE * startSize, BOX_SIZE * startSize),
                UnityEngine.Random.Range(-BOX_SIZE * startSize, BOX_SIZE * startSize)
            );
            ParticleBuffer particleBuffer = new ParticleBuffer {
                position = particles[i].transform.position,
                velocity = particles[i].v,
                acceleration = particles[i].a,
                density = particles[i].density,
                pressure = particles[i].pressure
            };
            particleBufferList[i] = particleBuffer;
        }

        R_particleBuffer.SetData(particleBufferList);

        sphComputeShader = Resources.Load<ComputeShader>("SPH");
        sphComputeShader.SetInt("NUM_PARTICLES", numParticles);
        sphComputeShader.SetVector("GRAVITY", new Vector3(0, GRAVITY, 0));
        sphComputeShader.SetFloat("PARTICLE_MASS", 0.04f);
        sphComputeShader.SetFloat("SMOOTH_LEN", particleRadius);
        sphComputeShader.SetFloat("REST_DENSITY", targetDensity);
        sphComputeShader.SetFloat("PRESS_COEFF", pressureCoefficient);
        sphComputeShader.SetFloat("VISCO_COEFF", viscosityCoefficient);
        sphComputeShader.SetFloat("WALL_STIFF", WallStiffness);
        sphComputeShader.SetFloat("BOX_SIZE", BOX_SIZE);
    }

    void Update() {
        sphComputeShader.SetFloat("TIME_STEP", Time.deltaTime / 100.0f);

        for (int i = 0; i < iterations; i++) {
            SolveSPH();
        }
    }

    void SolveSPH() {
        int kernelId;
        int num_threadgroup = numParticles / BASE_THREAD_SIZE;

        // Calculate density
        kernelId = sphComputeShader.FindKernel("ComputeDensity");
        sphComputeShader.SetBuffer(kernelId, "R_particlesBuffer", R_particleBuffer);
        sphComputeShader.SetBuffer(kernelId, "W_particlesBuffer", W_particleBuffer);
        sphComputeShader.Dispatch(kernelId, num_threadgroup, 1, 1);

        // Calculate pressure
        kernelId = sphComputeShader.FindKernel("ComputePressure");
        sphComputeShader.SetBuffer(kernelId, "R_particlesBuffer", R_particleBuffer);
        sphComputeShader.SetBuffer(kernelId, "W_particlesBuffer", W_particleBuffer);
        sphComputeShader.Dispatch(kernelId, num_threadgroup, 1, 1);

        // Calculate acceleration
        kernelId = sphComputeShader.FindKernel("ComputeAcceleration");
        sphComputeShader.SetBuffer(kernelId, "R_particlesBuffer", R_particleBuffer);
        sphComputeShader.SetBuffer(kernelId, "W_particlesBuffer", W_particleBuffer);
        sphComputeShader.SetBuffer(kernelId, "debugBuffer", debugBufferList);
        sphComputeShader.Dispatch(kernelId, num_threadgroup, 1, 1);

        // Calculate time integration
        kernelId = sphComputeShader.FindKernel("ComputeTimeIntegration");
        sphComputeShader.SetBuffer(kernelId, "R_particlesBuffer", R_particleBuffer);
        sphComputeShader.SetBuffer(kernelId, "W_particlesBuffer", W_particleBuffer);
        sphComputeShader.Dispatch(kernelId, num_threadgroup, 1, 1);

        // Copy data back to particles
        ParticleBuffer[] particleBufferList = new ParticleBuffer[numParticles];
        W_particleBuffer.GetData(particleBufferList);

        DebugBuffer[] debugBufferListArray = new DebugBuffer[numParticles];
        debugBufferList.GetData(debugBufferListArray);

        for (int i = 0; i < numParticles; i++) {
            particles[i].transform.position = particleBufferList[i].position;
        }

        for (int i = 0; i < 10; i++) {
            // Debug.Log("Particle " + i + " Density: " + particleBufferList[i].density);
            // Debug.Log("Particle " + i + " Pressure: " + particleBufferList[i].pressure);
            // Debug.Log("Particle " + i + " Pressure Gradient: " + debugBufferListArray[i].pressureGradient);
            // Debug.Log("Particle " + i + " Viscosity: " + debugBufferListArray[i].viscosity);
            // Debug.Log("Particle " + i + " Velocity: " + particleBufferList[i].velocity);
            // Debug.Log("Particle " + i + " Acceleration: " + particleBufferList[i].acceleration);
            // Debug.Log("Particle " + i + " Position: " + particleBufferList[i].position);
        }

        // Swap buffers
        ComputeBuffer temp = R_particleBuffer;
        R_particleBuffer = W_particleBuffer;
        W_particleBuffer = temp;
    }
}
