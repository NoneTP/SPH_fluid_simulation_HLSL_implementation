# GPU-Accelerated 3D SPH Fluid Simulation

A real-time 3D fluid simulation built in Unity 6 using HLSL compute shaders, implementing Smoothed Particle Hydrodynamics (SPH). This project improves upon an existing CPU-based SPH baseline by porting the full physics pipeline to the GPU with additional stability enhancements.

**Course:** Computer Animation (2110512) — Chulalongkorn University, Semester 2/2568  
**Author:** NoneTP | GitHub: NoneTP  
**Baseline Reference:** [wendtpiotr/sph-fluid-simulation](https://github.com/wendtpiotr/sph-fluid-simulation)

---

## Demo

https://youtu.be/YEzDSzDxI_Q
---

## Problem

Simulating fluids in real time using SPH requires computing forces between every pair of nearby particles — an O(N²) operation. At 10,000 particles this means 100 million pairwise interactions per frame. Running this sequentially on the CPU limits the baseline to ~13.7 FPS at 10k particles, with visible numerical instability at higher counts.

---

## Approach

### Baseline (Wendt 2025 — CPU)
- Full SPH pipeline in C# on a single CPU thread
- Dictionary-based 3D spatial hash for neighbour queries
- Rendering via MaterialPropertyBlock batching
- Uses `Time.deltaTime` (variable timestep) — source of instability
- No predicted positions, no substeps

### Proposed (This Project — GPU)
- All SPH math ported to four HLSL compute shader kernels
- Particles live permanently in `ComputeBuffer` on the GPU — no CPU readback
- Rendering via `DrawMeshInstancedIndirect` reading positions directly from GPU buffer

**Stability improvements over baseline:**

| Improvement | What it fixes |
|---|---|
| Predicted positions | Computes forces from `pos + vel * dt` instead of stale current positions |
| Fixed timestep + substeps | Replaces variable `Time.deltaTime` with fixed `dt = 1/60`, 3 substeps/frame |
| Poly6 viscosity weighting | Wider damping radius catches isolated fast-moving particles |
| XSPH velocity correction | Blends each particle's velocity with neighbours for coherent flow |
| Contact spring repulsion | Short-range force prevents particle interpenetration |

---

## Results

Tested on Intel Core Ultra 5 225H (integrated GPU, shared DDR5 memory):

| Particles | Baseline FPS | Baseline ms | GPU FPS | GPU ms | Speedup |
|-----------|-------------|-------------|---------|--------|---------|
| 500       | 181.5       | 5.92        | 100.4   | 10.21  | 0.58x   |
| 2,500     | 55.0        | 18.54       | 75.8    | 13.48  | 1.38x   |
| 5,000     | 29.0        | 34.55       | 56.2    | 17.85  | 1.94x   |
| 10,000    | 13.7        | 73.08       | 26.8    | 37.50  | 1.95x   |
| 20,000    | 3.0         | 333.33      | 10.4    | 96.65  | 3.45x   |

> Note: The GPU version underperforms at 500 particles due to kernel dispatch overhead. The crossover point is ~1,500–2,000 particles. On dedicated GPU hardware (RTX 3060) the GPU version exceeds 60 FPS at 20,000 particles.

---

## How to Run

### Requirements
- Unity 6 LTS (6000.2.8f1 or newer)
- Universal Render Pipeline package
- GPU with DirectX 11 / Shader Model 4.5 support (any GPU from ~2012+)

### Steps

1. Clone this repository:
   ```bash
   git clone https://github.com/NoneTP/SPH_fluid_simulation_HLSL_implementation.git
   ```

2. Open the project in Unity Hub → select `FluidSim3D` folder

3. Open `Assets/Scenes/SampleScene.unity`

4. Hit **Play**

### Inspector Parameters (live-tunable during simulation)

| Parameter | Default | Description |
|---|---|---|
| Particle Count | 12,000 | Total particles (restart required to change) |
| Rest Density | 5.5 | Target fluid density |
| Gas Constant | 50 | Pressure stiffness — lower = softer fluid |
| Viscosity | 0.3 | Fluid thickness |
| Smoothing Radius | 0.3 | SPH kernel radius (world units) |
| Fixed Dt | 1/120 | Integration timestep |
| Substeps | 3 | Physics passes per frame |
| Particle Size | 0.1 | Visual size of each sphere |

### Benchmarking

A `BenchmarkLogger` component is attached to the main GameObject. It auto-starts recording after 1 second of play and saves results to:
```
experiments/results/<RunLabel>_<timestamp>.csv
```
Change the **Run Label** field in the Inspector before each test.

---

## Project Structure

```
Assets/
├── Scenes/
│   └── SampleScene.unity
├── Scripts/
│   ├── HydrodynamicsManagerGPU.cs   ← GPU simulation driver
│   ├── IcosphereGenerator.cs         ← Sphere mesh generation
│   └── BenchmarkLogger.cs            ← Performance recording
├── Shaders/
│   ├── FluidSimGPU.compute           ← All 4 SPH compute kernels
│   └── ParticleGPU.shader            ← Instance rendering shader
└── Materials/
    └── ParticleGPU.mat               ← Material using ParticleGPU shader

experiments/
└── results/                          ← Benchmark CSV output

final_report/
└── final_report.docx                 ← Project report
```

---

## GPU Kernel Pipeline

Each simulation frame dispatches four kernels in sequence:

```
1. PredictPositions    →  pos_pred = pos + vel * dt
2. CalculateDensities  →  ρᵢ = Σⱼ m · W_poly6(|r_pred_ij|², h)
                           Pᵢ = k · ((ρᵢ/ρ₀)⁷ - 1)
3. CalculateForces     →  F_pressure + F_viscosity + F_spring
4. Integrate           →  vel += accel * dt  |  XSPH correction  |  AABB boundary
```

All kernels use `[numthreads(256, 1, 1)]`. Thread `id.x` handles particle `id.x` — no thread communication required.

---

## Known Limitations

- **O(N²) neighbour search** — no GPU spatial hash yet. Adding bitonic sort + hash grid would enable 100k+ particles in real time.
- **Near-pressure force** — implemented but caused instability on iGPU; removed pending further tuning.
- **iGPU shared memory** — baseline and GPU version share the same DDR5 bus, limiting the speedup advantage vs. dedicated GPU.
- **No surface rendering** — particles rendered as spheres; marching cubes surface reconstruction would improve visual quality.

---

## References

1. Müller, Charypar, Gross — *Particle-Based Fluid Simulation for Interactive Applications*, SCA 2003
2. Becker, Teschner — *Weakly Compressible SPH for Free Surface Flows*, SCA 2007
3. Schechter, Bridson — *Ghost SPH for Animating Water*, ACM TOG 2012
4. Wendt (2025) — [sph-fluid-simulation](https://github.com/wendtpiotr/sph-fluid-simulation) (baseline)
5. Lague (2023) — [Coding Adventure: Simulating Fluids](https://www.youtube.com/watch?v=rSKMYc1CQHE)
