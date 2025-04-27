using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Claims;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;

public class SPH_solver : MonoBehaviour {
    public GameObject particlePrefab;   // Prefab for the particles
    public Particle[] particles;        // Array of particles in the fluid

    public float GRAVITY = -0.81f;
    public float BOX_SIZE = 3.0f;

    private HashGridSearcher gridSearcher;  // Hash grid searcher for neighbor search
    private ComputeShader sphComputeShader; // Compute shader for SPH calculations
    private ComputeBuffer particleBuffer;   // Buffer for particle data in the compute shader

    [SerializeField] int numParticles = 1000;               // Number of particles in the fluid
    [SerializeField] float targetDensity = 5.0f;            // Target density for the fluid
    [SerializeField] float pressureCoefficient = 0.57f;     // Coefficient for the pressure term
    [SerializeField] float viscosityCoefficient = 0.1f;     // Coefficient for the viscosity term
    [SerializeField] float WallStiffness = 100.0f;          // Stiffness for the wall penalty
    [SerializeField] public float particleRadius = 0.12f;   // Radius of the particles
    [SerializeField] int iterations = 4;                    // Number of iterations for the solver
    [SerializeField] float startSize = 0.8f;                // Number of iterations for the solver


    void Start() {
        particles = new Particle[numParticles];


        for (int i = 0; i < numParticles; i++) {
            GameObject particleObject = Instantiate(particlePrefab, UnityEngine.Random.insideUnitSphere * 5, Quaternion.identity);
            particles[i] = particleObject.GetComponent<Particle>();
            particles[i].transform.position = new Vector3(
                UnityEngine.Random.Range(-BOX_SIZE * startSize, BOX_SIZE * startSize),
                UnityEngine.Random.Range(-BOX_SIZE * startSize, BOX_SIZE * startSize),
                UnityEngine.Random.Range(-BOX_SIZE * startSize, BOX_SIZE * startSize)
            );
        }

        gridSearcher = new HashGridSearcher();
    }

    void Update() {
        for (int i = 0; i < iterations; i++) {
            SolveSPH();
        }
    }

    float Kernel(float r) {
        float h = particleRadius;
        float q = r / h;

        if (q < 1.0f) {
            return 315.0f / (64.0f * Mathf.PI * Mathf.Pow(h, 9)) * Mathf.Pow(h * h - r * r, 3);
        } else {
            return 0.0f;
        }
    }

    Vector3 KernelGradient(float r, Vector3 direction) {
        float h = particleRadius;
        float q = r / h;

        if (q < 1.0f) {
            return -945.0f / (32.0f * Mathf.PI * Mathf.Pow(h, 9)) * Mathf.Pow(h * h - r * r, 2) * direction;
        } else {
            return Vector3.zero;
        }
    }

    float KernelLaplacian(float r) {
        float h = particleRadius;
        float q = r / h;
        float q2 = q * q;

        if (q < 1.0f) {
            return 945.0f / (32.0f * Mathf.PI * Mathf.Pow(h, 5)) * (1 - q2) * (3 * q2 - 1);
            // return -945.0f / (32.0f * Mathf.PI * Mathf.Pow(h, 9)) * (h * h - r * r) * (3 * h * h - 7 * r * r);
        } else {
            return 0.0f;
        }
    }

    void CalcDensity() {
        for (int i = 0; i < numParticles; i++) {
            particles[i].density = 0.0f;
        }

        Action<int, int> action = (int i, int j) => {
            float distance = Vector3.Distance(particles[i].transform.position, particles[j].transform.position);
            particles[i].density += particles[j].mass * Kernel(distance);
        };

        gridSearcher.forEachNeighbor(particles, action);
    }

    void CalcPressure() {
        for (int i = 0; i < numParticles; i++) {
            particles[i].pressure = PressureEOS(particles[i].density);
        }
    }

    float PressureEOS(float density) {
        float p = pressureCoefficient * Mathf.Max(Mathf.Pow(density / targetDensity, 7) - 1.0f, 0.0f);
        return p;
    }

    void CalcPressureGradient() {
        for (int i = 0; i < numParticles; i++) {
            particles[i].pressureGradient = Vector3.zero;
        }

        Action<int, int> action = (int i, int j) => {
            float distance = Vector3.Distance(particles[i].transform.position, particles[j].transform.position);
            Vector3 direction = (particles[j].transform.position - particles[i].transform.position).normalized;
            particles[i].pressureGradient += particles[i].mass * KernelGradient(distance, direction)
            * (particles[j].pressure / (particles[j].density * particles[j].density) + particles[i].pressure / (particles[i].density * particles[i].density));
        };

        gridSearcher.forEachNeighbor(particles, action);
    }

    void CalcViscocity() {
        for (int i = 0; i < numParticles; i++) {
            particles[i].viscosity = Vector3.zero;
        }

        Action<int, int> action = (int i, int j) => {
            float distance = Vector3.Distance(particles[i].transform.position, particles[j].transform.position);
            particles[i].viscosity += viscosityCoefficient * particles[i].mass * KernelLaplacian(distance) * (particles[j].v - particles[i].v) / particles[j].density;
        };

        gridSearcher.forEachNeighbor(particles, action);
    }

    void CalcAcceleration() {
        float ave_dencity = 0.0f;
        for (int i = 0; i < numParticles; i++) {
            particles[i].a = Vector3.zero;
            particles[i].a += new Vector3(0, GRAVITY, 0);                           // Gravity
            particles[i].a += particles[i].pressureGradient / particles[i].density; // Pressure gradient
            particles[i].a += particles[i].viscosity;                               // Viscosity

            // Debug.Log("Pressure: " + particles[i].pressure);
            // Debug.Log("Pressure Gradient: " + particles[i].pressureGradient);
            // Debug.Log("Density: " + particles[i].density);
            // Debug.Log("Velocity: " + particles[i].v);
            // Debug.Log("Acceleration: " + particles[i].a);

            ave_dencity += particles[i].density;
        }
        ave_dencity /= numParticles;
        Debug.Log("Average Density: " + ave_dencity);
    }

    void CalcWallPenalty() {
        for (int i = 0; i < numParticles; i++) {
            if (particles[i].transform.position.y < -BOX_SIZE) {
                float dist = particles[i].transform.position.y - (-BOX_SIZE);
                particles[i].a += new Vector3(0, -dist * WallStiffness, 0);
            }

            if (particles[i].transform.position.y > BOX_SIZE) {
                float dist = particles[i].transform.position.y - BOX_SIZE;
                particles[i].a += new Vector3(0, -dist * WallStiffness, 0);
            }

            if (particles[i].transform.position.x > BOX_SIZE) {
                float dist = particles[i].transform.position.x - BOX_SIZE;
                particles[i].a += new Vector3(-dist * WallStiffness, 0, 0);
            }

            if (particles[i].transform.position.x < -BOX_SIZE) {
                float dist = particles[i].transform.position.x - (-BOX_SIZE);
                particles[i].a += new Vector3(-dist * WallStiffness, 0, 0);
            }

            if (particles[i].transform.position.z > BOX_SIZE) {
                float dist = particles[i].transform.position.z - BOX_SIZE;
                particles[i].a += new Vector3(0, 0, -dist * WallStiffness);
            }

            if (particles[i].transform.position.z < -BOX_SIZE) {
                float dist = particles[i].transform.position.z - (-BOX_SIZE);
                particles[i].a += new Vector3(0, 0, -dist * WallStiffness);
            }
        }
    }

    void CalcTimeStep() {
        for (int i = 0; i < numParticles; i++) {
            particles[i].v += particles[i].a * Time.deltaTime / 100;
            Vector3 p = particles[i].transform.position;
            if (p.x < BOX_SIZE && p.x > -BOX_SIZE && p.y > -BOX_SIZE && p.z < BOX_SIZE && p.z > -BOX_SIZE) {
                particles[i].v = Vector3.ClampMagnitude(particles[i].v, 5);
            }
            Vector3 newPos = particles[i].transform.position + particles[i].v * Time.deltaTime;
            particles[i].transform.position = newPos;
        }
    }

    void SolveSPH() {
        gridSearcher.UpdateHashGrid(particles); // Update the hash grid for neighbor search
        CalcDensity(); // Calculate density for each particle
        CalcPressure(); // Calculate pressure for each particle
        CalcPressureGradient(); // Calculate pressure gradient for each particle
        CalcViscocity(); // Calculate viscosity for each particle
        CalcAcceleration(); // Calculate acceleration for each particle
        CalcWallPenalty(); // Apply wall penalty to particles near the boundaries
        CalcTimeStep(); // Update positions and velocities of particles
    }
}
