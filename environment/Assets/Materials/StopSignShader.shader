Shader "Unlit/StopSignShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _RotationAngle ("Rotation Angle", Range(0, 360)) = 15
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _RotationAngle;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);

                // Apply Unity's tiling and offset first
                float2 tiledUV = TRANSFORM_TEX(v.uv, _MainTex);

                // Convert rotation angle to radians
                float rad = radians(_RotationAngle);
                float2x2 rotationMatrix = float2x2(
                    cos(rad), -sin(rad),
                    sin(rad),  cos(rad)
                );

                // Rotate UVs around the center (0.5, 0.5)
                float2 centeredUV = tiledUV - 0.5;
                o.uv = mul(rotationMatrix, centeredUV) + 0.5;

                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
