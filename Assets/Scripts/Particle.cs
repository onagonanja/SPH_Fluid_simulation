using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Particle : MonoBehaviour {
    public Vector3 v;
    public Vector3 a;
    public float density = 1f;
    public float mass = 0.04f;
    public float pressure = 0f;
    public Vector3 pressureGradient = Vector3.zero;
    public Vector3 viscosity = Vector3.zero;

    // Start is called before the first frame update
    void Start() {
        v = new Vector3(0, 0, 0);
        a = new Vector3(0, 0, 0);
    }

    // Update is called once per frame
    void Update() {

    }
}
