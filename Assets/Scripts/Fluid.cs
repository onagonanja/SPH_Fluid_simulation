using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Fluid : MonoBehaviour {
    public GameObject particlePrefab; // Prefab for the particles
    public Particle[] particles; // Array of particles in the fluid

    public int numParticles = 100; // Number of particles in the fluid

    // Start is called before the first frame update
    void Start() {
        // particles = new Particle[numParticles]; // Initialize the array of particles

        // for (int i = 0; i < numParticles; i++) {
        //     // Instantiate a new particle and set its position
        //     GameObject particleObject = Instantiate(particlePrefab, Random.insideUnitSphere * 5, Quaternion.identity);
        //     particles[i] = particleObject.GetComponent<Particle>();
        //     particles[i].transform.parent = transform; // Set the parent to the fluid object
        //     particles[i].transform.localPosition = new Vector3(Random.Range(-5f, 5f), Random.Range(-5f, 5f), Random.Range(-5f, 5f)); // Set random position within a sphere of radius 5
        // }
    }

    // Update is called once per frame
    void Update() {
    }
}
