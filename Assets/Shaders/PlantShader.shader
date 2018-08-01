Shader "Custom/PlantShader" {
	Properties {
		_Color ("Color", Color) = (1,1,1,1)
		_MainTex ("Albedo (RGB)", 2D) = "white" {}
		_Roughness ("_Roughness", 2D) = "white" {}
		_Metallic ("Metallic", Range(0,1)) = 0.5
		_Normal ("Normal", 2D) = "white" {}
		_Opacity ("Opacity", 2D) = "white" {}
		_Distortion ("Distortion", Float) = 0.5
		_Power ("Power", Float) = 2.0
		_Scale ("Scale", Float) = 1.0
		_Attenuation ("Attenuation", Float) = 1.0
		_Ambient ("Ambient", Float) = 0.2
		_Thickness ("Thickness", Float) = 1.0
		_SSSColor ("SSS Color", Color) = (1,1,1,1)
	}
	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 200

		CGPROGRAM
		// Physically based Standard lighting model, and enable shadows on all light types
		#pragma surface surf StandardTranslucent fullforwardshadows alpha:blend

		// Use shader model 3.0 target, to get nicer looking lighting
		#pragma target 3.0

		sampler2D 	_MainTex;
		sampler2D 	_Normal;
		sampler2D	_Roughness;
		sampler2D	_Opacity;

		struct Input {
			float2 uv_MainTex;
			float2 uv_Normal;
			float2 uv_Roughness;
			float2 uv_Opacity;
		};

		half 		_Metallic;
		fixed4 		_Color;

		// SSS Parameters
		float 		_Distortion;
		float		_Power;
		float		_Scale;
		float		_Attenuation;
		float		_Ambient;
		float		_Thickness;
		float4		_SSSColor;

		#include "UnityPBSLighting.cginc"

		inline fixed4 LightingStandardTranslucent(SurfaceOutputStandard s, fixed3 viewDir, UnityGI gi)
		{
			// Original colour
			fixed4 pbr = LightingStandard(s, viewDir, gi);
			
			// --- Translucency ---
			float3 L = gi.light.dir;
			float3 V = viewDir;
			float3 N = s.Normal;

			float3 H = normalize(L + N * _Distortion);
			float VdotH = pow(saturate(dot(V, -H)), _Power) * _Scale;
			float3 I = _Attenuation * (VdotH + _Ambient) * _Thickness;

			// Final add
			pbr.rgb = pbr.rgb + (gi.light.color * I * _SSSColor * s.Albedo);
			return pbr;
		}
 
		void LightingStandardTranslucent_GI(SurfaceOutputStandard s, UnityGIInput data, inout UnityGI gi)
		{
			LightingStandard_GI(s, data, gi); 
		}


		void surf (Input IN, inout SurfaceOutputStandard o) {

			half3 inputNormal = UnpackNormal(tex2D(_Normal, IN.uv_Normal));
			fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
			half roughnessValue = tex2D(_Roughness, IN.uv_Roughness).r;
			half alphaValue = tex2D(_Opacity, IN.uv_Opacity).r;

			o.Albedo = c.rgb;
			o.Metallic = _Metallic;
			o.Normal = inputNormal;
			o.Smoothness = 1.0f - roughnessValue;
			o.Alpha = alphaValue;

		}
		ENDCG
	}
	FallBack "Diffuse"
}
