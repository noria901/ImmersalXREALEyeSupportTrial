// YUV_420_888 (planar Y/U/V textures) -> RGB, BT.601 video range.
// If colors look washed out / crushed on device, toggle _FullRange.
Shader "ImmersalXREALEye/YUVToRGB"
{
    Properties
    {
        _YTex ("Y", 2D) = "black" {}
        _UTex ("U", 2D) = "gray" {}
        _VTex ("V", 2D) = "gray" {}
        [Toggle] _FullRange ("Full range (0-255)", Float) = 0
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _YTex, _UTex, _VTex;
            float _FullRange;

            fixed4 frag (v2f_img i) : SV_Target
            {
                float y = tex2D(_YTex, i.uv).r;
                float u = tex2D(_UTex, i.uv).r - 0.5;
                float v = tex2D(_VTex, i.uv).r - 0.5;

                if (_FullRange < 0.5)
                    y = (y - 16.0/255.0) * (255.0/219.0); // video range expand

                float r = y + 1.402 * v;
                float g = y - 0.344136 * u - 0.714136 * v;
                float b = y + 1.772 * u;
                return fixed4(saturate(r), saturate(g), saturate(b), 1);
            }
            ENDCG
        }
    }
}
