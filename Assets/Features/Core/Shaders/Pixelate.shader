// Полноэкранный постэффект пикселизации для URP Full Screen Pass Renderer Feature.
// _PixelSize - размер "пикселя" в реальных экранных пикселях (1 = эффект незаметен).
Shader "Hidden/Pixelate"
{
    Properties
    {
        _PixelSize ("Pixel Size", Range(1, 64)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "Pixelate"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _PixelSize;

            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 texSize = _BlitTexture_TexelSize.zw; // ширина/высота источника в пикселях

                float2 pixelatedUV = floor(uv * texSize / _PixelSize) * _PixelSize / texSize;

                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, pixelatedUV);
            }
            ENDHLSL
        }
    }
}
