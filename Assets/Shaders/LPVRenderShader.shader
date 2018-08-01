Shader "Hidden/LPVRenderShader"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
	}
	SubShader
	{
		// No culling or depth
		Cull Off ZWrite Off ZTest Always

		CGINCLUDE

		#include "UnityCG.cginc"

		uniform sampler3D					lpvRedSHFirstCascade;
		uniform sampler3D					lpvGreenSHFirstCascade;
		uniform sampler3D					lpvBlueSHFirstCascade;

		uniform sampler3D					lpvRedSHSecondCascade;
		uniform sampler3D					lpvGreenSHSecondCascade;
		uniform sampler3D					lpvBlueSHSecondCascade;

		uniform sampler3D					lpvRedSHThirdCascade;
		uniform sampler3D					lpvGreenSHThirdCascade;
		uniform sampler3D					lpvBlueSHThirdCascade;

		uniform sampler2D 					_MainTex;
		uniform sampler2D					_CameraDepthTexture;
		uniform sampler2D					_CameraDepthNormalsTexture;
		uniform sampler2D					_CameraGBufferTexture0;
		uniform sampler2D					positionTexture;
		uniform sampler2D					normalTexture;

		uniform float4x4					InverseProjectionMatrix;
		uniform float4x4					InverseViewMatrix;

		uniform float						firstCascadeBoundary;
		uniform float						secondCascadeBoundary;
		uniform float						thirdCascadeBoundary;
		uniform float						indirectLightStrength;

		uniform int							lpvDimension;

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

		struct v2f_world_pos
		{
			float2 uv : TEXCOORD0;
			float4 vertex : SV_POSITION;
			float4 cameraRay : TEXCOORD1;
		};

		// Spherical harmonics coefficients – precomputed
		#define SH_C0 0.282094792f // 1 / 2sqrt(pi)
		#define SH_C1 0.488602512f // sqrt(3/pi) / 2

		// Cosine lobe coeff
		#define SH_cosLobe_C0 0.886226925f // sqrt(pi)/2
		#define SH_cosLobe_C1 1.02332671f // sqrt(pi/3)

		#define PI 3.1415926f

		float4 dirToCosineLobe(float3 dir) {
			return float4(SH_cosLobe_C0, -SH_cosLobe_C1 * dir.y, SH_cosLobe_C1 * dir.z, -SH_cosLobe_C1 * dir.x);
		}

		float4 dirToSH(float3 dir) {
			return float4(SH_C0, -SH_C1 * dir.y, SH_C1 * dir.z, -SH_C1 * dir.x);
		}

		// Function to get position of cell in the first cascade from world position
		inline float3 GetCellPositionFirstCascade (float3 worldPosition)
		{
			float3 encodedPosition = worldPosition / firstCascadeBoundary;
			encodedPosition += float3(1.0f, 1.0f, 1.0f);
			encodedPosition /= 2.0f;
			return encodedPosition;
		}

		// Function to get position of cell in the second cascade from world position
		inline float3 GetCellPositionSecondCascade (float3 worldPosition)
		{
			float3 encodedPosition = worldPosition / secondCascadeBoundary;
			encodedPosition += float3(1.0f, 1.0f, 1.0f);
			encodedPosition /= 2.0f;
			return encodedPosition;
		}

		// Function to get position of cell in the third cascade from world position
		inline float3 GetCellPositionThirdCascade (float3 worldPosition)
		{
			float3 encodedPosition = worldPosition / thirdCascadeBoundary;
			encodedPosition += float3(1.0f, 1.0f, 1.0f);
			encodedPosition /= 2.0f;
			return encodedPosition;
		}

		v2f vert (appdata v)
		{
			v2f o;
			o.vertex = UnityObjectToClipPos(v.vertex);
			o.uv = v.uv;
			return o;
		}

		v2f_world_pos vert_world_pos (appdata v)
		{
			v2f_world_pos o;
			o.vertex = UnityObjectToClipPos(v.vertex);
			o.uv = v.uv;

			//transform clip pos to view space
			float4 clipPos = float4( v.uv * 2.0f - 1.0f, 1.0f, 1.0f);
			float4 cameraRay = mul(InverseProjectionMatrix, clipPos);
			o.cameraRay = cameraRay / cameraRay.w;

			return o;
		}

		float4 frag_position_texture (v2f_world_pos i) : SV_Target
		{
			// read low res depth and reconstruct world position
			float depth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv);
			
			//linearise depth		
			float lindepth = Linear01Depth (depth);
			
			//get view and then world positions		
			float4 viewPos = float4(i.cameraRay.xyz * lindepth, 1.0f);
			float3 worldPos = mul(InverseViewMatrix, viewPos).xyz;

			return float4(worldPos, 1.0f);
		}

		float4 frag_normal_texture (v2f i) : SV_Target
		{
			float depthValue;
			float3 viewSpaceNormal;
			DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture, i.uv), depthValue, viewSpaceNormal);
			viewSpaceNormal = normalize(viewSpaceNormal);
			float3 worldSpaceNormal = mul((float3x3)InverseViewMatrix, viewSpaceNormal);
			worldSpaceNormal = normalize(worldSpaceNormal);
			return float4(worldSpaceNormal, 1.0f);
		}

		float4 frag_lighting (v2f i) : SV_Target
		{
			float3 direct = tex2D(_MainTex, i.uv).rgb;
			float3 indirect = float3(0.0f, 0.0f, 0.0f);

			float3 albedo = tex2D(_CameraGBufferTexture0, i.uv).rgb;
			float ao = tex2D(_CameraGBufferTexture0, i.uv).a;

			float3 worldPosition = tex2D(positionTexture, i.uv);
			float3 worldNormal = tex2D(normalTexture, i.uv);

			float4 SHintensity = dirToSH(normalize(-worldNormal));

			float3 cellPosition = float3(0.0f, 0.0f, 0.0f);

			float4 currentCellRedSH = float4(0.0f, 0.0f, 0.0f, 0.0f);
			float4 currentCellGreenSH = float4(0.0f, 0.0f, 0.0f, 0.0f);
			float4 currentCellBlueSH = float4(0.0f, 0.0f, 0.0f, 0.0f);

			if ((worldPosition.x < firstCascadeBoundary) && (worldPosition.y < firstCascadeBoundary) && (worldPosition.z < firstCascadeBoundary))
			{
				cellPosition = GetCellPositionFirstCascade(worldPosition);

				currentCellRedSH = tex3D(lpvRedSHFirstCascade, cellPosition);
				currentCellGreenSH = tex3D(lpvGreenSHFirstCascade, cellPosition);
				currentCellBlueSH = tex3D(lpvBlueSHFirstCascade, cellPosition);
			} 
			else if ((worldPosition.x < secondCascadeBoundary) && (worldPosition.y < secondCascadeBoundary) && (worldPosition.z < secondCascadeBoundary))
			{
				cellPosition = GetCellPositionSecondCascade(worldPosition);

				currentCellRedSH = tex3D(lpvRedSHSecondCascade, cellPosition);
				currentCellGreenSH = tex3D(lpvGreenSHSecondCascade, cellPosition);
				currentCellBlueSH = tex3D(lpvBlueSHSecondCascade, cellPosition);
			}
			else
			{
				cellPosition = GetCellPositionThirdCascade(worldPosition);

				currentCellRedSH = tex3D(lpvRedSHThirdCascade, cellPosition);
				currentCellGreenSH = tex3D(lpvGreenSHThirdCascade, cellPosition);
				currentCellBlueSH = tex3D(lpvBlueSHThirdCascade, cellPosition);
			}

			indirect = float3(dot(SHintensity, currentCellRedSH), dot(SHintensity, currentCellGreenSH), dot(SHintensity, currentCellBlueSH));
			indirect = (max(0.0f, indirect) / PI);
			indirect *= (albedo * ao);
			indirect *= indirectLightStrength;

			float3 finalLighting = direct + indirect;
			return float4(finalLighting, 1.0f);
		}

		ENDCG

		// 0 : Position texture writing
		Pass
		{
			CGPROGRAM
			#pragma vertex vert_world_pos
			#pragma fragment frag_position_texture
			ENDCG
		}

		// 1 : Normal texture writing
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag_normal_texture
			ENDCG
		}

		// 2 : Final lighting computation
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag_lighting
			ENDCG
		}
	}
}