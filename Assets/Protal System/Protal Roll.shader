Shader "Unlit/Protal Roll"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Speed ("Rotate Speed", Float) = 1
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5 // 透明裁切門檻
    }
    SubShader
    {
        // 1. 改為 AlphaTest（裁切）模式
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }

        Cull Off     // 開啟雙面渲染
        ZWrite On    // 開啟深度寫入，防止前後面互相穿透

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Speed;
            fixed _Cutoff;

            struct v2f { 
                float4 pos : SV_POSITION; 
                float2 uv : TEXCOORD0; 
            };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv - 0.5;           // 以中心為軸
                float a = _Time.y * _Speed;
                float s = sin(a), c = cos(a);
                uv = float2(uv.x * c - uv.y * s, uv.x * s + uv.y * c);
                uv += 0.5;

                fixed4 col = tex2D(_MainTex, uv);
                
                // 關鍵：將 Alpha 低於門檻的像素直接剔除（丟棄）
                clip(col.a - _Cutoff);

                return col;
            }
            ENDCG
        }
    }
}