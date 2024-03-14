Shader "Unlit/CglShader"
{
    Properties
    {
        _OnColor ("OnColor", Color) = (0.75,0.75,0.75,1)
        _OffColor ("OffColor", Color) = (0, 0, 0, 1)
        _OobColor ("OobColor", Color) = (0.25, 0.25, 0.25, 1)
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
        }
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
                float4 vertex : SV_POSITION;
            };

            float4 _OnColor;
            float4 _OffColor;
            float4 _OobColor;
            float4 _offset;

            StructuredBuffer<uint> _buffer;
            uint _length;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                float2 uv = v.uv;
                uv.y = 1 - uv.y;
                o.uv = (uv - float2(0.5f, 0.5f)) * 2 - _offset;

                return o;
            }

            fixed4 run(float2 uv, uint offset, uint maxLength)
            {
                int2 pos = uv * 4 * 8;
                if (pos.x < 0 || pos.x >= 4 * 16 || pos.y < 0 || pos.y >= 4 * 16)
                {
                    return _OobColor;
                }

                int bitX = pos.x % 8;
                int groupX = pos.x / 8;

                int byteY = pos.y % 8;
                int groupY = pos.y / 8;

                int index = (byteY / 4) + groupX * 2 + groupY * 2 * 4;
                uint bit = 8 * (byteY % 4) + bitX;

                if (index < 0 || index >= maxLength)
                {
                    return float4(index / (float)maxLength, bit / 32.0, frac(index / 4.0), 1);
                }

                uint value = _buffer[index + offset];

                bool alive = (value >> bit) & 1;
                return alive ? _OnColor : _OffColor;;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;

                if (uv.x < 0 && uv.y < 0)
                {
                    uv += float2(1, 1);
                    return run(uv, 0, _length / 4);
                }

                if (uv.x >= 0 && uv.y < 0)
                {
                    uv.y += 1;
                    return run(uv, _length / 4, _length / 4);
                }

                if (uv.x < 0 && uv.y >= 0)
                {
                    uv.x += 1;
                    return run(uv, _length / 2, _length / 4);
                }

                if (uv.x >= 0 && uv.y >= 0)
                {
                    return run(uv, _length - _length / 4, _length / 4);
                }

                return float4(1, 0, 0, 1);
            }
            ENDCG
        }
    }
}