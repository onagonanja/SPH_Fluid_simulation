using System.Collections.Generic;
using UnityEngine;

public class HashGridSearcher {
    Vector3 gridBaseMin;
    Vector3 gridBaseMax;
    float gridSize;
    HashSet<int>[][][] buket;

    public HashGridSearcher(float gridSize) {
        GameObject sph = GameObject.Find("SPH_solver");
        SPH_Compute_solver solver = sph.GetComponent<SPH_Compute_solver>();
        gridBaseMin = new Vector3(-solver.BOX_SIZE, -solver.BOX_SIZE, -solver.BOX_SIZE) * 10;
        gridBaseMax = new Vector3(solver.BOX_SIZE, solver.BOX_SIZE, solver.BOX_SIZE) * 10;
        int gridSizeX = Mathf.CeilToInt((gridBaseMax.x - gridBaseMin.x) / gridSize);
        int gridSizeY = Mathf.CeilToInt((gridBaseMax.y - gridBaseMin.y) / gridSize);
        int gridSizeZ = Mathf.CeilToInt((gridBaseMax.z - gridBaseMin.z) / gridSize);
        buket = new HashSet<int>[gridSizeX][][];

        for (int i = 0; i < gridSizeX; i++) {
            buket[i] = new HashSet<int>[gridSizeY][];
            for (int j = 0; j < gridSizeY; j++) {
                buket[i][j] = new HashSet<int>[gridSizeZ];
                for (int k = 0; k < gridSizeZ; k++) {
                    buket[i][j][k] = new HashSet<int>();
                }
            }
        }
    }

    public void UpdateHashGrid(Particle[] particles) {
        Clear();
        for (int i = 0; i < particles.Length; i++) {
            Particle p = particles[i];
            Vector3 pos = p.transform.position;
            int x = Mathf.FloorToInt((pos.x - gridBaseMin.x) / gridSize);
            int y = Mathf.FloorToInt((pos.y - gridBaseMin.y) / gridSize);
            int z = Mathf.FloorToInt((pos.z - gridBaseMin.z) / gridSize);

            if (x >= 0 && x < buket.Length && y >= 0 && y < buket[x].Length && z >= 0 && z < buket[x][y].Length) {
                buket[x][y][z].Add(i);
            }
        }
    }

    public void forEachNeighbor(Particle[] particles, System.Action<int, int> action) {
        for (int i = 0; i < particles.Length; i++) {
            Particle p = particles[i];
            Vector3 pos = p.transform.position;
            int x = Mathf.FloorToInt((pos.x - gridBaseMin.x) / gridSize);
            int y = Mathf.FloorToInt((pos.y - gridBaseMin.y) / gridSize);
            int z = Mathf.FloorToInt((pos.z - gridBaseMin.z) / gridSize);

            for (int dx = -1; dx <= 1; dx++) {
                for (int dy = -1; dy <= 1; dy++) {
                    for (int dz = -1; dz <= 1; dz++) {
                        int nx = x + dx;
                        int ny = y + dy;
                        int nz = z + dz;

                        if (nx >= 0 && nx < buket.Length && ny >= 0 && ny < buket[nx].Length && nz >= 0 && nz < buket[nx][ny].Length) {
                            foreach (int neighborIndex in buket[nx][ny][nz]) {
                                action(i, neighborIndex);
                            }
                        }
                    }
                }
            }
        }
    }

    public void Clear() {
        for (int i = 0; i < buket.Length; i++) {
            for (int j = 0; j < buket[i].Length; j++) {
                for (int k = 0; k < buket[i][j].Length; k++) {
                    buket[i][j][k].Clear();
                }
            }
        }
    }
}
