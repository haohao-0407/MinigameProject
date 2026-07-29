Shader "Hidden/PixelArt/PaletteLab"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Palette Lab Mapping"

            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragPaletteLab

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #define MAX_PALETTE_COLORS 32

            float4 _PaletteColors[MAX_PALETTE_COLORS];
            int _PaletteCount;

            float3 CameraColorToSrgb(float3 rgb)
            {
                rgb = saturate(rgb);

                #if defined(UNITY_COLORSPACE_GAMMA)
                    return rgb;
                #else
                    return pow(rgb, 1.0 / 2.2);
                #endif
            }

            float3 SrgbToCameraColor(float3 rgb)
            {
                rgb = saturate(rgb);

                #if defined(UNITY_COLORSPACE_GAMMA)
                    return rgb;
                #else
                    return pow(rgb, 2.2);
                #endif
            }

            float3 RgbToXyz(float3 rgb)
            {
                // The palette and source are compared in sRGB space, following
                // the requested 2.2 gamma approximation before the XYZ matrix.
                rgb = pow(saturate(rgb), 2.2);

                float3x3 rgbToXyz = float3x3(
                    0.4124564, 0.3575761, 0.1804375,
                    0.2126729, 0.7151522, 0.0721750,
                    0.0193339, 0.1191920, 0.9503041
                );

                return mul(rgbToXyz, rgb);
            }

            float3 XyzToLab(float3 xyz)
            {
                // D65 reference white.
                xyz /= float3(0.95047, 1.0, 1.08883);

                float3 cubic = pow(max(xyz, 0.0), 1.0 / 3.0);
                float3 linearPart = 7.787 * xyz + 16.0 / 116.0;
                float3 f = lerp(linearPart, cubic, step(0.008856, xyz));

                float L = 116.0 * f.y - 16.0;
                float a = 500.0 * (f.x - f.y);
                float b = 200.0 * (f.y - f.z);
                return float3(L, a, b);
            }

            float3 RgbToLab(float3 rgb)
            {
                return XyzToLab(RgbToXyz(rgb));
            }

            float4 FragPaletteLab(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float4 source = SAMPLE_TEXTURE2D_X_LOD(
                    _BlitTexture,
                    sampler_PointClamp,
                    input.texcoord.xy,
                    _BlitMipLevel);

                float3 sourceSrgb = CameraColorToSrgb(source.rgb);
                float3 sourceLab = RgbToLab(sourceSrgb);
                float3 closestColor = sourceSrgb;
                float closestDistanceSquared = 3.402823466e+38;

                [loop]
                for (int i = 0; i < MAX_PALETTE_COLORS; i++)
                {
                    if (i >= _PaletteCount)
                    {
                        break;
                    }

                    float3 paletteColor = saturate(_PaletteColors[i].rgb);
                    float3 difference = sourceLab - RgbToLab(paletteColor);
                    float distanceSquared = dot(difference, difference);

                    if (distanceSquared < closestDistanceSquared)
                    {
                        closestDistanceSquared = distanceSquared;
                        closestColor = paletteColor;
                    }
                }

                return float4(SrgbToCameraColor(closestColor), source.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
