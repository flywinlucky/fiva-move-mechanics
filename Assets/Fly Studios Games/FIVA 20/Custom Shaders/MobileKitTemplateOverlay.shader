Shader "FlyStudios/Mobile/KitTemplateOverlay"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _TemplateTex ("Template Overlay (RGBA)", 2D) = "white" {}
        _OverlayTint ("Overlay Tint", Color) = (1,1,1,1)
        _OverlayStrength ("Overlay Strength", Range(0, 1)) = 1
        _TemplateAffectsBase ("Template Detail On Base", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 150

        CGPROGRAM
        #pragma surface surf Lambert noforwardadd nolightmap nodynlightmap
        #pragma target 2.0
        #pragma multi_compile_instancing

        sampler2D _TemplateTex;
        fixed4 _BaseColor;
        fixed4 _OverlayTint;
        half _OverlayStrength;
        half _TemplateAffectsBase;

        struct Input
        {
            float2 uv_TemplateTex;
        };

        inline half Luma(half3 color)
        {
            return dot(color, half3(0.299h, 0.587h, 0.114h));
        }

        void surf(Input IN, inout SurfaceOutput o)
        {
            fixed4 templateSample = tex2D(_TemplateTex, IN.uv_TemplateTex);

            half overlayMask = saturate(templateSample.a * _OverlayStrength);
            half detailMask = overlayMask * _TemplateAffectsBase;
            half baseDetail = lerp(1.0h, Luma(templateSample.rgb), detailMask);

            half3 baseColor = _BaseColor.rgb * baseDetail;
            half3 overlayColor = templateSample.rgb * _OverlayTint.rgb;

            o.Albedo = lerp(baseColor, overlayColor, overlayMask);
            o.Alpha = 1.0h;
        }
        ENDCG
    }

    FallBack "Mobile/Diffuse"
}
