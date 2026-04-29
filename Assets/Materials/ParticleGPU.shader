// Reads particle positions and velocities directly from the GPU compute buffer.
// No CPU readback needed — used with DrawMeshInstancedIndirect.
// Place in Assets/Materials/ParticleGPU.shader

Shader "Custom/ParticleGPU"
{
    Properties
    {
        _MaxSpeed     ("Max Speed",     Float) = 10.0
        _ParticleSize ("Particle Size", Float) = 0.1
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Back
        ZWrite On

        Pass
        {
            Tags { "LightMode"="UniversalForward" }

            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   4.5    // Required for StructuredBuffer + SV_InstanceID

            #include "UnityCG.cginc"

            // ----------------------------------------------------------------
            // Must match GPUParticle struct in HydrodynamicsManagerGPU.cs
            // and Particle struct in FluidSimGPU.compute (44 bytes)
            // ----------------------------------------------------------------
            struct Particle
            {
                float3 position;
                float3 velocity;
                float3 acceleration;
                float  density;
                float  pressure;
            };

            StructuredBuffer<Particle> _ParticleBuffer;
            float _MaxSpeed;
            float _ParticleSize;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 normal : TEXCOORD0;
                float4 color  : TEXCOORD1;
            };

            // Same color ramp as baseline GetSpeedColor() for a fair visual comparison
            float4 SpeedToColor(float speed)
            {
                float t = saturate(speed / _MaxSpeed);
                float4 col;
                if (t < 0.5)
                {
                    // Dark blue → mint blue
                    col = lerp(float4(0.0, 0.0, 0.3, 1),
                               float4(0.3, 0.8, 0.8, 1),
                               t * 2.0);
                }
                else if (t < 0.65)
                {
                    // Mint blue → yellow
                    col = lerp(float4(0.3, 0.8, 0.8, 1),
                               float4(1.0, 1.0, 0.0, 1),
                               (t - 0.5) / 0.15);
                }
                else
                {
                    // Yellow → red/orange
                    col = lerp(float4(1.0, 1.0, 0.0, 1),
                               float4(1.0, 0.3, 0.0, 1),
                               (t - 0.65) / 0.35);
                }
                return col;
            }

            v2f vert(appdata v, uint instanceID : SV_InstanceID)
            {
                Particle p = _ParticleBuffer[instanceID];

                // Scale mesh vertex by particle size, then offset to world position
                float3 worldPos = p.position + v.vertex.xyz * _ParticleSize;

                v2f o;
                o.vertex = UnityWorldToClipPos(float4(worldPos, 1.0));
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.color  = SpeedToColor(length(p.velocity));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Simple wrap lighting — matches visual feel of baseline
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float  ndotl    = dot(normalize(i.normal), lightDir) * 0.5 + 0.5;
                return i.color * ndotl;
            }

            ENDCG
        }
    }
    FallBack Off
}
