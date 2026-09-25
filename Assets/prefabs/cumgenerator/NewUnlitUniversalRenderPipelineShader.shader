Shader "Custom/FluidParticle"
{
    Properties
    {
        _Color ("Fluid Color", Color) = (1, 1, 1, 1) // Honey color
        _ParticleSize ("Particle Size", Float) = 0.1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            
            // This is the magic command for GPU Instancing
            #pragma instancing_options procedural:setup

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            // 1. Must match our C# and Compute Shader perfectly
            struct Particle
            {
                float3 position;
                float3 velocity;
            };

            StructuredBuffer<Particle> particleBuffer;
            float4 _Color;
            float _ParticleSize;

            // 2. This function runs on the GPU before the vertex shader
            void setup()
            {
            #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
                // Grab the correct particle from the buffer using its ID
                Particle p = particleBuffer[unity_InstanceID];
                
                // Build a matrix to move the mesh to the particle's position and scale it
                unity_ObjectToWorld._11_21_31_41 = float4(_ParticleSize, 0, 0, 0);
                unity_ObjectToWorld._12_22_32_42 = float4(0, _ParticleSize, 0, 0);
                unity_ObjectToWorld._13_23_33_43 = float4(0, 0, _ParticleSize, 0);
                unity_ObjectToWorld._14_24_34_44 = float4(p.position.x, p.position.y, p.position.z, 1);
            #endif
            }

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                
                // Apply the matrix we built in setup() to the mesh vertices
                o.vertex = mul(UNITY_MATRIX_VP, mul(unity_ObjectToWorld, v.vertex));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return _Color; // Render our honey color
            }
            ENDCG
        }
    }
}