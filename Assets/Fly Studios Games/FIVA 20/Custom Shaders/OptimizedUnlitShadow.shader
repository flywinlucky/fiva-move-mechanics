Shader "Unlit/TransparentWithColor_Optimized"
{
    Properties
    {
        // Textura ta albă (PNG-ul cu shadow)
        _MainTex ("Texture (White with Alpha)", 2D) = "white" {}
        // Culoarea pe care o dorești (seteaz-o pe Negru în Inspector)
        _Color ("Shadow Color", Color) = (0,0,0,1)
        // Control global al transparenței (din material)
        _AlphaMultiplier ("Shadow Opacity", Range(0.0, 1.0)) = 1.0
    }

    SubShader
    {
        // Definim că obiectul este transparent și trebuie redat după obiectele opace
        Tags 
        { 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent" 
            "PreviewType"="Plane" // Arată mai bine previzualizarea
        }

        // Setările de Blending (standard pentru transparență)
        Cull Off       // Nu face backface culling, shadow-ul se vede din ambele părți (pentru un Quad)
        Lighting Off   // Ultra-optimizat: nu calculează deloc lumină
        ZWrite Off     // Nu scrie în Depth Buffer, shadow-urile se suprapun corect
        Blend SrcAlpha OneMinusSrcAlpha // Formula standard de transparență

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0 // Esențial pentru WebGL vechi și mobile, foarte compatibil
            
            // Această directivă permite GPU Instancing
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID // Necesar pentru Instancing
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO // Necesar pentru VR (opțional)
            };

            sampler2D _MainTex;
            float4 _MainTex_ST; // Necesar pentru Tiling & Offset
            
            // Folosim UNITY_INSTANCING_BUFFER pentru a stoca proprietățile materialului
            // dacă activăm GPU Instancing
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(fixed, _AlphaMultiplier)
            UNITY_INSTANCING_BUFFER_END(Props)

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v); // Setup Instancing
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); // Setup VR (opțional)
                
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i); // Setup Instancing în fragment shader
                
                // 1. Citim culoarea din textura albă
                fixed4 col = tex2D(_MainTex, i.uv);
                
                // 2. Extragem proprietățile optimizate pentru Instancing
                fixed4 targetColor = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                fixed alphaMultiplier = UNITY_ACCESS_INSTANCED_PROP(Props, _AlphaMultiplier);

                // --- ALGORITMUL OPTIMIZAT ---
                // a. Păstrăm informația de transparență (Alpha) din PNG-ul tău original (col.a)
                // b. Înmulțim transparența din PNG cu transparența globală setată în material (alphaMultiplier)
                fixed finalAlpha = col.a * alphaMultiplier;
                
                // c. Rezultatul final: Culoarea setată (Negru), dar cu transparența combinată
                // (Ignorăm complet componenta RGB albă din PNG)
                return fixed4(targetColor.rgb, finalAlpha);
            }
            ENDCG
        }
    }
}