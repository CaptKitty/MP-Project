Shader "Unlit/MapShower"
{
    Properties
    {
        _MainTex ("Main", 2D) = "white" {}
        _RemapTex ("Remap", 2D) = "white" {}
        _PaletteTex ("Palette", 2D) = "white" {}
        _OwnerTex ("Owner", 2D) = "white" {}
        _TerrainTex("Terrain", 2D) = "white" {}
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
            // make fog work
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
            sampler2D _RemapTex;
            sampler2D _PaletteTex;
            sampler2D _TerrainTex;
            sampler2D _OwnerTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                // return tex2D(_MainTex, i.uv) - tex2D(_MainTex, i.uv + float2(0.001, 0));
                fixed4 c1 = tex2D(_MainTex, i.uv + float2(0.0005, 0));
                fixed4 c2 = tex2D(_MainTex, i.uv - float2(0.0005, 0));
                fixed4 c3 = tex2D(_MainTex, i.uv + float2(0, 0.0005));
                fixed4 c4 = tex2D(_MainTex, i.uv - float2(0, 0.0005));

                if(any(c1 != col) || any(c2 != col) || any(c3 != col) || any(c4 != col)){
                    return fixed4(0,0,0,1);
                }


                
                fixed4 index =           tex2D(_RemapTex,    i.uv                                                        );

                // fixed4 yes = lerp(tex2D(_PaletteTex,  index.xy * 255.0 / 256.0 + float2(0.001953125, 0.001953125) ),tex2D(_RemapTex,  index.xy * 255.0 / 256.0 + float2(0.001953125, 0.001953125) ), 0.5) ;
                fixed4 yes = tex2D(_PaletteTex,  index.xy * 255.0 / 256.0 + float2(0.001953125, 0.001953125) );
                fixed4 owner = tex2D(_OwnerTex,  index.xy * 255.0 / 256.0 + float2(0.001953125, 0.001953125) );
                fixed4 ok = pow(tex2D(_TerrainTex,  i.uv), yes.r   );
                // fixed4 potatos = lerp(yes,tex2D(_TerrainTex,  i.uv ), 0.75 ) ;
                // fixed4 potato = lerp(yes,pow(tex2D(_TerrainTex,  i.uv),yes.r), 0.75) ;
                // fixed4 tomato = lerp(potatos,tex2D(_OwnerTex,  index.xy * 255.0 / 256.0 + float2(0.001953125, 0.001953125) ), 0.25);

                fixed4 oki = pow(yes, 4);
                fixed4 okies = pow(ok, 4);
                fixed4 okie = lerp(okies, oki,0.5) ;

                fixed4 include_clicker = lerp(yes,okies, 0.5 ) ;
                fixed4 include_clickers = lerp(owner,include_clicker, 2 ) ;

                // fixed4 potatoss = lerp( tex2D(_OwnerTex,  index.xy * 255.0 / 256.0 + float2(0.001953125, 0.001953125)),tex2D(_TerrainTex,  i.uv ), 0.75 ) ;
                // fixed4 tomatos = pow(potatoss, yes.r   );//lerp(potatos,tex2D(_PaletteTex,  index.xy * 255.0 / 256.0 + float2(0.001953125, 0.001953125)), 0.25);
                
                //return okies;

                return include_clickers;
                

                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
