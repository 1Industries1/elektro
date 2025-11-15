// Made with Amplify Shader Editor v1.9.8.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Drakkar/URP/MultiMaterial/MultiMaterial Trim"
{
	Properties
	{
		[HideInInspector] _EmissionColor("Emission Color", Color) = (1,1,1,1)
		[HideInInspector] _AlphaCutoff("Alpha Cutoff ", Range(0, 1)) = 0.5
		[SingleLineTexture]_SurfaceTexture("SurfaceTexture", 2D) = "white" {}
		[Header(Triplanar Values ______________________________________)]_Tiling("Tiling", Float) = 1
		_Blend("Blend", Range( -5 , 20)) = 1
		[Header(Trim Sheet _____________________________________________)][SingleLineTexture]_TrimSheet("TrimSheet", 2D) = "white" {}
		[NoScaleOffset][Normal][SingleLineTexture]_Normal("Normal", 2D) = "bump" {}
		[IntRange]_VinylSelect("Vinyl Select", Range( 0 , 4)) = 0
		[IntRange]_VinylMaterial("Vinyl Material", Range( 0 , 3)) = 0
		[NoScaleOffset][SingleLineTexture]_VinylTrimsheet("Vinyl Trimsheet", 2D) = "white" {}
		[Header(Materials Definition ____________________________________)]_Color0("Color 0", Color) = (0,0,0,0)
		_Color1("Color 1", Color) = (0,0,0,0)
		_Color2("Color 2", Color) = (0,0,0,0)
		_Color3("Color 3", Color) = (0,0,0,0)
		[HDR]_Emissive1("Emissive 1", Color) = (0,0,0,0)
		[HDR]_Emissive2("Emissive 2", Color) = (0,0,0,0)
		[HDR]_Emissive3("Emissive 3", Color) = (0,0,0,0)
		_Metalness("Metalness", Vector) = (0,1,0.3,0.6)
		_SmoothnessOffset("Smoothness Offset", Vector) = (0,1,0.3,0.6)
		_Pattern("Pattern", Vector) = (0,0,0,0)
		_PatternIntensity("Pattern Intensity", Vector) = (0,0,0,0)
		_ColorOffset("Color Offset", Vector) = (0,1,0.3,0.6)
		[Header(Parallax Occlusion Mapping __________________________)]_ParallaxCropDistance("Parallax Crop Distance", Float) = 5
		_AngleTolerance1("Angle Tolerance", Range( 0.1 , 0.99)) = 0.5
		_ParallaxMaxDistance1("Parallax Max Distance", Float) = 8
		_ParallaxMinDistance1("Parallax Min Distance", Float) = 5
		_ParallaxOffset1("Parallax Offset", Float) = 0.05
		_ReferencePlane("Reference Plane", Range( 0 , 0.7)) = 0.5
		_QualityMultipliers("Quality Multipliers", Vector) = (1,1,1,0)


		//_TransmissionShadow( "Transmission Shadow", Range( 0, 1 ) ) = 0.5
		//_TransStrength( "Trans Strength", Range( 0, 50 ) ) = 1
		//_TransNormal( "Trans Normal Distortion", Range( 0, 1 ) ) = 0.5
		//_TransScattering( "Trans Scattering", Range( 1, 50 ) ) = 2
		//_TransDirect( "Trans Direct", Range( 0, 1 ) ) = 0.9
		//_TransAmbient( "Trans Ambient", Range( 0, 1 ) ) = 0.1
		//_TransShadow( "Trans Shadow", Range( 0, 1 ) ) = 0.5
		//_TessPhongStrength( "Tess Phong Strength", Range( 0, 1 ) ) = 0.5
		//_TessValue( "Tess Max Tessellation", Range( 1, 32 ) ) = 16
		//_TessMin( "Tess Min Distance", Float ) = 10
		//_TessMax( "Tess Max Distance", Float ) = 25
		//_TessEdgeLength ( "Tess Edge length", Range( 2, 50 ) ) = 16
		//_TessMaxDisp( "Tess Max Displacement", Float ) = 25

		[HideInInspector][ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1
		[HideInInspector][ToggleOff] _EnvironmentReflections("Environment Reflections", Float) = 1
		[HideInInspector][ToggleOff] _ReceiveShadows("Receive Shadows", Float) = 1.0

		[HideInInspector] _QueueOffset("_QueueOffset", Float) = 0
        [HideInInspector] _QueueControl("_QueueControl", Float) = -1

        [HideInInspector][NoScaleOffset] unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset] unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
	}

	SubShader
	{
		LOD 0

		

		Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" "UniversalMaterialType"="Lit" }

		Cull Back
		ZWrite On
		ZTest LEqual
		Offset 0,0
		AlphaToMask Off

		

		HLSLINCLUDE
		#pragma target 4.5
		#pragma prefer_hlslcc gles
		// ensure rendering platforms toggle list is visible

		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"

		#ifndef ASE_TESS_FUNCS
		#define ASE_TESS_FUNCS
		float4 FixedTess( float tessValue )
		{
			return tessValue;
		}

		float CalcDistanceTessFactor (float4 vertex, float minDist, float maxDist, float tess, float4x4 o2w, float3 cameraPos )
		{
			float3 wpos = mul(o2w,vertex).xyz;
			float dist = distance (wpos, cameraPos);
			float f = clamp(1.0 - (dist - minDist) / (maxDist - minDist), 0.01, 1.0) * tess;
			return f;
		}

		float4 CalcTriEdgeTessFactors (float3 triVertexFactors)
		{
			float4 tess;
			tess.x = 0.5 * (triVertexFactors.y + triVertexFactors.z);
			tess.y = 0.5 * (triVertexFactors.x + triVertexFactors.z);
			tess.z = 0.5 * (triVertexFactors.x + triVertexFactors.y);
			tess.w = (triVertexFactors.x + triVertexFactors.y + triVertexFactors.z) / 3.0f;
			return tess;
		}

		float CalcEdgeTessFactor (float3 wpos0, float3 wpos1, float edgeLen, float3 cameraPos, float4 scParams )
		{
			float dist = distance (0.5 * (wpos0+wpos1), cameraPos);
			float len = distance(wpos0, wpos1);
			float f = max(len * scParams.y / (edgeLen * dist), 1.0);
			return f;
		}

		float DistanceFromPlane (float3 pos, float4 plane)
		{
			float d = dot (float4(pos,1.0f), plane);
			return d;
		}

		bool WorldViewFrustumCull (float3 wpos0, float3 wpos1, float3 wpos2, float cullEps, float4 planes[6] )
		{
			float4 planeTest;
			planeTest.x = (( DistanceFromPlane(wpos0, planes[0]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos1, planes[0]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos2, planes[0]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.y = (( DistanceFromPlane(wpos0, planes[1]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos1, planes[1]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos2, planes[1]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.z = (( DistanceFromPlane(wpos0, planes[2]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos1, planes[2]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos2, planes[2]) > -cullEps) ? 1.0f : 0.0f );
			planeTest.w = (( DistanceFromPlane(wpos0, planes[3]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos1, planes[3]) > -cullEps) ? 1.0f : 0.0f ) +
							(( DistanceFromPlane(wpos2, planes[3]) > -cullEps) ? 1.0f : 0.0f );
			return !all (planeTest);
		}

		float4 DistanceBasedTess( float4 v0, float4 v1, float4 v2, float tess, float minDist, float maxDist, float4x4 o2w, float3 cameraPos )
		{
			float3 f;
			f.x = CalcDistanceTessFactor (v0,minDist,maxDist,tess,o2w,cameraPos);
			f.y = CalcDistanceTessFactor (v1,minDist,maxDist,tess,o2w,cameraPos);
			f.z = CalcDistanceTessFactor (v2,minDist,maxDist,tess,o2w,cameraPos);

			return CalcTriEdgeTessFactors (f);
		}

		float4 EdgeLengthBasedTess( float4 v0, float4 v1, float4 v2, float edgeLength, float4x4 o2w, float3 cameraPos, float4 scParams )
		{
			float3 pos0 = mul(o2w,v0).xyz;
			float3 pos1 = mul(o2w,v1).xyz;
			float3 pos2 = mul(o2w,v2).xyz;
			float4 tess;
			tess.x = CalcEdgeTessFactor (pos1, pos2, edgeLength, cameraPos, scParams);
			tess.y = CalcEdgeTessFactor (pos2, pos0, edgeLength, cameraPos, scParams);
			tess.z = CalcEdgeTessFactor (pos0, pos1, edgeLength, cameraPos, scParams);
			tess.w = (tess.x + tess.y + tess.z) / 3.0f;
			return tess;
		}

		float4 EdgeLengthBasedTessCull( float4 v0, float4 v1, float4 v2, float edgeLength, float maxDisplacement, float4x4 o2w, float3 cameraPos, float4 scParams, float4 planes[6] )
		{
			float3 pos0 = mul(o2w,v0).xyz;
			float3 pos1 = mul(o2w,v1).xyz;
			float3 pos2 = mul(o2w,v2).xyz;
			float4 tess;

			if (WorldViewFrustumCull(pos0, pos1, pos2, maxDisplacement, planes))
			{
				tess = 0.0f;
			}
			else
			{
				tess.x = CalcEdgeTessFactor (pos1, pos2, edgeLength, cameraPos, scParams);
				tess.y = CalcEdgeTessFactor (pos2, pos0, edgeLength, cameraPos, scParams);
				tess.z = CalcEdgeTessFactor (pos0, pos1, edgeLength, cameraPos, scParams);
				tess.w = (tess.x + tess.y + tess.z) / 3.0f;
			}
			return tess;
		}
		#endif //ASE_TESS_FUNCS
		ENDHLSL

		
		Pass
		{
			
			Name "Forward"
			Tags { "LightMode"="UniversalForward" }

			Blend One Zero, One Zero
			ZWrite On
			ZTest LEqual
			Offset 0 , 0
			ColorMask RGBA

			

			HLSLPROGRAM

			

			#define _NORMAL_DROPOFF_TS 1
			#pragma shader_feature_local _RECEIVE_SHADOWS_OFF
			#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
			#pragma multi_compile_instancing
			#pragma instancing_options renderinglayer
			#pragma multi_compile_fog
			#define ASE_FOG 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19801
			#define ASE_SRP_VERSION 140011


			

			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
			#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS

			
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
		

			#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION

			

			
			#pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
           

			#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
			#pragma multi_compile _ _LIGHT_LAYERS
			#pragma multi_compile_fragment _ _LIGHT_COOKIES
			#pragma multi_compile _ _FORWARD_PLUS

			

			#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
			#pragma multi_compile _ SHADOWS_SHADOWMASK
			#pragma multi_compile _ DIRLIGHTMAP_COMBINED
			#pragma multi_compile _ LIGHTMAP_ON
			#pragma multi_compile _ DYNAMICLIGHTMAP_ON

			#pragma vertex vert
			#pragma fragment frag

			#if defined(_SPECULAR_SETUP) && defined(_ASE_LIGHTING_SIMPLE)
				#define _SPECULAR_COLOR 1
			#endif

			#define SHADERPASS SHADERPASS_FORWARD

			
            #if ASE_SRP_VERSION >=140007
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#endif
		

			
			#if ASE_SRP_VERSION >=140007
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
			#endif
		

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"

			
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
           

			
            #if ASE_SRP_VERSION >=140009
			#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
			#endif
		

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			#if defined(UNITY_INSTANCING_ENABLED) && defined(_TERRAIN_INSTANCED_PERPIXEL_NORMAL)
				#define ENABLE_TERRAIN_PERPIXEL_NORMAL
			#endif

			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_TANGENT
			#define ASE_NEEDS_FRAG_WORLD_NORMAL
			#define ASE_NEEDS_FRAG_WORLD_BITANGENT
			#define ASE_NEEDS_FRAG_WORLD_VIEW_DIR


			#if defined(ASE_EARLY_Z_DEPTH_OPTIMIZE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float4 tangentOS : TANGENT;
				float4 texcoord : TEXCOORD0;
				float4 texcoord1 : TEXCOORD1;
				float4 texcoord2 : TEXCOORD2;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float4 clipPosV : TEXCOORD0;
				float4 lightmapUVOrVertexSH : TEXCOORD1;
				#if defined(ASE_FOG) || defined(_ADDITIONAL_LIGHTS_VERTEX)
					half4 fogFactorAndVertexLight : TEXCOORD2;
				#endif
				float4 tSpace0 : TEXCOORD3;
				float4 tSpace1 : TEXCOORD4;
				float4 tSpace2 : TEXCOORD5;
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
					float4 shadowCoord : TEXCOORD6;
				#endif
				#if defined(DYNAMICLIGHTMAP_ON)
					float2 dynamicLightmapUV : TEXCOORD7;
				#endif
				float4 ase_texcoord8 : TEXCOORD8;
				float4 ase_color : COLOR;
				float4 ase_texcoord9 : TEXCOORD9;
				float4 ase_texcoord10 : TEXCOORD10;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _Color1;
			float4 _Emissive3;
			float4 _Emissive2;
			float4 _Emissive1;
			half4 _PatternIntensity;
			half4 _Pattern;
			half4 _ColorOffset;
			float4 _Color3;
			float4 _Color2;
			float4 _Metalness;
			float4 _SmoothnessOffset;
			float4 _TrimSheet_ST;
			float4 _Color0;
			float3 _QualityMultipliers;
			float _VinylMaterial;
			float _VinylSelect;
			float _Tiling;
			float _Blend;
			float _ReferencePlane;
			float _ParallaxCropDistance;
			float _ParallaxMinDistance1;
			float _ParallaxMaxDistance1;
			float _AngleTolerance1;
			float _ParallaxOffset1;
			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			sampler2D _TrimSheet;
			float4 ParallaxOffsets;
			sampler2D _VinylTrimsheet;
			sampler2D _SurfaceTexture;
			sampler2D _Normal;


			inline float2 POM( sampler2D heightMap, float2 uvs, float2 dx, float2 dy, float3 normalWorld, float3 viewWorld, float3 viewDirTan, int minSamples, int maxSamples, int sidewallSteps, float parallax, float refPlane, float2 tilling, float2 curv, int index )
			{
				float3 result = 0;
				float stepIndex = 0;
				float numSteps = floor( lerp( (float)maxSamples, (float)minSamples, saturate( dot( normalWorld, viewWorld ) ) ) );
				float layerHeight = 1.0 / numSteps;
				float2 plane = parallax * ( viewDirTan.xy / viewDirTan.z );
				uvs.xy += refPlane * plane;
				float2 deltaTex = -plane * layerHeight;
				float2 prevTexOffset = 0;
				float prevRayZ = 1.0f;
				float prevHeight = 0.0f;
				float2 currTexOffset = deltaTex;
				float currRayZ = 1.0f - layerHeight;
				float currHeight = 0.0f;
				float intersection = 0;
				float2 finalTexOffset = 0;
				while ( stepIndex < numSteps + 1 )
				{
				 	currHeight = tex2Dgrad( heightMap, uvs + currTexOffset, dx, dy ).g;
				 	if ( currHeight > currRayZ )
				 	{
				 	 	stepIndex = numSteps + 1;
				 	}
				 	else
				 	{
				 	 	stepIndex++;
				 	 	prevTexOffset = currTexOffset;
				 	 	prevRayZ = currRayZ;
				 	 	prevHeight = currHeight;
				 	 	currTexOffset += deltaTex;
				 	 	currRayZ -= layerHeight;
				 	}
				}
				float sectionSteps = sidewallSteps;
				float sectionIndex = 0;
				float newZ = 0;
				float newHeight = 0;
				while ( sectionIndex < sectionSteps )
				{
				 	intersection = ( prevHeight - prevRayZ ) / ( prevHeight - currHeight + currRayZ - prevRayZ );
				 	finalTexOffset = prevTexOffset + intersection * deltaTex;
				 	newZ = prevRayZ - intersection * layerHeight;
				 	newHeight = tex2Dgrad( heightMap, uvs + finalTexOffset, dx, dy ).g;
				 	if ( newHeight > newZ )
				 	{
				 	 	currTexOffset = finalTexOffset;
				 	 	currHeight = newHeight;
				 	 	currRayZ = newZ;
				 	 	deltaTex = intersection * deltaTex;
				 	 	layerHeight = intersection * layerHeight;
				 	}
				 	else
				 	{
				 	 	prevTexOffset = finalTexOffset;
				 	 	prevHeight = newHeight;
				 	 	prevRayZ = newZ;
				 	 	deltaTex = ( 1 - intersection ) * deltaTex;
				 	 	layerHeight = ( 1 - intersection ) * layerHeight;
				 	}
				 	sectionIndex++;
				}
				return uvs.xy + finalTexOffset;
			}
			
			inline float VectorIndex1_g313( float4 Vector, float Index )
			{
				return Vector[Index];
			}
			
			float ToMaterial152( float In )
			{
				float arr[4]={0.25,.5,.75,1};
				return arr[In];
			}
			
			inline float FindIndex1_g302( float In )
			{
				return (In-0.009)*4;;
			}
			
			float4 MergeColors6_g321( float In, float4 C0, float4 C1, float4 C2, float4 C3 )
			{
				//return In.x*C0+In.y*C1+In.z*C2+In.w*C3;
				float4 arr[4]={C0,C1,C2,C3};
				return arr[In];
			}
			
			inline float VectorIndex1_g308( float4 Vector, float Index )
			{
				return Vector[Index];
			}
			
			inline float VectorIndex1_g304( float4 Vector, float Index )
			{
				return Vector[Index];
			}
			
			inline float VectorIndex1_g305( float4 Vector, float Index )
			{
				return Vector[Index];
			}
			
			float4 BlendVector1_g306( float In )
			{
				float4 _in=saturate(float4(In,In-0.25,In-0.5,In-0.75)*4);
				_in.xyz-=_in.yzw;
				return (_in);
			}
			
			float3 Merge3Colors96( float4 C0, float4 C1, float4 C2, float4 In )
			{
				return (In.x*C0+In.y*C1+In.z*C2).xyz;
				//float4 arr[4]={C0,C1,C2,C2};
				//return arr[In];
			}
			
			inline float VectorIndex1_g312( float4 Vector, float Index )
			{
				return Vector[Index];
			}
			
			inline float VectorIndex1_g311( float4 Vector, float Index )
			{
				return Vector[Index];
			}
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float vertexToFrag34 = distance( _WorldSpaceCameraPos , ase_positionWS );
				output.ase_texcoord8.z = vertexToFrag34;
				
				float4 C096 = _Emissive1;
				float4 C196 = _Emissive2;
				float4 C296 = _Emissive3;
				float In1_g306 = input.ase_color.r;
				float4 localBlendVector1_g306 = BlendVector1_g306( In1_g306 );
				float4 In96 = step( float4( 0.8,0.8,0.8,0 ) , localBlendVector1_g306 );
				float3 localMerge3Colors96 = Merge3Colors96( C096 , C196 , C296 , In96 );
				float3 vertexToFrag204 = localMerge3Colors96;
				output.ase_texcoord10.xyz = vertexToFrag204;
				
				output.ase_texcoord8.xy = input.texcoord.xy;
				output.ase_color = input.ase_color;
				output.ase_texcoord9.xy = input.texcoord2.xy;
				output.ase_texcoord9.zw = input.texcoord1.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord8.w = 0;
				output.ase_texcoord10.w = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif
				input.normalOS = input.normalOS;
				input.tangentOS = input.tangentOS;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );
				VertexNormalInputs normalInput = GetVertexNormalInputs( input.normalOS, input.tangentOS );

				output.tSpace0 = float4( normalInput.normalWS, vertexInput.positionWS.x );
				output.tSpace1 = float4( normalInput.tangentWS, vertexInput.positionWS.y );
				output.tSpace2 = float4( normalInput.bitangentWS, vertexInput.positionWS.z );

				#if defined(LIGHTMAP_ON)
					OUTPUT_LIGHTMAP_UV(input.texcoord1, unity_LightmapST, output.lightmapUVOrVertexSH.xy);
				#else
					OUTPUT_SH(normalInput.normalWS.xyz, output.lightmapUVOrVertexSH.xyz);
				#endif
				#if defined(DYNAMICLIGHTMAP_ON)
					output.dynamicLightmapUV.xy = input.texcoord2.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
				#endif

				#if defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
					output.lightmapUVOrVertexSH.zw = input.texcoord.xy;
					output.lightmapUVOrVertexSH.xy = input.texcoord.xy * unity_LightmapST.xy + unity_LightmapST.zw;
				#endif

				#if defined(ASE_FOG) || defined(_ADDITIONAL_LIGHTS_VERTEX)
					output.fogFactorAndVertexLight = 0;
					#if defined(ASE_FOG) && !defined(_FOG_FRAGMENT)
						output.fogFactorAndVertexLight.x = ComputeFogFactor(vertexInput.positionCS.z);
					#endif
					#ifdef _ADDITIONAL_LIGHTS_VERTEX
						half3 vertexLight = VertexLighting( vertexInput.positionWS, normalInput.normalWS );
						output.fogFactorAndVertexLight.yzw = vertexLight;
					#endif
				#endif

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
					output.shadowCoord = GetShadowCoord( vertexInput );
				#endif

				output.positionCS = vertexInput.positionCS;
				output.clipPosV = vertexInput.positionCS;
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				float3 normalOS : NORMAL;
				float4 tangentOS : TANGENT;
				float4 texcoord : TEXCOORD0;
				float4 texcoord1 : TEXCOORD1;
				float4 texcoord2 : TEXCOORD2;
				float4 ase_color : COLOR;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.positionOS = input.positionOS;
				output.normalOS = input.normalOS;
				output.tangentOS = input.tangentOS;
				output.texcoord = input.texcoord;
				output.texcoord1 = input.texcoord1;
				output.texcoord2 = input.texcoord2;
				output.ase_color = input.ase_color;
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				output.tangentOS = patch[0].tangentOS * bary.x + patch[1].tangentOS * bary.y + patch[2].tangentOS * bary.z;
				output.texcoord = patch[0].texcoord * bary.x + patch[1].texcoord * bary.y + patch[2].texcoord * bary.z;
				output.texcoord1 = patch[0].texcoord1 * bary.x + patch[1].texcoord1 * bary.y + patch[2].texcoord1 * bary.z;
				output.texcoord2 = patch[0].texcoord2 * bary.x + patch[1].texcoord2 * bary.y + patch[2].texcoord2 * bary.z;
				output.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			half4 frag ( PackedVaryings input
						#ifdef ASE_DEPTH_WRITE_ON
						,out float outputDepth : ASE_SV_DEPTH
						#endif
						#ifdef _WRITE_RENDERING_LAYERS
						, out float4 outRenderingLayers : SV_Target1
						#endif
						 ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

				#if defined(LOD_FADE_CROSSFADE)
					LODFadeCrossFade( input.positionCS );
				#endif

				#if defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
					float2 sampleCoords = (input.lightmapUVOrVertexSH.zw / _TerrainHeightmapRecipSize.zw + 0.5f) * _TerrainHeightmapRecipSize.xy;
					float3 WorldNormal = TransformObjectToWorldNormal(normalize(SAMPLE_TEXTURE2D(_TerrainNormalmapTexture, sampler_TerrainNormalmapTexture, sampleCoords).rgb * 2 - 1));
					float3 WorldTangent = -cross(GetObjectToWorldMatrix()._13_23_33, WorldNormal);
					float3 WorldBiTangent = cross(WorldNormal, -WorldTangent);
				#else
					float3 WorldNormal = normalize( input.tSpace0.xyz );
					float3 WorldTangent = input.tSpace1.xyz;
					float3 WorldBiTangent = input.tSpace2.xyz;
				#endif

				float3 WorldPosition = float3(input.tSpace0.w,input.tSpace1.w,input.tSpace2.w);
				float3 WorldViewDirection = GetWorldSpaceNormalizeViewDir( WorldPosition );
				float4 ShadowCoords = float4( 0, 0, 0, 0 );
				float4 ClipPos = input.clipPosV;
				float4 ScreenPos = ComputeScreenPos( input.clipPosV );

				float2 NormalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
					ShadowCoords = input.shadowCoord;
				#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
					ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
				#endif

				float2 texCoord137 = input.ase_texcoord8.xy * float2( 1,1 ) + float2( 0,0 );
				float2 TexUV136 = texCoord137;
				float3 tanToWorld0 = float3( WorldTangent.x, WorldBiTangent.x, WorldNormal.x );
				float3 tanToWorld1 = float3( WorldTangent.y, WorldBiTangent.y, WorldNormal.y );
				float3 tanToWorld2 = float3( WorldTangent.z, WorldBiTangent.z, WorldNormal.z );
				float3 ase_viewVectorTS =  tanToWorld0 * ( _WorldSpaceCameraPos.xyz - WorldPosition ).x + tanToWorld1 * ( _WorldSpaceCameraPos.xyz - WorldPosition ).y  + tanToWorld2 * ( _WorldSpaceCameraPos.xyz - WorldPosition ).z;
				float3 ase_viewDirTS = normalize( ase_viewVectorTS );
				float temp_output_1_0_g320 = 0.0;
				float vertexToFrag34 = input.ase_texcoord8.z;
				float CameraDistance35 = vertexToFrag34;
				float temp_output_1_0_g298 = _ParallaxMaxDistance1;
				float temp_output_44_0 = saturate( ( ( CameraDistance35 - temp_output_1_0_g298 ) / ( _ParallaxMinDistance1 - temp_output_1_0_g298 ) ) );
				float4 tex2DNode45 = tex2D( _TrimSheet, TexUV136 );
				float temp_output_51_0 = step( ( ParallaxOffsets.w + _ParallaxCropDistance ) , CameraDistance35 );
				float lerpResult53 = lerp( temp_output_44_0 , ( temp_output_44_0 * tex2DNode45.a ) , temp_output_51_0);
				float3 break56 = ( ( float3(8,16,4) + (ParallaxOffsets).xyz ) * lerpResult53 * _QualityMultipliers );
				float2 OffsetPOM194 = POM( _TrimSheet, TexUV136, ddx(TexUV136), ddy(TexUV136), WorldNormal, WorldViewDirection, ase_viewDirTS, (int)break56.x, (int)break56.y, (int)break56.z, ( _ParallaxOffset1 * ( ( (ase_viewDirTS).z - temp_output_1_0_g320 ) / ( _AngleTolerance1 - temp_output_1_0_g320 ) ) * lerpResult53 * lerpResult53 ), _ReferencePlane, _TrimSheet_ST.xy, float2(1,1), 0 );
				float2 ParallaxUV175 = OffsetPOM194;
				float4 tex2DNode197 = tex2D( _TrimSheet, ParallaxUV175 );
				float Trim_Occlusion191 = tex2DNode197.r;
				float Occlusion154 = Trim_Occlusion191;
				float2 texCoord57 = input.ase_texcoord9.xy * float2( 1,1 ) + float2( 0,0 );
				float4 Vector1_g313 = ( tex2D( _VinylTrimsheet, texCoord57 ) * saturate( _VinylSelect ) );
				float Index1_g313 = floor( ( _VinylSelect - 1.0 ) );
				float localVectorIndex1_g313 = VectorIndex1_g313( Vector1_g313 , Index1_g313 );
				float In152 = _VinylMaterial;
				float localToMaterial152 = ToMaterial152( In152 );
				float Vinyl_Material153 = ( step( 0.8 , localVectorIndex1_g313 ) * localToMaterial152 );
				float Trim_Material190 = tex2DNode197.b;
				float lerpResult199 = lerp( Vinyl_Material153 , Trim_Material190 , step( 0.2 , Trim_Material190 ));
				float lerpResult72 = lerp( input.ase_color.a , lerpResult199 , step( 0.1 , lerpResult199 ));
				float In1_g302 = lerpResult72;
				float localFindIndex1_g302 = FindIndex1_g302( In1_g302 );
				float temp_output_76_0 = localFindIndex1_g302;
				float In6_g321 = temp_output_76_0;
				float4 C06_g321 = _Color0;
				float4 C16_g321 = _Color1;
				float4 C26_g321 = _Color2;
				float4 C36_g321 = _Color3;
				float4 localMergeColors6_g321 = MergeColors6_g321( In6_g321 , C06_g321 , C16_g321 , C26_g321 , C36_g321 );
				float4 Color138 = ( Occlusion154 * localMergeColors6_g321 );
				float2 temp_cast_8 = (_Tiling).xx;
				float2 temp_cast_9 = (_Blend).xx;
				float2 texCoord7_g323 = input.ase_texcoord9.zw * temp_cast_8 + temp_cast_9;
				float4 tex2DNode10_g323 = tex2D( _SurfaceTexture, texCoord7_g323 );
				float4 break68 = tex2DNode10_g323;
				float Texture_R95 = break68.x;
				float4 Vector1_g308 = _ColorOffset;
				float Index1_g308 = temp_output_76_0;
				float localVectorIndex1_g308 = VectorIndex1_g308( Vector1_g308 , Index1_g308 );
				float ColorOffset94 = localVectorIndex1_g308;
				float lerpResult103 = lerp( Texture_R95 , 1.0 , ColorOffset94);
				float Pattern_1_Source70 = break68.y;
				float Pattern_2_Source71 = break68.z;
				float4 Vector1_g304 = _Pattern;
				float temp_output_11_0_g303 = temp_output_76_0;
				float Index1_g304 = temp_output_11_0_g303;
				float localVectorIndex1_g304 = VectorIndex1_g304( Vector1_g304 , Index1_g304 );
				float lerpResult5_g303 = lerp( Pattern_1_Source70 , Pattern_2_Source71 , localVectorIndex1_g304);
				float Pattern81 = lerpResult5_g303;
				float4 Vector1_g305 = _PatternIntensity;
				float Index1_g305 = temp_output_11_0_g303;
				float localVectorIndex1_g305 = VectorIndex1_g305( Vector1_g305 , Index1_g305 );
				float PatternIntensity82 = localVectorIndex1_g305;
				float lerpResult86 = lerp( 0.5 , Pattern81 , PatternIntensity82);
				float PatternColor98 = ( lerpResult86 * 2.0 );
				float4 FinalColor139 = saturate( ( Color138 * lerpResult103 * PatternColor98 ) );
				
				float Trim_Normal_Mask187 = tex2DNode197.a;
				float3 lerpResult109 = lerp( float3( 0,0,1 ) , UnpackNormalScale( tex2D( _Normal, ParallaxUV175 ), 1.0f ) , Trim_Normal_Mask187);
				float3 NormalMap192 = lerpResult109;
				
				float3 vertexToFrag204 = input.ase_texcoord10.xyz;
				float3 Glow100 = vertexToFrag204;
				
				float4 Vector1_g312 = _Metalness;
				float temp_output_7_0_g310 = temp_output_76_0;
				float Index1_g312 = temp_output_7_0_g310;
				float localVectorIndex1_g312 = VectorIndex1_g312( Vector1_g312 , Index1_g312 );
				float Metalness122 = localVectorIndex1_g312;
				
				float4 Vector1_g311 = _SmoothnessOffset;
				float Index1_g311 = temp_output_7_0_g310;
				float localVectorIndex1_g311 = VectorIndex1_g311( Vector1_g311 , Index1_g311 );
				float SmoothnessOffset115 = localVectorIndex1_g311;
				float Smoothness_Source111 = break68.w;
				float Smoothness121 = ( SmoothnessOffset115 + ( Color138.w * Smoothness_Source111 ) + ( 0.5 - lerpResult86 ) );
				

				float3 BaseColor = FinalColor139.xyz;
				float3 Normal = NormalMap192;
				float3 Emission = Glow100;
				float3 Specular = 0.5;
				float Metallic = Metalness122;
				float Smoothness = Smoothness121;
				float Occlusion = Occlusion154;
				float Alpha = 1;
				float AlphaClipThreshold = 0.5;
				float AlphaClipThresholdShadow = 0.5;
				float3 BakedGI = 0;
				float3 RefractionColor = 1;
				float RefractionIndex = 1;
				float3 Transmission = 1;
				float3 Translucency = 1;

				#ifdef ASE_DEPTH_WRITE_ON
					float DepthValue = input.positionCS.z;
				#endif

				#ifdef _CLEARCOAT
					float CoatMask = 0;
					float CoatSmoothness = 0;
				#endif

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				InputData inputData = (InputData)0;
				inputData.positionWS = WorldPosition;
				inputData.positionCS = input.positionCS;
				inputData.viewDirectionWS = WorldViewDirection;

				#ifdef _NORMALMAP
						#if _NORMAL_DROPOFF_TS
							inputData.normalWS = TransformTangentToWorld(Normal, half3x3(WorldTangent, WorldBiTangent, WorldNormal));
						#elif _NORMAL_DROPOFF_OS
							inputData.normalWS = TransformObjectToWorldNormal(Normal);
						#elif _NORMAL_DROPOFF_WS
							inputData.normalWS = Normal;
						#endif
					inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
				#else
					inputData.normalWS = WorldNormal;
				#endif

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
					inputData.shadowCoord = ShadowCoords;
				#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
					inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
				#else
					inputData.shadowCoord = float4(0, 0, 0, 0);
				#endif

				#ifdef ASE_FOG
					inputData.fogCoord = InitializeInputDataFog(float4(inputData.positionWS, 1.0), input.fogFactorAndVertexLight.x);
				#endif
				#ifdef _ADDITIONAL_LIGHTS_VERTEX
					inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
				#endif

				#if defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
					float3 SH = SampleSH(inputData.normalWS.xyz);
				#else
					float3 SH = input.lightmapUVOrVertexSH.xyz;
				#endif

				#if defined(DYNAMICLIGHTMAP_ON)
					inputData.bakedGI = SAMPLE_GI(input.lightmapUVOrVertexSH.xy, input.dynamicLightmapUV.xy, SH, inputData.normalWS);
				#else
					inputData.bakedGI = SAMPLE_GI(input.lightmapUVOrVertexSH.xy, SH, inputData.normalWS);
				#endif

				#ifdef ASE_BAKEDGI
					inputData.bakedGI = BakedGI;
				#endif

				inputData.normalizedScreenSpaceUV = NormalizedScreenSpaceUV;
				inputData.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUVOrVertexSH.xy);

				#if defined(DEBUG_DISPLAY)
					#if defined(DYNAMICLIGHTMAP_ON)
						inputData.dynamicLightmapUV = input.dynamicLightmapUV.xy;
					#endif
					#if defined(LIGHTMAP_ON)
						inputData.staticLightmapUV = input.lightmapUVOrVertexSH.xy;
					#else
						inputData.vertexSH = SH;
					#endif
				#endif

				SurfaceData surfaceData;
				surfaceData.albedo              = BaseColor;
				surfaceData.metallic            = saturate(Metallic);
				surfaceData.specular            = Specular;
				surfaceData.smoothness          = saturate(Smoothness),
				surfaceData.occlusion           = Occlusion,
				surfaceData.emission            = Emission,
				surfaceData.alpha               = saturate(Alpha);
				surfaceData.normalTS            = Normal;
				surfaceData.clearCoatMask       = 0;
				surfaceData.clearCoatSmoothness = 1;

				#ifdef _CLEARCOAT
					surfaceData.clearCoatMask       = saturate(CoatMask);
					surfaceData.clearCoatSmoothness = saturate(CoatSmoothness);
				#endif

				#ifdef _DBUFFER
					ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
				#endif

				#ifdef _ASE_LIGHTING_SIMPLE
					half4 color = UniversalFragmentBlinnPhong( inputData, surfaceData);
				#else
					half4 color = UniversalFragmentPBR( inputData, surfaceData);
				#endif

				#ifdef ASE_TRANSMISSION
				{
					float shadow = _TransmissionShadow;

					#define SUM_LIGHT_TRANSMISSION(Light)\
						float3 atten = Light.color * Light.distanceAttenuation;\
						atten = lerp( atten, atten * Light.shadowAttenuation, shadow );\
						half3 transmission = max( 0, -dot( inputData.normalWS, Light.direction ) ) * atten * Transmission;\
						color.rgb += BaseColor * transmission;

					SUM_LIGHT_TRANSMISSION( GetMainLight( inputData.shadowCoord ) );

					#if defined(_ADDITIONAL_LIGHTS)
						uint meshRenderingLayers = GetMeshRenderingLayer();
						uint pixelLightCount = GetAdditionalLightsCount();
						#if USE_FORWARD_PLUS
							for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
							{
								FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK

								Light light = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
								#ifdef _LIGHT_LAYERS
								if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
								#endif
								{
									SUM_LIGHT_TRANSMISSION( light );
								}
							}
						#endif
						LIGHT_LOOP_BEGIN( pixelLightCount )
							Light light = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
							#ifdef _LIGHT_LAYERS
							if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
							#endif
							{
								SUM_LIGHT_TRANSMISSION( light );
							}
						LIGHT_LOOP_END
					#endif
				}
				#endif

				#ifdef ASE_TRANSLUCENCY
				{
					float shadow = _TransShadow;
					float normal = _TransNormal;
					float scattering = _TransScattering;
					float direct = _TransDirect;
					float ambient = _TransAmbient;
					float strength = _TransStrength;

					#define SUM_LIGHT_TRANSLUCENCY(Light)\
						float3 atten = Light.color * Light.distanceAttenuation;\
						atten = lerp( atten, atten * Light.shadowAttenuation, shadow );\
						half3 lightDir = Light.direction + inputData.normalWS * normal;\
						half VdotL = pow( saturate( dot( inputData.viewDirectionWS, -lightDir ) ), scattering );\
						half3 translucency = atten * ( VdotL * direct + inputData.bakedGI * ambient ) * Translucency;\
						color.rgb += BaseColor * translucency * strength;

					SUM_LIGHT_TRANSLUCENCY( GetMainLight( inputData.shadowCoord ) );

					#if defined(_ADDITIONAL_LIGHTS)
						uint meshRenderingLayers = GetMeshRenderingLayer();
						uint pixelLightCount = GetAdditionalLightsCount();
						#if USE_FORWARD_PLUS
							for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
							{
								FORWARD_PLUS_SUBTRACTIVE_LIGHT_CHECK

								Light light = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
								#ifdef _LIGHT_LAYERS
								if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
								#endif
								{
									SUM_LIGHT_TRANSLUCENCY( light );
								}
							}
						#endif
						LIGHT_LOOP_BEGIN( pixelLightCount )
							Light light = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
							#ifdef _LIGHT_LAYERS
							if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
							#endif
							{
								SUM_LIGHT_TRANSLUCENCY( light );
							}
						LIGHT_LOOP_END
					#endif
				}
				#endif

				#ifdef ASE_REFRACTION
					float4 projScreenPos = ScreenPos / ScreenPos.w;
					float3 refractionOffset = ( RefractionIndex - 1.0 ) * mul( UNITY_MATRIX_V, float4( WorldNormal,0 ) ).xyz * ( 1.0 - dot( WorldNormal, WorldViewDirection ) );
					projScreenPos.xy += refractionOffset.xy;
					float3 refraction = SHADERGRAPH_SAMPLE_SCENE_COLOR( projScreenPos.xy ) * RefractionColor;
					color.rgb = lerp( refraction, color.rgb, color.a );
					color.a = 1;
				#endif

				#ifdef ASE_FINAL_COLOR_ALPHA_MULTIPLY
					color.rgb *= color.a;
				#endif

				#ifdef ASE_FOG
					#ifdef TERRAIN_SPLAT_ADDPASS
						color.rgb = MixFogColor(color.rgb, half3(0,0,0), inputData.fogCoord);
					#else
						color.rgb = MixFog(color.rgb, inputData.fogCoord);
					#endif
				#endif

				#ifdef ASE_DEPTH_WRITE_ON
					outputDepth = DepthValue;
				#endif

				#ifdef _WRITE_RENDERING_LAYERS
					uint renderingLayers = GetMeshRenderingLayer();
					outRenderingLayers = float4( EncodeMeshRenderingLayer( renderingLayers ), 0, 0, 0 );
				#endif

				return color;
			}
			ENDHLSL
		}

		
		Pass
		{
			
			Name "ShadowCaster"
			Tags { "LightMode"="ShadowCaster" }

			ZWrite On
			ZTest LEqual
			AlphaToMask Off
			ColorMask 0

			HLSLPROGRAM

			

			#define _NORMAL_DROPOFF_TS 1
			#pragma multi_compile_instancing
			#define ASE_FOG 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19801
			#define ASE_SRP_VERSION 140011


			

			#pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

			#pragma vertex vert
			#pragma fragment frag

			#if defined(_SPECULAR_SETUP) && defined(_ASE_LIGHTING_SIMPLE)
				#define _SPECULAR_COLOR 1
			#endif

			#define SHADERPASS SHADERPASS_SHADOWCASTER

			
            #if ASE_SRP_VERSION >=140007
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#endif
		

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"

			
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
           

			
            #if ASE_SRP_VERSION >=140009
			#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
			#endif
		

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			

			#if defined(ASE_EARLY_Z_DEPTH_OPTIMIZE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float4 clipPosV : TEXCOORD0;
				float3 positionWS : TEXCOORD1;
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					float4 shadowCoord : TEXCOORD2;
				#endif
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _Color1;
			float4 _Emissive3;
			float4 _Emissive2;
			float4 _Emissive1;
			half4 _PatternIntensity;
			half4 _Pattern;
			half4 _ColorOffset;
			float4 _Color3;
			float4 _Color2;
			float4 _Metalness;
			float4 _SmoothnessOffset;
			float4 _TrimSheet_ST;
			float4 _Color0;
			float3 _QualityMultipliers;
			float _VinylMaterial;
			float _VinylSelect;
			float _Tiling;
			float _Blend;
			float _ReferencePlane;
			float _ParallaxCropDistance;
			float _ParallaxMinDistance1;
			float _ParallaxMaxDistance1;
			float _AngleTolerance1;
			float _ParallaxOffset1;
			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			

			
			float3 _LightDirection;
			float3 _LightPosition;

			PackedVaryings VertexFunction( Attributes input )
			{
				PackedVaryings output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( output );

				

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;

				float3 positionWS = TransformObjectToWorld( input.positionOS.xyz );
				float3 normalWS = TransformObjectToWorldDir(input.normalOS);

				#if _CASTING_PUNCTUAL_LIGHT_SHADOW
					float3 lightDirectionWS = normalize(_LightPosition - positionWS);
				#else
					float3 lightDirectionWS = _LightDirection;
				#endif

				float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

				#if UNITY_REVERSED_Z
					positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
				#else
					positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
				#endif

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					VertexPositionInputs vertexInput = (VertexPositionInputs)0;
					vertexInput.positionWS = positionWS;
					vertexInput.positionCS = positionCS;
					output.shadowCoord = GetShadowCoord( vertexInput );
				#endif

				output.positionCS = positionCS;
				output.clipPosV = positionCS;
				output.positionWS = positionWS;
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				float3 normalOS : NORMAL;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.positionOS = input.positionOS;
				output.normalOS = input.normalOS;
				
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			half4 frag(	PackedVaryings input
						#ifdef ASE_DEPTH_WRITE_ON
						,out float outputDepth : ASE_SV_DEPTH
						#endif
						 ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( input );
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( input );

				float3 WorldPosition = input.positionWS;
				float4 ShadowCoords = float4( 0, 0, 0, 0 );
				float4 ClipPos = input.clipPosV;
				float4 ScreenPos = ComputeScreenPos( input.clipPosV );

				#if defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
						ShadowCoords = input.shadowCoord;
					#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
						ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
					#endif
				#endif

				

				float Alpha = 1;
				float AlphaClipThreshold = 0.5;
				float AlphaClipThresholdShadow = 0.5;

				#ifdef ASE_DEPTH_WRITE_ON
					float DepthValue = input.positionCS.z;
				#endif

				#ifdef _ALPHATEST_ON
					#ifdef _ALPHATEST_SHADOW_ON
						clip(Alpha - AlphaClipThresholdShadow);
					#else
						clip(Alpha - AlphaClipThreshold);
					#endif
				#endif

				#if defined(LOD_FADE_CROSSFADE)
					LODFadeCrossFade( input.positionCS );
				#endif

				#ifdef ASE_DEPTH_WRITE_ON
					outputDepth = DepthValue;
				#endif

				return 0;
			}
			ENDHLSL
		}

		
		Pass
		{
			
			Name "DepthOnly"
			Tags { "LightMode"="DepthOnly" }

			ZWrite On
			ColorMask R
			AlphaToMask Off

			HLSLPROGRAM

			

			#define _NORMAL_DROPOFF_TS 1
			#pragma multi_compile_instancing
			#define ASE_FOG 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19801
			#define ASE_SRP_VERSION 140011


			

			#pragma vertex vert
			#pragma fragment frag

			#if defined(_SPECULAR_SETUP) && defined(_ASE_LIGHTING_SIMPLE)
				#define _SPECULAR_COLOR 1
			#endif

			#define SHADERPASS SHADERPASS_DEPTHONLY

			
            #if ASE_SRP_VERSION >=140007
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#endif
		

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"

			
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
           

			
            #if ASE_SRP_VERSION >=140009
			#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
			#endif
		

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			

			#if defined(ASE_EARLY_Z_DEPTH_OPTIMIZE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float4 clipPosV : TEXCOORD0;
				float3 positionWS : TEXCOORD1;
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					float4 shadowCoord : TEXCOORD2;
				#endif
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _Color1;
			float4 _Emissive3;
			float4 _Emissive2;
			float4 _Emissive1;
			half4 _PatternIntensity;
			half4 _Pattern;
			half4 _ColorOffset;
			float4 _Color3;
			float4 _Color2;
			float4 _Metalness;
			float4 _SmoothnessOffset;
			float4 _TrimSheet_ST;
			float4 _Color0;
			float3 _QualityMultipliers;
			float _VinylMaterial;
			float _VinylSelect;
			float _Tiling;
			float _Blend;
			float _ReferencePlane;
			float _ParallaxCropDistance;
			float _ParallaxMinDistance1;
			float _ParallaxMaxDistance1;
			float _AngleTolerance1;
			float _ParallaxOffset1;
			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			

			
			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					output.shadowCoord = GetShadowCoord( vertexInput );
				#endif

				output.positionCS = vertexInput.positionCS;
				output.clipPosV = vertexInput.positionCS;
				output.positionWS = vertexInput.positionWS;
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				float3 normalOS : NORMAL;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.positionOS = input.positionOS;
				output.normalOS = input.normalOS;
				
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			half4 frag(	PackedVaryings input
						#ifdef ASE_DEPTH_WRITE_ON
						,out float outputDepth : ASE_SV_DEPTH
						#endif
						 ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( input );

				float3 WorldPosition = input.positionWS;
				float4 ShadowCoords = float4( 0, 0, 0, 0 );
				float4 ClipPos = input.clipPosV;
				float4 ScreenPos = ComputeScreenPos( input.clipPosV );

				#if defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
						ShadowCoords = input.shadowCoord;
					#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
						ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
					#endif
				#endif

				

				float Alpha = 1;
				float AlphaClipThreshold = 0.5;

				#ifdef ASE_DEPTH_WRITE_ON
					float DepthValue = input.positionCS.z;
				#endif

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				#if defined(LOD_FADE_CROSSFADE)
					LODFadeCrossFade( input.positionCS );
				#endif

				#ifdef ASE_DEPTH_WRITE_ON
					outputDepth = DepthValue;
				#endif

				return 0;
			}
			ENDHLSL
		}

		
		Pass
		{
			
			Name "Meta"
			Tags { "LightMode"="Meta" }

			Cull Off

			HLSLPROGRAM
			#define _NORMAL_DROPOFF_TS 1
			#define ASE_FOG 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19801
			#define ASE_SRP_VERSION 140011

			#pragma shader_feature EDITOR_VISUALIZATION

			#pragma vertex vert
			#pragma fragment frag

			#if defined(_SPECULAR_SETUP) && defined(_ASE_LIGHTING_SIMPLE)
				#define _SPECULAR_COLOR 1
			#endif

			#define SHADERPASS SHADERPASS_META

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"

			
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
           

			
            #if ASE_SRP_VERSION >=140009
			#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
			#endif
		

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#define ASE_NEEDS_VERT_NORMAL


			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float4 texcoord0 : TEXCOORD0;
				float4 texcoord1 : TEXCOORD1;
				float4 texcoord2 : TEXCOORD2;
				float4 ase_tangent : TANGENT;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				float4 positionCS : SV_POSITION;
				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
					float3 positionWS : TEXCOORD0;
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					float4 shadowCoord : TEXCOORD1;
				#endif
				#ifdef EDITOR_VISUALIZATION
					float4 VizUV : TEXCOORD2;
					float4 LightCoord : TEXCOORD3;
				#endif
				float4 ase_texcoord4 : TEXCOORD4;
				float4 ase_texcoord5 : TEXCOORD5;
				float4 ase_texcoord6 : TEXCOORD6;
				float4 ase_texcoord7 : TEXCOORD7;
				float4 ase_color : COLOR;
				float4 ase_texcoord8 : TEXCOORD8;
				float4 ase_texcoord9 : TEXCOORD9;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _Color1;
			float4 _Emissive3;
			float4 _Emissive2;
			float4 _Emissive1;
			half4 _PatternIntensity;
			half4 _Pattern;
			half4 _ColorOffset;
			float4 _Color3;
			float4 _Color2;
			float4 _Metalness;
			float4 _SmoothnessOffset;
			float4 _TrimSheet_ST;
			float4 _Color0;
			float3 _QualityMultipliers;
			float _VinylMaterial;
			float _VinylSelect;
			float _Tiling;
			float _Blend;
			float _ReferencePlane;
			float _ParallaxCropDistance;
			float _ParallaxMinDistance1;
			float _ParallaxMaxDistance1;
			float _AngleTolerance1;
			float _ParallaxOffset1;
			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			sampler2D _TrimSheet;
			float4 ParallaxOffsets;
			sampler2D _VinylTrimsheet;
			sampler2D _SurfaceTexture;


			inline float2 POM( sampler2D heightMap, float2 uvs, float2 dx, float2 dy, float3 normalWorld, float3 viewWorld, float3 viewDirTan, int minSamples, int maxSamples, int sidewallSteps, float parallax, float refPlane, float2 tilling, float2 curv, int index )
			{
				float3 result = 0;
				float stepIndex = 0;
				float numSteps = floor( lerp( (float)maxSamples, (float)minSamples, saturate( dot( normalWorld, viewWorld ) ) ) );
				float layerHeight = 1.0 / numSteps;
				float2 plane = parallax * ( viewDirTan.xy / viewDirTan.z );
				uvs.xy += refPlane * plane;
				float2 deltaTex = -plane * layerHeight;
				float2 prevTexOffset = 0;
				float prevRayZ = 1.0f;
				float prevHeight = 0.0f;
				float2 currTexOffset = deltaTex;
				float currRayZ = 1.0f - layerHeight;
				float currHeight = 0.0f;
				float intersection = 0;
				float2 finalTexOffset = 0;
				while ( stepIndex < numSteps + 1 )
				{
				 	currHeight = tex2Dgrad( heightMap, uvs + currTexOffset, dx, dy ).g;
				 	if ( currHeight > currRayZ )
				 	{
				 	 	stepIndex = numSteps + 1;
				 	}
				 	else
				 	{
				 	 	stepIndex++;
				 	 	prevTexOffset = currTexOffset;
				 	 	prevRayZ = currRayZ;
				 	 	prevHeight = currHeight;
				 	 	currTexOffset += deltaTex;
				 	 	currRayZ -= layerHeight;
				 	}
				}
				float sectionSteps = sidewallSteps;
				float sectionIndex = 0;
				float newZ = 0;
				float newHeight = 0;
				while ( sectionIndex < sectionSteps )
				{
				 	intersection = ( prevHeight - prevRayZ ) / ( prevHeight - currHeight + currRayZ - prevRayZ );
				 	finalTexOffset = prevTexOffset + intersection * deltaTex;
				 	newZ = prevRayZ - intersection * layerHeight;
				 	newHeight = tex2Dgrad( heightMap, uvs + finalTexOffset, dx, dy ).g;
				 	if ( newHeight > newZ )
				 	{
				 	 	currTexOffset = finalTexOffset;
				 	 	currHeight = newHeight;
				 	 	currRayZ = newZ;
				 	 	deltaTex = intersection * deltaTex;
				 	 	layerHeight = intersection * layerHeight;
				 	}
				 	else
				 	{
				 	 	prevTexOffset = finalTexOffset;
				 	 	prevHeight = newHeight;
				 	 	prevRayZ = newZ;
				 	 	deltaTex = ( 1 - intersection ) * deltaTex;
				 	 	layerHeight = ( 1 - intersection ) * layerHeight;
				 	}
				 	sectionIndex++;
				}
				return uvs.xy + finalTexOffset;
			}
			
			inline float VectorIndex1_g313( float4 Vector, float Index )
			{
				return Vector[Index];
			}
			
			float ToMaterial152( float In )
			{
				float arr[4]={0.25,.5,.75,1};
				return arr[In];
			}
			
			inline float FindIndex1_g302( float In )
			{
				return (In-0.009)*4;;
			}
			
			float4 MergeColors6_g321( float In, float4 C0, float4 C1, float4 C2, float4 C3 )
			{
				//return In.x*C0+In.y*C1+In.z*C2+In.w*C3;
				float4 arr[4]={C0,C1,C2,C3};
				return arr[In];
			}
			
			inline float VectorIndex1_g308( float4 Vector, float Index )
			{
				return Vector[Index];
			}
			
			inline float VectorIndex1_g304( float4 Vector, float Index )
			{
				return Vector[Index];
			}
			
			inline float VectorIndex1_g305( float4 Vector, float Index )
			{
				return Vector[Index];
			}
			
			float4 BlendVector1_g306( float In )
			{
				float4 _in=saturate(float4(In,In-0.25,In-0.5,In-0.75)*4);
				_in.xyz-=_in.yzw;
				return (_in);
			}
			
			float3 Merge3Colors96( float4 C0, float4 C1, float4 C2, float4 In )
			{
				return (In.x*C0+In.y*C1+In.z*C2).xyz;
				//float4 arr[4]={C0,C1,C2,C2};
				//return arr[In];
			}
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float3 ase_tangentWS = TransformObjectToWorldDir( input.ase_tangent.xyz );
				output.ase_texcoord5.xyz = ase_tangentWS;
				float3 ase_normalWS = TransformObjectToWorldNormal( input.normalOS );
				output.ase_texcoord6.xyz = ase_normalWS;
				float ase_tangentSign = input.ase_tangent.w * ( unity_WorldTransformParams.w >= 0.0 ? 1.0 : -1.0 );
				float3 ase_bitangentWS = cross( ase_normalWS, ase_tangentWS ) * ase_tangentSign;
				output.ase_texcoord7.xyz = ase_bitangentWS;
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float vertexToFrag34 = distance( _WorldSpaceCameraPos , ase_positionWS );
				output.ase_texcoord4.z = vertexToFrag34;
				
				float4 C096 = _Emissive1;
				float4 C196 = _Emissive2;
				float4 C296 = _Emissive3;
				float In1_g306 = input.ase_color.r;
				float4 localBlendVector1_g306 = BlendVector1_g306( In1_g306 );
				float4 In96 = step( float4( 0.8,0.8,0.8,0 ) , localBlendVector1_g306 );
				float3 localMerge3Colors96 = Merge3Colors96( C096 , C196 , C296 , In96 );
				float3 vertexToFrag204 = localMerge3Colors96;
				output.ase_texcoord9.xyz = vertexToFrag204;
				
				output.ase_texcoord4.xy = input.texcoord0.xy;
				output.ase_color = input.ase_color;
				output.ase_texcoord8.xy = input.texcoord2.xy;
				output.ase_texcoord8.zw = input.texcoord1.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord4.w = 0;
				output.ase_texcoord5.w = 0;
				output.ase_texcoord6.w = 0;
				output.ase_texcoord7.w = 0;
				output.ase_texcoord9.w = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;

				float3 positionWS = TransformObjectToWorld( input.positionOS.xyz );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
					output.positionWS = positionWS;
				#endif

				output.positionCS = MetaVertexPosition( input.positionOS, input.texcoord1.xy, input.texcoord1.xy, unity_LightmapST, unity_DynamicLightmapST );

				#ifdef EDITOR_VISUALIZATION
					float2 VizUV = 0;
					float4 LightCoord = 0;
					UnityEditorVizData(input.positionOS.xyz, input.texcoord0.xy, input.texcoord1.xy, input.texcoord2.xy, VizUV, LightCoord);
					output.VizUV = float4(VizUV, 0, 0);
					output.LightCoord = LightCoord;
				#endif

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					VertexPositionInputs vertexInput = (VertexPositionInputs)0;
					vertexInput.positionWS = positionWS;
					vertexInput.positionCS = output.positionCS;
					output.shadowCoord = GetShadowCoord( vertexInput );
				#endif

				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				float3 normalOS : NORMAL;
				float4 texcoord0 : TEXCOORD0;
				float4 texcoord1 : TEXCOORD1;
				float4 texcoord2 : TEXCOORD2;
				float4 ase_tangent : TANGENT;
				float4 ase_color : COLOR;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.positionOS = input.positionOS;
				output.normalOS = input.normalOS;
				output.texcoord0 = input.texcoord0;
				output.texcoord1 = input.texcoord1;
				output.texcoord2 = input.texcoord2;
				output.ase_tangent = input.ase_tangent;
				output.ase_color = input.ase_color;
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				output.texcoord0 = patch[0].texcoord0 * bary.x + patch[1].texcoord0 * bary.y + patch[2].texcoord0 * bary.z;
				output.texcoord1 = patch[0].texcoord1 * bary.x + patch[1].texcoord1 * bary.y + patch[2].texcoord1 * bary.z;
				output.texcoord2 = patch[0].texcoord2 * bary.x + patch[1].texcoord2 * bary.y + patch[2].texcoord2 * bary.z;
				output.ase_tangent = patch[0].ase_tangent * bary.x + patch[1].ase_tangent * bary.y + patch[2].ase_tangent * bary.z;
				output.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			half4 frag(PackedVaryings input  ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( input );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
					float3 WorldPosition = input.positionWS;
				#endif

				float4 ShadowCoords = float4( 0, 0, 0, 0 );

				#if defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
						ShadowCoords = input.shadowCoord;
					#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
						ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
					#endif
				#endif

				float2 texCoord137 = input.ase_texcoord4.xy * float2( 1,1 ) + float2( 0,0 );
				float2 TexUV136 = texCoord137;
				float3 ase_tangentWS = input.ase_texcoord5.xyz;
				float3 ase_normalWS = input.ase_texcoord6.xyz;
				float3 ase_bitangentWS = input.ase_texcoord7.xyz;
				float3 tanToWorld0 = float3( ase_tangentWS.x, ase_bitangentWS.x, ase_normalWS.x );
				float3 tanToWorld1 = float3( ase_tangentWS.y, ase_bitangentWS.y, ase_normalWS.y );
				float3 tanToWorld2 = float3( ase_tangentWS.z, ase_bitangentWS.z, ase_normalWS.z );
				float3 ase_viewVectorTS =  tanToWorld0 * ( _WorldSpaceCameraPos.xyz - WorldPosition ).x + tanToWorld1 * ( _WorldSpaceCameraPos.xyz - WorldPosition ).y  + tanToWorld2 * ( _WorldSpaceCameraPos.xyz - WorldPosition ).z;
				float3 ase_viewDirTS = normalize( ase_viewVectorTS );
				float temp_output_1_0_g320 = 0.0;
				float vertexToFrag34 = input.ase_texcoord4.z;
				float CameraDistance35 = vertexToFrag34;
				float temp_output_1_0_g298 = _ParallaxMaxDistance1;
				float temp_output_44_0 = saturate( ( ( CameraDistance35 - temp_output_1_0_g298 ) / ( _ParallaxMinDistance1 - temp_output_1_0_g298 ) ) );
				float4 tex2DNode45 = tex2D( _TrimSheet, TexUV136 );
				float temp_output_51_0 = step( ( ParallaxOffsets.w + _ParallaxCropDistance ) , CameraDistance35 );
				float lerpResult53 = lerp( temp_output_44_0 , ( temp_output_44_0 * tex2DNode45.a ) , temp_output_51_0);
				float3 break56 = ( ( float3(8,16,4) + (ParallaxOffsets).xyz ) * lerpResult53 * _QualityMultipliers );
				float3 ase_viewVectorWS = ( _WorldSpaceCameraPos.xyz - WorldPosition );
				float3 ase_viewDirWS = normalize( ase_viewVectorWS );
				float2 OffsetPOM194 = POM( _TrimSheet, TexUV136, ddx(TexUV136), ddy(TexUV136), ase_normalWS, ase_viewDirWS, ase_viewDirTS, (int)break56.x, (int)break56.y, (int)break56.z, ( _ParallaxOffset1 * ( ( (ase_viewDirTS).z - temp_output_1_0_g320 ) / ( _AngleTolerance1 - temp_output_1_0_g320 ) ) * lerpResult53 * lerpResult53 ), _ReferencePlane, _TrimSheet_ST.xy, float2(1,1), 0 );
				float2 ParallaxUV175 = OffsetPOM194;
				float4 tex2DNode197 = tex2D( _TrimSheet, ParallaxUV175 );
				float Trim_Occlusion191 = tex2DNode197.r;
				float Occlusion154 = Trim_Occlusion191;
				float2 texCoord57 = input.ase_texcoord8.xy * float2( 1,1 ) + float2( 0,0 );
				float4 Vector1_g313 = ( tex2D( _VinylTrimsheet, texCoord57 ) * saturate( _VinylSelect ) );
				float Index1_g313 = floor( ( _VinylSelect - 1.0 ) );
				float localVectorIndex1_g313 = VectorIndex1_g313( Vector1_g313 , Index1_g313 );
				float In152 = _VinylMaterial;
				float localToMaterial152 = ToMaterial152( In152 );
				float Vinyl_Material153 = ( step( 0.8 , localVectorIndex1_g313 ) * localToMaterial152 );
				float Trim_Material190 = tex2DNode197.b;
				float lerpResult199 = lerp( Vinyl_Material153 , Trim_Material190 , step( 0.2 , Trim_Material190 ));
				float lerpResult72 = lerp( input.ase_color.a , lerpResult199 , step( 0.1 , lerpResult199 ));
				float In1_g302 = lerpResult72;
				float localFindIndex1_g302 = FindIndex1_g302( In1_g302 );
				float temp_output_76_0 = localFindIndex1_g302;
				float In6_g321 = temp_output_76_0;
				float4 C06_g321 = _Color0;
				float4 C16_g321 = _Color1;
				float4 C26_g321 = _Color2;
				float4 C36_g321 = _Color3;
				float4 localMergeColors6_g321 = MergeColors6_g321( In6_g321 , C06_g321 , C16_g321 , C26_g321 , C36_g321 );
				float4 Color138 = ( Occlusion154 * localMergeColors6_g321 );
				float2 temp_cast_8 = (_Tiling).xx;
				float2 temp_cast_9 = (_Blend).xx;
				float2 texCoord7_g323 = input.ase_texcoord8.zw * temp_cast_8 + temp_cast_9;
				float4 tex2DNode10_g323 = tex2D( _SurfaceTexture, texCoord7_g323 );
				float4 break68 = tex2DNode10_g323;
				float Texture_R95 = break68.x;
				float4 Vector1_g308 = _ColorOffset;
				float Index1_g308 = temp_output_76_0;
				float localVectorIndex1_g308 = VectorIndex1_g308( Vector1_g308 , Index1_g308 );
				float ColorOffset94 = localVectorIndex1_g308;
				float lerpResult103 = lerp( Texture_R95 , 1.0 , ColorOffset94);
				float Pattern_1_Source70 = break68.y;
				float Pattern_2_Source71 = break68.z;
				float4 Vector1_g304 = _Pattern;
				float temp_output_11_0_g303 = temp_output_76_0;
				float Index1_g304 = temp_output_11_0_g303;
				float localVectorIndex1_g304 = VectorIndex1_g304( Vector1_g304 , Index1_g304 );
				float lerpResult5_g303 = lerp( Pattern_1_Source70 , Pattern_2_Source71 , localVectorIndex1_g304);
				float Pattern81 = lerpResult5_g303;
				float4 Vector1_g305 = _PatternIntensity;
				float Index1_g305 = temp_output_11_0_g303;
				float localVectorIndex1_g305 = VectorIndex1_g305( Vector1_g305 , Index1_g305 );
				float PatternIntensity82 = localVectorIndex1_g305;
				float lerpResult86 = lerp( 0.5 , Pattern81 , PatternIntensity82);
				float PatternColor98 = ( lerpResult86 * 2.0 );
				float4 FinalColor139 = saturate( ( Color138 * lerpResult103 * PatternColor98 ) );
				
				float3 vertexToFrag204 = input.ase_texcoord9.xyz;
				float3 Glow100 = vertexToFrag204;
				

				float3 BaseColor = FinalColor139.xyz;
				float3 Emission = Glow100;
				float Alpha = 1;
				float AlphaClipThreshold = 0.5;

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				MetaInput metaInput = (MetaInput)0;
				metaInput.Albedo = BaseColor;
				metaInput.Emission = Emission;
				#ifdef EDITOR_VISUALIZATION
					metaInput.VizUV = input.VizUV.xy;
					metaInput.LightCoord = input.LightCoord;
				#endif

				return UnityMetaFragment(metaInput);
			}
			ENDHLSL
		}

		
		Pass
		{
			
			Name "Universal2D"
			Tags { "LightMode"="Universal2D" }

			Blend One Zero, One Zero
			ZWrite On
			ZTest LEqual
			Offset 0 , 0
			ColorMask RGBA

			HLSLPROGRAM

			#define _NORMAL_DROPOFF_TS 1
			#define ASE_FOG 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19801
			#define ASE_SRP_VERSION 140011


			#pragma vertex vert
			#pragma fragment frag

			#if defined(_SPECULAR_SETUP) && defined(_ASE_LIGHTING_SIMPLE)
				#define _SPECULAR_COLOR 1
			#endif

			#define SHADERPASS SHADERPASS_2D

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"

			
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
           

			
            #if ASE_SRP_VERSION >=140009
			#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
			#endif
		

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#define ASE_NEEDS_VERT_NORMAL


			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_tangent : TANGENT;
				float4 ase_color : COLOR;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_texcoord1 : TEXCOORD1;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				float4 positionCS : SV_POSITION;
				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
					float3 positionWS : TEXCOORD0;
				#endif
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					float4 shadowCoord : TEXCOORD1;
				#endif
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_texcoord3 : TEXCOORD3;
				float4 ase_texcoord4 : TEXCOORD4;
				float4 ase_texcoord5 : TEXCOORD5;
				float4 ase_color : COLOR;
				float4 ase_texcoord6 : TEXCOORD6;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _Color1;
			float4 _Emissive3;
			float4 _Emissive2;
			float4 _Emissive1;
			half4 _PatternIntensity;
			half4 _Pattern;
			half4 _ColorOffset;
			float4 _Color3;
			float4 _Color2;
			float4 _Metalness;
			float4 _SmoothnessOffset;
			float4 _TrimSheet_ST;
			float4 _Color0;
			float3 _QualityMultipliers;
			float _VinylMaterial;
			float _VinylSelect;
			float _Tiling;
			float _Blend;
			float _ReferencePlane;
			float _ParallaxCropDistance;
			float _ParallaxMinDistance1;
			float _ParallaxMaxDistance1;
			float _AngleTolerance1;
			float _ParallaxOffset1;
			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			sampler2D _TrimSheet;
			float4 ParallaxOffsets;
			sampler2D _VinylTrimsheet;
			sampler2D _SurfaceTexture;


			inline float2 POM( sampler2D heightMap, float2 uvs, float2 dx, float2 dy, float3 normalWorld, float3 viewWorld, float3 viewDirTan, int minSamples, int maxSamples, int sidewallSteps, float parallax, float refPlane, float2 tilling, float2 curv, int index )
			{
				float3 result = 0;
				float stepIndex = 0;
				float numSteps = floor( lerp( (float)maxSamples, (float)minSamples, saturate( dot( normalWorld, viewWorld ) ) ) );
				float layerHeight = 1.0 / numSteps;
				float2 plane = parallax * ( viewDirTan.xy / viewDirTan.z );
				uvs.xy += refPlane * plane;
				float2 deltaTex = -plane * layerHeight;
				float2 prevTexOffset = 0;
				float prevRayZ = 1.0f;
				float prevHeight = 0.0f;
				float2 currTexOffset = deltaTex;
				float currRayZ = 1.0f - layerHeight;
				float currHeight = 0.0f;
				float intersection = 0;
				float2 finalTexOffset = 0;
				while ( stepIndex < numSteps + 1 )
				{
				 	currHeight = tex2Dgrad( heightMap, uvs + currTexOffset, dx, dy ).g;
				 	if ( currHeight > currRayZ )
				 	{
				 	 	stepIndex = numSteps + 1;
				 	}
				 	else
				 	{
				 	 	stepIndex++;
				 	 	prevTexOffset = currTexOffset;
				 	 	prevRayZ = currRayZ;
				 	 	prevHeight = currHeight;
				 	 	currTexOffset += deltaTex;
				 	 	currRayZ -= layerHeight;
				 	}
				}
				float sectionSteps = sidewallSteps;
				float sectionIndex = 0;
				float newZ = 0;
				float newHeight = 0;
				while ( sectionIndex < sectionSteps )
				{
				 	intersection = ( prevHeight - prevRayZ ) / ( prevHeight - currHeight + currRayZ - prevRayZ );
				 	finalTexOffset = prevTexOffset + intersection * deltaTex;
				 	newZ = prevRayZ - intersection * layerHeight;
				 	newHeight = tex2Dgrad( heightMap, uvs + finalTexOffset, dx, dy ).g;
				 	if ( newHeight > newZ )
				 	{
				 	 	currTexOffset = finalTexOffset;
				 	 	currHeight = newHeight;
				 	 	currRayZ = newZ;
				 	 	deltaTex = intersection * deltaTex;
				 	 	layerHeight = intersection * layerHeight;
				 	}
				 	else
				 	{
				 	 	prevTexOffset = finalTexOffset;
				 	 	prevHeight = newHeight;
				 	 	prevRayZ = newZ;
				 	 	deltaTex = ( 1 - intersection ) * deltaTex;
				 	 	layerHeight = ( 1 - intersection ) * layerHeight;
				 	}
				 	sectionIndex++;
				}
				return uvs.xy + finalTexOffset;
			}
			
			inline float VectorIndex1_g313( float4 Vector, float Index )
			{
				return Vector[Index];
			}
			
			float ToMaterial152( float In )
			{
				float arr[4]={0.25,.5,.75,1};
				return arr[In];
			}
			
			inline float FindIndex1_g302( float In )
			{
				return (In-0.009)*4;;
			}
			
			float4 MergeColors6_g321( float In, float4 C0, float4 C1, float4 C2, float4 C3 )
			{
				//return In.x*C0+In.y*C1+In.z*C2+In.w*C3;
				float4 arr[4]={C0,C1,C2,C3};
				return arr[In];
			}
			
			inline float VectorIndex1_g308( float4 Vector, float Index )
			{
				return Vector[Index];
			}
			
			inline float VectorIndex1_g304( float4 Vector, float Index )
			{
				return Vector[Index];
			}
			
			inline float VectorIndex1_g305( float4 Vector, float Index )
			{
				return Vector[Index];
			}
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID( input );
				UNITY_TRANSFER_INSTANCE_ID( input, output );
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( output );

				float3 ase_tangentWS = TransformObjectToWorldDir( input.ase_tangent.xyz );
				output.ase_texcoord3.xyz = ase_tangentWS;
				float3 ase_normalWS = TransformObjectToWorldNormal( input.normalOS );
				output.ase_texcoord4.xyz = ase_normalWS;
				float ase_tangentSign = input.ase_tangent.w * ( unity_WorldTransformParams.w >= 0.0 ? 1.0 : -1.0 );
				float3 ase_bitangentWS = cross( ase_normalWS, ase_tangentWS ) * ase_tangentSign;
				output.ase_texcoord5.xyz = ase_bitangentWS;
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float vertexToFrag34 = distance( _WorldSpaceCameraPos , ase_positionWS );
				output.ase_texcoord2.z = vertexToFrag34;
				
				output.ase_texcoord2.xy = input.ase_texcoord.xy;
				output.ase_color = input.ase_color;
				output.ase_texcoord6.xy = input.ase_texcoord2.xy;
				output.ase_texcoord6.zw = input.ase_texcoord1.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord2.w = 0;
				output.ase_texcoord3.w = 0;
				output.ase_texcoord4.w = 0;
				output.ase_texcoord5.w = 0;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
					output.positionWS = vertexInput.positionWS;
				#endif

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					output.shadowCoord = GetShadowCoord( vertexInput );
				#endif

				output.positionCS = vertexInput.positionCS;
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				float3 normalOS : NORMAL;
				float4 ase_texcoord : TEXCOORD0;
				float4 ase_tangent : TANGENT;
				float4 ase_color : COLOR;
				float4 ase_texcoord2 : TEXCOORD2;
				float4 ase_texcoord1 : TEXCOORD1;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.positionOS = input.positionOS;
				output.normalOS = input.normalOS;
				output.ase_texcoord = input.ase_texcoord;
				output.ase_tangent = input.ase_tangent;
				output.ase_color = input.ase_color;
				output.ase_texcoord2 = input.ase_texcoord2;
				output.ase_texcoord1 = input.ase_texcoord1;
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				output.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				output.ase_tangent = patch[0].ase_tangent * bary.x + patch[1].ase_tangent * bary.y + patch[2].ase_tangent * bary.z;
				output.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				output.ase_texcoord2 = patch[0].ase_texcoord2 * bary.x + patch[1].ase_texcoord2 * bary.y + patch[2].ase_texcoord2 * bary.z;
				output.ase_texcoord1 = patch[0].ase_texcoord1 * bary.x + patch[1].ase_texcoord1 * bary.y + patch[2].ase_texcoord1 * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			half4 frag(PackedVaryings input  ) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( input );
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( input );

				#if defined(ASE_NEEDS_FRAG_WORLD_POSITION)
					float3 WorldPosition = input.positionWS;
				#endif

				float4 ShadowCoords = float4( 0, 0, 0, 0 );

				#if defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
						ShadowCoords = input.shadowCoord;
					#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
						ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
					#endif
				#endif

				float2 texCoord137 = input.ase_texcoord2.xy * float2( 1,1 ) + float2( 0,0 );
				float2 TexUV136 = texCoord137;
				float3 ase_tangentWS = input.ase_texcoord3.xyz;
				float3 ase_normalWS = input.ase_texcoord4.xyz;
				float3 ase_bitangentWS = input.ase_texcoord5.xyz;
				float3 tanToWorld0 = float3( ase_tangentWS.x, ase_bitangentWS.x, ase_normalWS.x );
				float3 tanToWorld1 = float3( ase_tangentWS.y, ase_bitangentWS.y, ase_normalWS.y );
				float3 tanToWorld2 = float3( ase_tangentWS.z, ase_bitangentWS.z, ase_normalWS.z );
				float3 ase_viewVectorTS =  tanToWorld0 * ( _WorldSpaceCameraPos.xyz - WorldPosition ).x + tanToWorld1 * ( _WorldSpaceCameraPos.xyz - WorldPosition ).y  + tanToWorld2 * ( _WorldSpaceCameraPos.xyz - WorldPosition ).z;
				float3 ase_viewDirTS = normalize( ase_viewVectorTS );
				float temp_output_1_0_g320 = 0.0;
				float vertexToFrag34 = input.ase_texcoord2.z;
				float CameraDistance35 = vertexToFrag34;
				float temp_output_1_0_g298 = _ParallaxMaxDistance1;
				float temp_output_44_0 = saturate( ( ( CameraDistance35 - temp_output_1_0_g298 ) / ( _ParallaxMinDistance1 - temp_output_1_0_g298 ) ) );
				float4 tex2DNode45 = tex2D( _TrimSheet, TexUV136 );
				float temp_output_51_0 = step( ( ParallaxOffsets.w + _ParallaxCropDistance ) , CameraDistance35 );
				float lerpResult53 = lerp( temp_output_44_0 , ( temp_output_44_0 * tex2DNode45.a ) , temp_output_51_0);
				float3 break56 = ( ( float3(8,16,4) + (ParallaxOffsets).xyz ) * lerpResult53 * _QualityMultipliers );
				float3 ase_viewVectorWS = ( _WorldSpaceCameraPos.xyz - WorldPosition );
				float3 ase_viewDirWS = normalize( ase_viewVectorWS );
				float2 OffsetPOM194 = POM( _TrimSheet, TexUV136, ddx(TexUV136), ddy(TexUV136), ase_normalWS, ase_viewDirWS, ase_viewDirTS, (int)break56.x, (int)break56.y, (int)break56.z, ( _ParallaxOffset1 * ( ( (ase_viewDirTS).z - temp_output_1_0_g320 ) / ( _AngleTolerance1 - temp_output_1_0_g320 ) ) * lerpResult53 * lerpResult53 ), _ReferencePlane, _TrimSheet_ST.xy, float2(1,1), 0 );
				float2 ParallaxUV175 = OffsetPOM194;
				float4 tex2DNode197 = tex2D( _TrimSheet, ParallaxUV175 );
				float Trim_Occlusion191 = tex2DNode197.r;
				float Occlusion154 = Trim_Occlusion191;
				float2 texCoord57 = input.ase_texcoord6.xy * float2( 1,1 ) + float2( 0,0 );
				float4 Vector1_g313 = ( tex2D( _VinylTrimsheet, texCoord57 ) * saturate( _VinylSelect ) );
				float Index1_g313 = floor( ( _VinylSelect - 1.0 ) );
				float localVectorIndex1_g313 = VectorIndex1_g313( Vector1_g313 , Index1_g313 );
				float In152 = _VinylMaterial;
				float localToMaterial152 = ToMaterial152( In152 );
				float Vinyl_Material153 = ( step( 0.8 , localVectorIndex1_g313 ) * localToMaterial152 );
				float Trim_Material190 = tex2DNode197.b;
				float lerpResult199 = lerp( Vinyl_Material153 , Trim_Material190 , step( 0.2 , Trim_Material190 ));
				float lerpResult72 = lerp( input.ase_color.a , lerpResult199 , step( 0.1 , lerpResult199 ));
				float In1_g302 = lerpResult72;
				float localFindIndex1_g302 = FindIndex1_g302( In1_g302 );
				float temp_output_76_0 = localFindIndex1_g302;
				float In6_g321 = temp_output_76_0;
				float4 C06_g321 = _Color0;
				float4 C16_g321 = _Color1;
				float4 C26_g321 = _Color2;
				float4 C36_g321 = _Color3;
				float4 localMergeColors6_g321 = MergeColors6_g321( In6_g321 , C06_g321 , C16_g321 , C26_g321 , C36_g321 );
				float4 Color138 = ( Occlusion154 * localMergeColors6_g321 );
				float2 temp_cast_8 = (_Tiling).xx;
				float2 temp_cast_9 = (_Blend).xx;
				float2 texCoord7_g323 = input.ase_texcoord6.zw * temp_cast_8 + temp_cast_9;
				float4 tex2DNode10_g323 = tex2D( _SurfaceTexture, texCoord7_g323 );
				float4 break68 = tex2DNode10_g323;
				float Texture_R95 = break68.x;
				float4 Vector1_g308 = _ColorOffset;
				float Index1_g308 = temp_output_76_0;
				float localVectorIndex1_g308 = VectorIndex1_g308( Vector1_g308 , Index1_g308 );
				float ColorOffset94 = localVectorIndex1_g308;
				float lerpResult103 = lerp( Texture_R95 , 1.0 , ColorOffset94);
				float Pattern_1_Source70 = break68.y;
				float Pattern_2_Source71 = break68.z;
				float4 Vector1_g304 = _Pattern;
				float temp_output_11_0_g303 = temp_output_76_0;
				float Index1_g304 = temp_output_11_0_g303;
				float localVectorIndex1_g304 = VectorIndex1_g304( Vector1_g304 , Index1_g304 );
				float lerpResult5_g303 = lerp( Pattern_1_Source70 , Pattern_2_Source71 , localVectorIndex1_g304);
				float Pattern81 = lerpResult5_g303;
				float4 Vector1_g305 = _PatternIntensity;
				float Index1_g305 = temp_output_11_0_g303;
				float localVectorIndex1_g305 = VectorIndex1_g305( Vector1_g305 , Index1_g305 );
				float PatternIntensity82 = localVectorIndex1_g305;
				float lerpResult86 = lerp( 0.5 , Pattern81 , PatternIntensity82);
				float PatternColor98 = ( lerpResult86 * 2.0 );
				float4 FinalColor139 = saturate( ( Color138 * lerpResult103 * PatternColor98 ) );
				

				float3 BaseColor = FinalColor139.xyz;
				float Alpha = 1;
				float AlphaClipThreshold = 0.5;

				half4 color = half4(BaseColor, Alpha );

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				return color;
			}
			ENDHLSL
		}

		
		Pass
		{
			
			Name "DepthNormals"
			Tags { "LightMode"="DepthNormals" }

			ZWrite On
			Blend One Zero
			ZTest LEqual
			ZWrite On

			HLSLPROGRAM

			

			

			#define _NORMAL_DROPOFF_TS 1
			#pragma multi_compile_instancing
			#define ASE_FOG 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19801
			#define ASE_SRP_VERSION 140011


			

			

			#pragma vertex vert
			#pragma fragment frag

			#if defined(_SPECULAR_SETUP) && defined(_ASE_LIGHTING_SIMPLE)
				#define _SPECULAR_COLOR 1
			#endif

			#define SHADERPASS SHADERPASS_DEPTHNORMALSONLY
			//#define SHADERPASS SHADERPASS_DEPTHNORMALS

			
            #if ASE_SRP_VERSION >=140007
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#endif
		

			
			#if ASE_SRP_VERSION >=140007
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
			#endif
		

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"

			
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
           

			
            #if ASE_SRP_VERSION >=140009
			#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
			#endif
		

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_TANGENT
			#define ASE_NEEDS_FRAG_WORLD_NORMAL
			#define ASE_NEEDS_VERT_NORMAL
			#define ASE_NEEDS_VERT_TANGENT


			#if defined(ASE_EARLY_Z_DEPTH_OPTIMIZE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float4 tangentOS : TANGENT;
				float4 ase_texcoord : TEXCOORD0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float4 clipPosV : TEXCOORD0;
				float3 positionWS : TEXCOORD1;
				float3 normalWS : TEXCOORD2;
				float4 tangentWS : TEXCOORD3;
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					float4 shadowCoord : TEXCOORD4;
				#endif
				float4 ase_texcoord5 : TEXCOORD5;
				float4 ase_texcoord6 : TEXCOORD6;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _Color1;
			float4 _Emissive3;
			float4 _Emissive2;
			float4 _Emissive1;
			half4 _PatternIntensity;
			half4 _Pattern;
			half4 _ColorOffset;
			float4 _Color3;
			float4 _Color2;
			float4 _Metalness;
			float4 _SmoothnessOffset;
			float4 _TrimSheet_ST;
			float4 _Color0;
			float3 _QualityMultipliers;
			float _VinylMaterial;
			float _VinylSelect;
			float _Tiling;
			float _Blend;
			float _ReferencePlane;
			float _ParallaxCropDistance;
			float _ParallaxMinDistance1;
			float _ParallaxMaxDistance1;
			float _AngleTolerance1;
			float _ParallaxOffset1;
			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			sampler2D _Normal;
			sampler2D _TrimSheet;
			float4 ParallaxOffsets;


			inline float2 POM( sampler2D heightMap, float2 uvs, float2 dx, float2 dy, float3 normalWorld, float3 viewWorld, float3 viewDirTan, int minSamples, int maxSamples, int sidewallSteps, float parallax, float refPlane, float2 tilling, float2 curv, int index )
			{
				float3 result = 0;
				float stepIndex = 0;
				float numSteps = floor( lerp( (float)maxSamples, (float)minSamples, saturate( dot( normalWorld, viewWorld ) ) ) );
				float layerHeight = 1.0 / numSteps;
				float2 plane = parallax * ( viewDirTan.xy / viewDirTan.z );
				uvs.xy += refPlane * plane;
				float2 deltaTex = -plane * layerHeight;
				float2 prevTexOffset = 0;
				float prevRayZ = 1.0f;
				float prevHeight = 0.0f;
				float2 currTexOffset = deltaTex;
				float currRayZ = 1.0f - layerHeight;
				float currHeight = 0.0f;
				float intersection = 0;
				float2 finalTexOffset = 0;
				while ( stepIndex < numSteps + 1 )
				{
				 	currHeight = tex2Dgrad( heightMap, uvs + currTexOffset, dx, dy ).g;
				 	if ( currHeight > currRayZ )
				 	{
				 	 	stepIndex = numSteps + 1;
				 	}
				 	else
				 	{
				 	 	stepIndex++;
				 	 	prevTexOffset = currTexOffset;
				 	 	prevRayZ = currRayZ;
				 	 	prevHeight = currHeight;
				 	 	currTexOffset += deltaTex;
				 	 	currRayZ -= layerHeight;
				 	}
				}
				float sectionSteps = sidewallSteps;
				float sectionIndex = 0;
				float newZ = 0;
				float newHeight = 0;
				while ( sectionIndex < sectionSteps )
				{
				 	intersection = ( prevHeight - prevRayZ ) / ( prevHeight - currHeight + currRayZ - prevRayZ );
				 	finalTexOffset = prevTexOffset + intersection * deltaTex;
				 	newZ = prevRayZ - intersection * layerHeight;
				 	newHeight = tex2Dgrad( heightMap, uvs + finalTexOffset, dx, dy ).g;
				 	if ( newHeight > newZ )
				 	{
				 	 	currTexOffset = finalTexOffset;
				 	 	currHeight = newHeight;
				 	 	currRayZ = newZ;
				 	 	deltaTex = intersection * deltaTex;
				 	 	layerHeight = intersection * layerHeight;
				 	}
				 	else
				 	{
				 	 	prevTexOffset = finalTexOffset;
				 	 	prevHeight = newHeight;
				 	 	prevRayZ = newZ;
				 	 	deltaTex = ( 1 - intersection ) * deltaTex;
				 	 	layerHeight = ( 1 - intersection ) * layerHeight;
				 	}
				 	sectionIndex++;
				}
				return uvs.xy + finalTexOffset;
			}
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float3 ase_normalWS = TransformObjectToWorldNormal( input.normalOS );
				float3 ase_tangentWS = TransformObjectToWorldDir( input.tangentOS.xyz );
				float ase_tangentSign = input.tangentOS.w * ( unity_WorldTransformParams.w >= 0.0 ? 1.0 : -1.0 );
				float3 ase_bitangentWS = cross( ase_normalWS, ase_tangentWS ) * ase_tangentSign;
				output.ase_texcoord6.xyz = ase_bitangentWS;
				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float vertexToFrag34 = distance( _WorldSpaceCameraPos , ase_positionWS );
				output.ase_texcoord5.z = vertexToFrag34;
				
				output.ase_texcoord5.xy = input.ase_texcoord.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord5.w = 0;
				output.ase_texcoord6.w = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;
				input.tangentOS = input.tangentOS;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );

				float3 normalWS = TransformObjectToWorldNormal( input.normalOS );
				float4 tangentWS = float4( TransformObjectToWorldDir( input.tangentOS.xyz ), input.tangentOS.w );

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR) && defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					output.shadowCoord = GetShadowCoord( vertexInput );
				#endif

				output.positionCS = vertexInput.positionCS;
				output.clipPosV = vertexInput.positionCS;
				output.positionWS = vertexInput.positionWS;
				output.normalWS = normalWS;
				output.tangentWS = tangentWS;
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				float3 normalOS : NORMAL;
				float4 tangentOS : TANGENT;
				float4 ase_texcoord : TEXCOORD0;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.positionOS = input.positionOS;
				output.normalOS = input.normalOS;
				output.tangentOS = input.tangentOS;
				output.ase_texcoord = input.ase_texcoord;
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				output.tangentOS = patch[0].tangentOS * bary.x + patch[1].tangentOS * bary.y + patch[2].tangentOS * bary.z;
				output.ase_texcoord = patch[0].ase_texcoord * bary.x + patch[1].ase_texcoord * bary.y + patch[2].ase_texcoord * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			void frag(	PackedVaryings input
						, out half4 outNormalWS : SV_Target0
						#ifdef ASE_DEPTH_WRITE_ON
						,out float outputDepth : ASE_SV_DEPTH
						#endif
						#ifdef _WRITE_RENDERING_LAYERS
						, out float4 outRenderingLayers : SV_Target1
						#endif
						 )
			{
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( input );

				float4 ShadowCoords = float4( 0, 0, 0, 0 );
				float3 WorldNormal = input.normalWS;
				float4 WorldTangent = input.tangentWS;
				float3 WorldPosition = input.positionWS;
				float4 ClipPos = input.clipPosV;
				float4 ScreenPos = ComputeScreenPos( input.clipPosV );

				#if defined(ASE_NEEDS_FRAG_SHADOWCOORDS)
					#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
						ShadowCoords = input.shadowCoord;
					#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
						ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
					#endif
				#endif

				float2 texCoord137 = input.ase_texcoord5.xy * float2( 1,1 ) + float2( 0,0 );
				float2 TexUV136 = texCoord137;
				float3 ase_bitangentWS = input.ase_texcoord6.xyz;
				float3 tanToWorld0 = float3( WorldTangent.xyz.x, ase_bitangentWS.x, WorldNormal.x );
				float3 tanToWorld1 = float3( WorldTangent.xyz.y, ase_bitangentWS.y, WorldNormal.y );
				float3 tanToWorld2 = float3( WorldTangent.xyz.z, ase_bitangentWS.z, WorldNormal.z );
				float3 ase_viewVectorTS =  tanToWorld0 * ( _WorldSpaceCameraPos.xyz - WorldPosition ).x + tanToWorld1 * ( _WorldSpaceCameraPos.xyz - WorldPosition ).y  + tanToWorld2 * ( _WorldSpaceCameraPos.xyz - WorldPosition ).z;
				float3 ase_viewDirTS = normalize( ase_viewVectorTS );
				float temp_output_1_0_g320 = 0.0;
				float vertexToFrag34 = input.ase_texcoord5.z;
				float CameraDistance35 = vertexToFrag34;
				float temp_output_1_0_g298 = _ParallaxMaxDistance1;
				float temp_output_44_0 = saturate( ( ( CameraDistance35 - temp_output_1_0_g298 ) / ( _ParallaxMinDistance1 - temp_output_1_0_g298 ) ) );
				float4 tex2DNode45 = tex2D( _TrimSheet, TexUV136 );
				float temp_output_51_0 = step( ( ParallaxOffsets.w + _ParallaxCropDistance ) , CameraDistance35 );
				float lerpResult53 = lerp( temp_output_44_0 , ( temp_output_44_0 * tex2DNode45.a ) , temp_output_51_0);
				float3 break56 = ( ( float3(8,16,4) + (ParallaxOffsets).xyz ) * lerpResult53 * _QualityMultipliers );
				float3 ase_viewVectorWS = ( _WorldSpaceCameraPos.xyz - WorldPosition );
				float3 ase_viewDirWS = normalize( ase_viewVectorWS );
				float2 OffsetPOM194 = POM( _TrimSheet, TexUV136, ddx(TexUV136), ddy(TexUV136), WorldNormal, ase_viewDirWS, ase_viewDirTS, (int)break56.x, (int)break56.y, (int)break56.z, ( _ParallaxOffset1 * ( ( (ase_viewDirTS).z - temp_output_1_0_g320 ) / ( _AngleTolerance1 - temp_output_1_0_g320 ) ) * lerpResult53 * lerpResult53 ), _ReferencePlane, _TrimSheet_ST.xy, float2(1,1), 0 );
				float2 ParallaxUV175 = OffsetPOM194;
				float4 tex2DNode197 = tex2D( _TrimSheet, ParallaxUV175 );
				float Trim_Normal_Mask187 = tex2DNode197.a;
				float3 lerpResult109 = lerp( float3( 0,0,1 ) , UnpackNormalScale( tex2D( _Normal, ParallaxUV175 ), 1.0f ) , Trim_Normal_Mask187);
				float3 NormalMap192 = lerpResult109;
				

				float3 Normal = NormalMap192;
				float Alpha = 1;
				float AlphaClipThreshold = 0.5;

				#ifdef ASE_DEPTH_WRITE_ON
					float DepthValue = input.positionCS.z;
				#endif

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				#if defined(LOD_FADE_CROSSFADE)
					LODFadeCrossFade( input.positionCS );
				#endif

				#ifdef ASE_DEPTH_WRITE_ON
					outputDepth = DepthValue;
				#endif

				#if defined(_GBUFFER_NORMALS_OCT)
					float2 octNormalWS = PackNormalOctQuadEncode(WorldNormal);
					float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
					half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);
					outNormalWS = half4(packedNormalWS, 0.0);
				#else
					#if defined(_NORMALMAP)
						#if _NORMAL_DROPOFF_TS
							float crossSign = (WorldTangent.w > 0.0 ? 1.0 : -1.0) * GetOddNegativeScale();
							float3 bitangent = crossSign * cross(WorldNormal.xyz, WorldTangent.xyz);
							float3 normalWS = TransformTangentToWorld(Normal, half3x3(WorldTangent.xyz, bitangent, WorldNormal.xyz));
						#elif _NORMAL_DROPOFF_OS
							float3 normalWS = TransformObjectToWorldNormal(Normal);
						#elif _NORMAL_DROPOFF_WS
							float3 normalWS = Normal;
						#endif
					#else
						float3 normalWS = WorldNormal;
					#endif
					outNormalWS = half4(NormalizeNormalPerPixel(normalWS), 0.0);
				#endif

				#ifdef _WRITE_RENDERING_LAYERS
					uint renderingLayers = GetMeshRenderingLayer();
					outRenderingLayers = float4(EncodeMeshRenderingLayer(renderingLayers), 0, 0, 0);
				#endif
			}
			ENDHLSL
		}

		
		Pass
		{
			
			Name "GBuffer"
			Tags { "LightMode"="UniversalGBuffer" }

			Blend One Zero, One Zero
			ZWrite On
			ZTest LEqual
			Offset 0 , 0
			ColorMask RGBA
			

			HLSLPROGRAM

			

			#define _NORMAL_DROPOFF_TS 1
			#pragma shader_feature_local _RECEIVE_SHADOWS_OFF
			#pragma multi_compile_instancing
			#pragma instancing_options renderinglayer
			#pragma multi_compile_fog
			#define ASE_FOG 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19801
			#define ASE_SRP_VERSION 140011


			

			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
			#pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION

			

			
			#pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
           

			#pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
			#pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
			#pragma multi_compile_fragment _ _RENDER_PASS_ENABLED

			

			#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
			#pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
			#pragma multi_compile _ SHADOWS_SHADOWMASK
			#pragma multi_compile _ DIRLIGHTMAP_COMBINED
			#pragma multi_compile _ LIGHTMAP_ON
			#pragma multi_compile _ DYNAMICLIGHTMAP_ON

			#pragma vertex vert
			#pragma fragment frag

			#if defined(_SPECULAR_SETUP) && defined(_ASE_LIGHTING_SIMPLE)
				#define _SPECULAR_COLOR 1
			#endif

			#define SHADERPASS SHADERPASS_GBUFFER

			
            #if ASE_SRP_VERSION >=140007
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#endif
		

			
			#if ASE_SRP_VERSION >=140007
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
			#endif
		

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"

			
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
           

			
            #if ASE_SRP_VERSION >=140009
			#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
			#endif
		

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			#if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
            #endif

			#if defined(UNITY_INSTANCING_ENABLED) && defined(_TERRAIN_INSTANCED_PERPIXEL_NORMAL)
				#define ENABLE_TERRAIN_PERPIXEL_NORMAL
			#endif

			#define ASE_NEEDS_FRAG_WORLD_POSITION
			#define ASE_NEEDS_FRAG_WORLD_TANGENT
			#define ASE_NEEDS_FRAG_WORLD_NORMAL
			#define ASE_NEEDS_FRAG_WORLD_BITANGENT
			#define ASE_NEEDS_FRAG_WORLD_VIEW_DIR


			#if defined(ASE_EARLY_Z_DEPTH_OPTIMIZE) && (SHADER_TARGET >= 45)
				#define ASE_SV_DEPTH SV_DepthLessEqual
				#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
			#else
				#define ASE_SV_DEPTH SV_Depth
				#define ASE_SV_POSITION_QUALIFIERS
			#endif

			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				float4 tangentOS : TANGENT;
				float4 texcoord : TEXCOORD0;
				float4 texcoord1 : TEXCOORD1;
				float4 texcoord2 : TEXCOORD2;
				float4 ase_color : COLOR;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				ASE_SV_POSITION_QUALIFIERS float4 positionCS : SV_POSITION;
				float4 clipPosV : TEXCOORD0;
				float4 lightmapUVOrVertexSH : TEXCOORD1;
				#if defined(ASE_FOG) || defined(_ADDITIONAL_LIGHTS_VERTEX)
					half4 fogFactorAndVertexLight : TEXCOORD2;
				#endif
				float4 tSpace0 : TEXCOORD3;
				float4 tSpace1 : TEXCOORD4;
				float4 tSpace2 : TEXCOORD5;
				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
				float4 shadowCoord : TEXCOORD6;
				#endif
				#if defined(DYNAMICLIGHTMAP_ON)
				float2 dynamicLightmapUV : TEXCOORD7;
				#endif
				float4 ase_texcoord8 : TEXCOORD8;
				float4 ase_color : COLOR;
				float4 ase_texcoord9 : TEXCOORD9;
				float4 ase_texcoord10 : TEXCOORD10;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _Color1;
			float4 _Emissive3;
			float4 _Emissive2;
			float4 _Emissive1;
			half4 _PatternIntensity;
			half4 _Pattern;
			half4 _ColorOffset;
			float4 _Color3;
			float4 _Color2;
			float4 _Metalness;
			float4 _SmoothnessOffset;
			float4 _TrimSheet_ST;
			float4 _Color0;
			float3 _QualityMultipliers;
			float _VinylMaterial;
			float _VinylSelect;
			float _Tiling;
			float _Blend;
			float _ReferencePlane;
			float _ParallaxCropDistance;
			float _ParallaxMinDistance1;
			float _ParallaxMaxDistance1;
			float _AngleTolerance1;
			float _ParallaxOffset1;
			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			sampler2D _TrimSheet;
			float4 ParallaxOffsets;
			sampler2D _VinylTrimsheet;
			sampler2D _SurfaceTexture;
			sampler2D _Normal;


			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"

			inline float2 POM( sampler2D heightMap, float2 uvs, float2 dx, float2 dy, float3 normalWorld, float3 viewWorld, float3 viewDirTan, int minSamples, int maxSamples, int sidewallSteps, float parallax, float refPlane, float2 tilling, float2 curv, int index )
			{
				float3 result = 0;
				float stepIndex = 0;
				float numSteps = floor( lerp( (float)maxSamples, (float)minSamples, saturate( dot( normalWorld, viewWorld ) ) ) );
				float layerHeight = 1.0 / numSteps;
				float2 plane = parallax * ( viewDirTan.xy / viewDirTan.z );
				uvs.xy += refPlane * plane;
				float2 deltaTex = -plane * layerHeight;
				float2 prevTexOffset = 0;
				float prevRayZ = 1.0f;
				float prevHeight = 0.0f;
				float2 currTexOffset = deltaTex;
				float currRayZ = 1.0f - layerHeight;
				float currHeight = 0.0f;
				float intersection = 0;
				float2 finalTexOffset = 0;
				while ( stepIndex < numSteps + 1 )
				{
				 	currHeight = tex2Dgrad( heightMap, uvs + currTexOffset, dx, dy ).g;
				 	if ( currHeight > currRayZ )
				 	{
				 	 	stepIndex = numSteps + 1;
				 	}
				 	else
				 	{
				 	 	stepIndex++;
				 	 	prevTexOffset = currTexOffset;
				 	 	prevRayZ = currRayZ;
				 	 	prevHeight = currHeight;
				 	 	currTexOffset += deltaTex;
				 	 	currRayZ -= layerHeight;
				 	}
				}
				float sectionSteps = sidewallSteps;
				float sectionIndex = 0;
				float newZ = 0;
				float newHeight = 0;
				while ( sectionIndex < sectionSteps )
				{
				 	intersection = ( prevHeight - prevRayZ ) / ( prevHeight - currHeight + currRayZ - prevRayZ );
				 	finalTexOffset = prevTexOffset + intersection * deltaTex;
				 	newZ = prevRayZ - intersection * layerHeight;
				 	newHeight = tex2Dgrad( heightMap, uvs + finalTexOffset, dx, dy ).g;
				 	if ( newHeight > newZ )
				 	{
				 	 	currTexOffset = finalTexOffset;
				 	 	currHeight = newHeight;
				 	 	currRayZ = newZ;
				 	 	deltaTex = intersection * deltaTex;
				 	 	layerHeight = intersection * layerHeight;
				 	}
				 	else
				 	{
				 	 	prevTexOffset = finalTexOffset;
				 	 	prevHeight = newHeight;
				 	 	prevRayZ = newZ;
				 	 	deltaTex = ( 1 - intersection ) * deltaTex;
				 	 	layerHeight = ( 1 - intersection ) * layerHeight;
				 	}
				 	sectionIndex++;
				}
				return uvs.xy + finalTexOffset;
			}
			
			inline float VectorIndex1_g313( float4 Vector, float Index )
			{
				return Vector[Index];
			}
			
			float ToMaterial152( float In )
			{
				float arr[4]={0.25,.5,.75,1};
				return arr[In];
			}
			
			inline float FindIndex1_g302( float In )
			{
				return (In-0.009)*4;;
			}
			
			float4 MergeColors6_g321( float In, float4 C0, float4 C1, float4 C2, float4 C3 )
			{
				//return In.x*C0+In.y*C1+In.z*C2+In.w*C3;
				float4 arr[4]={C0,C1,C2,C3};
				return arr[In];
			}
			
			inline float VectorIndex1_g308( float4 Vector, float Index )
			{
				return Vector[Index];
			}
			
			inline float VectorIndex1_g304( float4 Vector, float Index )
			{
				return Vector[Index];
			}
			
			inline float VectorIndex1_g305( float4 Vector, float Index )
			{
				return Vector[Index];
			}
			
			float4 BlendVector1_g306( float In )
			{
				float4 _in=saturate(float4(In,In-0.25,In-0.5,In-0.75)*4);
				_in.xyz-=_in.yzw;
				return (_in);
			}
			
			float3 Merge3Colors96( float4 C0, float4 C1, float4 C2, float4 In )
			{
				return (In.x*C0+In.y*C1+In.z*C2).xyz;
				//float4 arr[4]={C0,C1,C2,C2};
				//return arr[In];
			}
			
			inline float VectorIndex1_g312( float4 Vector, float Index )
			{
				return Vector[Index];
			}
			
			inline float VectorIndex1_g311( float4 Vector, float Index )
			{
				return Vector[Index];
			}
			

			PackedVaryings VertexFunction( Attributes input  )
			{
				PackedVaryings output = (PackedVaryings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				float3 ase_positionWS = TransformObjectToWorld( ( input.positionOS ).xyz );
				float vertexToFrag34 = distance( _WorldSpaceCameraPos , ase_positionWS );
				output.ase_texcoord8.z = vertexToFrag34;
				
				float4 C096 = _Emissive1;
				float4 C196 = _Emissive2;
				float4 C296 = _Emissive3;
				float In1_g306 = input.ase_color.r;
				float4 localBlendVector1_g306 = BlendVector1_g306( In1_g306 );
				float4 In96 = step( float4( 0.8,0.8,0.8,0 ) , localBlendVector1_g306 );
				float3 localMerge3Colors96 = Merge3Colors96( C096 , C196 , C296 , In96 );
				float3 vertexToFrag204 = localMerge3Colors96;
				output.ase_texcoord10.xyz = vertexToFrag204;
				
				output.ase_texcoord8.xy = input.texcoord.xy;
				output.ase_color = input.ase_color;
				output.ase_texcoord9.xy = input.texcoord2.xy;
				output.ase_texcoord9.zw = input.texcoord1.xy;
				
				//setting value to unused interpolator channels and avoid initialization warnings
				output.ase_texcoord8.w = 0;
				output.ase_texcoord10.w = 0;
				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;
				input.tangentOS = input.tangentOS;

				VertexPositionInputs vertexInput = GetVertexPositionInputs( input.positionOS.xyz );
				VertexNormalInputs normalInput = GetVertexNormalInputs( input.normalOS, input.tangentOS );

				output.tSpace0 = float4( normalInput.normalWS, vertexInput.positionWS.x);
				output.tSpace1 = float4( normalInput.tangentWS, vertexInput.positionWS.y);
				output.tSpace2 = float4( normalInput.bitangentWS, vertexInput.positionWS.z);

				#if defined(LIGHTMAP_ON)
					OUTPUT_LIGHTMAP_UV(input.texcoord1, unity_LightmapST, output.lightmapUVOrVertexSH.xy);
				#endif

				#if defined(DYNAMICLIGHTMAP_ON)
					output.dynamicLightmapUV.xy = input.texcoord2.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
				#endif

				#if !defined(LIGHTMAP_ON)
					OUTPUT_SH(normalInput.normalWS.xyz, output.lightmapUVOrVertexSH.xyz);
				#endif

				#if defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
					output.lightmapUVOrVertexSH.zw = input.texcoord.xy;
					output.lightmapUVOrVertexSH.xy = input.texcoord.xy * unity_LightmapST.xy + unity_LightmapST.zw;
				#endif

				#if defined(ASE_FOG) || defined(_ADDITIONAL_LIGHTS_VERTEX)
					output.fogFactorAndVertexLight = 0;
					#if defined(ASE_FOG) && !defined(_FOG_FRAGMENT)
						// @diogo: no fog applied in GBuffer
					#endif
					#ifdef _ADDITIONAL_LIGHTS_VERTEX
						half3 vertexLight = VertexLighting( vertexInput.positionWS, normalInput.normalWS );
						output.fogFactorAndVertexLight.yzw = vertexLight;
					#endif
				#endif

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
					output.shadowCoord = GetShadowCoord( vertexInput );
				#endif

				output.positionCS = vertexInput.positionCS;
				output.clipPosV = vertexInput.positionCS;
				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				float3 normalOS : NORMAL;
				float4 tangentOS : TANGENT;
				float4 texcoord : TEXCOORD0;
				float4 texcoord1 : TEXCOORD1;
				float4 texcoord2 : TEXCOORD2;
				float4 ase_color : COLOR;

				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.positionOS = input.positionOS;
				output.normalOS = input.normalOS;
				output.tangentOS = input.tangentOS;
				output.texcoord = input.texcoord;
				output.texcoord1 = input.texcoord1;
				output.texcoord2 = input.texcoord2;
				output.ase_color = input.ase_color;
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				output.tangentOS = patch[0].tangentOS * bary.x + patch[1].tangentOS * bary.y + patch[2].tangentOS * bary.z;
				output.texcoord = patch[0].texcoord * bary.x + patch[1].texcoord * bary.y + patch[2].texcoord * bary.z;
				output.texcoord1 = patch[0].texcoord1 * bary.x + patch[1].texcoord1 * bary.y + patch[2].texcoord1 * bary.z;
				output.texcoord2 = patch[0].texcoord2 * bary.x + patch[1].texcoord2 * bary.y + patch[2].texcoord2 * bary.z;
				output.ase_color = patch[0].ase_color * bary.x + patch[1].ase_color * bary.y + patch[2].ase_color * bary.z;
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			FragmentOutput frag ( PackedVaryings input
								#ifdef ASE_DEPTH_WRITE_ON
								,out float outputDepth : ASE_SV_DEPTH
								#endif
								 )
			{
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

				#if defined(LOD_FADE_CROSSFADE)
					LODFadeCrossFade( input.positionCS );
				#endif

				#if defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
					float2 sampleCoords = (input.lightmapUVOrVertexSH.zw / _TerrainHeightmapRecipSize.zw + 0.5f) * _TerrainHeightmapRecipSize.xy;
					float3 WorldNormal = TransformObjectToWorldNormal(normalize(SAMPLE_TEXTURE2D(_TerrainNormalmapTexture, sampler_TerrainNormalmapTexture, sampleCoords).rgb * 2 - 1));
					float3 WorldTangent = -cross(GetObjectToWorldMatrix()._13_23_33, WorldNormal);
					float3 WorldBiTangent = cross(WorldNormal, -WorldTangent);
				#else
					float3 WorldNormal = normalize( input.tSpace0.xyz );
					float3 WorldTangent = input.tSpace1.xyz;
					float3 WorldBiTangent = input.tSpace2.xyz;
				#endif

				float3 WorldPosition = float3(input.tSpace0.w,input.tSpace1.w,input.tSpace2.w);
				float3 WorldViewDirection = GetWorldSpaceNormalizeViewDir( WorldPosition );
				float4 ShadowCoords = float4( 0, 0, 0, 0 );
				float4 ClipPos = input.clipPosV;
				float4 ScreenPos = ComputeScreenPos( input.clipPosV );

				float2 NormalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

				#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
					ShadowCoords = input.shadowCoord;
				#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
					ShadowCoords = TransformWorldToShadowCoord( WorldPosition );
				#else
					ShadowCoords = float4(0, 0, 0, 0);
				#endif

				float2 texCoord137 = input.ase_texcoord8.xy * float2( 1,1 ) + float2( 0,0 );
				float2 TexUV136 = texCoord137;
				float3 tanToWorld0 = float3( WorldTangent.x, WorldBiTangent.x, WorldNormal.x );
				float3 tanToWorld1 = float3( WorldTangent.y, WorldBiTangent.y, WorldNormal.y );
				float3 tanToWorld2 = float3( WorldTangent.z, WorldBiTangent.z, WorldNormal.z );
				float3 ase_viewVectorTS =  tanToWorld0 * ( _WorldSpaceCameraPos.xyz - WorldPosition ).x + tanToWorld1 * ( _WorldSpaceCameraPos.xyz - WorldPosition ).y  + tanToWorld2 * ( _WorldSpaceCameraPos.xyz - WorldPosition ).z;
				float3 ase_viewDirTS = normalize( ase_viewVectorTS );
				float temp_output_1_0_g320 = 0.0;
				float vertexToFrag34 = input.ase_texcoord8.z;
				float CameraDistance35 = vertexToFrag34;
				float temp_output_1_0_g298 = _ParallaxMaxDistance1;
				float temp_output_44_0 = saturate( ( ( CameraDistance35 - temp_output_1_0_g298 ) / ( _ParallaxMinDistance1 - temp_output_1_0_g298 ) ) );
				float4 tex2DNode45 = tex2D( _TrimSheet, TexUV136 );
				float temp_output_51_0 = step( ( ParallaxOffsets.w + _ParallaxCropDistance ) , CameraDistance35 );
				float lerpResult53 = lerp( temp_output_44_0 , ( temp_output_44_0 * tex2DNode45.a ) , temp_output_51_0);
				float3 break56 = ( ( float3(8,16,4) + (ParallaxOffsets).xyz ) * lerpResult53 * _QualityMultipliers );
				float2 OffsetPOM194 = POM( _TrimSheet, TexUV136, ddx(TexUV136), ddy(TexUV136), WorldNormal, WorldViewDirection, ase_viewDirTS, (int)break56.x, (int)break56.y, (int)break56.z, ( _ParallaxOffset1 * ( ( (ase_viewDirTS).z - temp_output_1_0_g320 ) / ( _AngleTolerance1 - temp_output_1_0_g320 ) ) * lerpResult53 * lerpResult53 ), _ReferencePlane, _TrimSheet_ST.xy, float2(1,1), 0 );
				float2 ParallaxUV175 = OffsetPOM194;
				float4 tex2DNode197 = tex2D( _TrimSheet, ParallaxUV175 );
				float Trim_Occlusion191 = tex2DNode197.r;
				float Occlusion154 = Trim_Occlusion191;
				float2 texCoord57 = input.ase_texcoord9.xy * float2( 1,1 ) + float2( 0,0 );
				float4 Vector1_g313 = ( tex2D( _VinylTrimsheet, texCoord57 ) * saturate( _VinylSelect ) );
				float Index1_g313 = floor( ( _VinylSelect - 1.0 ) );
				float localVectorIndex1_g313 = VectorIndex1_g313( Vector1_g313 , Index1_g313 );
				float In152 = _VinylMaterial;
				float localToMaterial152 = ToMaterial152( In152 );
				float Vinyl_Material153 = ( step( 0.8 , localVectorIndex1_g313 ) * localToMaterial152 );
				float Trim_Material190 = tex2DNode197.b;
				float lerpResult199 = lerp( Vinyl_Material153 , Trim_Material190 , step( 0.2 , Trim_Material190 ));
				float lerpResult72 = lerp( input.ase_color.a , lerpResult199 , step( 0.1 , lerpResult199 ));
				float In1_g302 = lerpResult72;
				float localFindIndex1_g302 = FindIndex1_g302( In1_g302 );
				float temp_output_76_0 = localFindIndex1_g302;
				float In6_g321 = temp_output_76_0;
				float4 C06_g321 = _Color0;
				float4 C16_g321 = _Color1;
				float4 C26_g321 = _Color2;
				float4 C36_g321 = _Color3;
				float4 localMergeColors6_g321 = MergeColors6_g321( In6_g321 , C06_g321 , C16_g321 , C26_g321 , C36_g321 );
				float4 Color138 = ( Occlusion154 * localMergeColors6_g321 );
				float2 temp_cast_8 = (_Tiling).xx;
				float2 temp_cast_9 = (_Blend).xx;
				float2 texCoord7_g323 = input.ase_texcoord9.zw * temp_cast_8 + temp_cast_9;
				float4 tex2DNode10_g323 = tex2D( _SurfaceTexture, texCoord7_g323 );
				float4 break68 = tex2DNode10_g323;
				float Texture_R95 = break68.x;
				float4 Vector1_g308 = _ColorOffset;
				float Index1_g308 = temp_output_76_0;
				float localVectorIndex1_g308 = VectorIndex1_g308( Vector1_g308 , Index1_g308 );
				float ColorOffset94 = localVectorIndex1_g308;
				float lerpResult103 = lerp( Texture_R95 , 1.0 , ColorOffset94);
				float Pattern_1_Source70 = break68.y;
				float Pattern_2_Source71 = break68.z;
				float4 Vector1_g304 = _Pattern;
				float temp_output_11_0_g303 = temp_output_76_0;
				float Index1_g304 = temp_output_11_0_g303;
				float localVectorIndex1_g304 = VectorIndex1_g304( Vector1_g304 , Index1_g304 );
				float lerpResult5_g303 = lerp( Pattern_1_Source70 , Pattern_2_Source71 , localVectorIndex1_g304);
				float Pattern81 = lerpResult5_g303;
				float4 Vector1_g305 = _PatternIntensity;
				float Index1_g305 = temp_output_11_0_g303;
				float localVectorIndex1_g305 = VectorIndex1_g305( Vector1_g305 , Index1_g305 );
				float PatternIntensity82 = localVectorIndex1_g305;
				float lerpResult86 = lerp( 0.5 , Pattern81 , PatternIntensity82);
				float PatternColor98 = ( lerpResult86 * 2.0 );
				float4 FinalColor139 = saturate( ( Color138 * lerpResult103 * PatternColor98 ) );
				
				float Trim_Normal_Mask187 = tex2DNode197.a;
				float3 lerpResult109 = lerp( float3( 0,0,1 ) , UnpackNormalScale( tex2D( _Normal, ParallaxUV175 ), 1.0f ) , Trim_Normal_Mask187);
				float3 NormalMap192 = lerpResult109;
				
				float3 vertexToFrag204 = input.ase_texcoord10.xyz;
				float3 Glow100 = vertexToFrag204;
				
				float4 Vector1_g312 = _Metalness;
				float temp_output_7_0_g310 = temp_output_76_0;
				float Index1_g312 = temp_output_7_0_g310;
				float localVectorIndex1_g312 = VectorIndex1_g312( Vector1_g312 , Index1_g312 );
				float Metalness122 = localVectorIndex1_g312;
				
				float4 Vector1_g311 = _SmoothnessOffset;
				float Index1_g311 = temp_output_7_0_g310;
				float localVectorIndex1_g311 = VectorIndex1_g311( Vector1_g311 , Index1_g311 );
				float SmoothnessOffset115 = localVectorIndex1_g311;
				float Smoothness_Source111 = break68.w;
				float Smoothness121 = ( SmoothnessOffset115 + ( Color138.w * Smoothness_Source111 ) + ( 0.5 - lerpResult86 ) );
				

				float3 BaseColor = FinalColor139.xyz;
				float3 Normal = NormalMap192;
				float3 Emission = Glow100;
				float3 Specular = 0.5;
				float Metallic = Metalness122;
				float Smoothness = Smoothness121;
				float Occlusion = Occlusion154;
				float Alpha = 1;
				float AlphaClipThreshold = 0.5;
				float AlphaClipThresholdShadow = 0.5;
				float3 BakedGI = 0;
				float3 RefractionColor = 1;
				float RefractionIndex = 1;
				float3 Transmission = 1;
				float3 Translucency = 1;

				#ifdef ASE_DEPTH_WRITE_ON
					float DepthValue = input.positionCS.z;
				#endif

				#ifdef _ALPHATEST_ON
					clip(Alpha - AlphaClipThreshold);
				#endif

				InputData inputData = (InputData)0;
				inputData.positionWS = WorldPosition;
				inputData.positionCS = input.positionCS;
				inputData.shadowCoord = ShadowCoords;

				#ifdef _NORMALMAP
					#if _NORMAL_DROPOFF_TS
						inputData.normalWS = TransformTangentToWorld(Normal, half3x3( WorldTangent, WorldBiTangent, WorldNormal ));
					#elif _NORMAL_DROPOFF_OS
						inputData.normalWS = TransformObjectToWorldNormal(Normal);
					#elif _NORMAL_DROPOFF_WS
						inputData.normalWS = Normal;
					#endif
				#else
					inputData.normalWS = WorldNormal;
				#endif

				inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
				inputData.viewDirectionWS = SafeNormalize( WorldViewDirection );

				#ifdef ASE_FOG
					// @diogo: no fog applied in GBuffer
				#endif
				#ifdef _ADDITIONAL_LIGHTS_VERTEX
					inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
				#endif

				#if defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
					float3 SH = SampleSH(inputData.normalWS.xyz);
				#else
					float3 SH = input.lightmapUVOrVertexSH.xyz;
				#endif

				#ifdef ASE_BAKEDGI
					inputData.bakedGI = BakedGI;
				#else
					#if defined(DYNAMICLIGHTMAP_ON)
						inputData.bakedGI = SAMPLE_GI( input.lightmapUVOrVertexSH.xy, input.dynamicLightmapUV.xy, SH, inputData.normalWS);
					#else
						inputData.bakedGI = SAMPLE_GI( input.lightmapUVOrVertexSH.xy, SH, inputData.normalWS );
					#endif
				#endif

				inputData.normalizedScreenSpaceUV = NormalizedScreenSpaceUV;
				inputData.shadowMask = SAMPLE_SHADOWMASK(input.lightmapUVOrVertexSH.xy);

				#if defined(DEBUG_DISPLAY)
					#if defined(DYNAMICLIGHTMAP_ON)
						inputData.dynamicLightmapUV = input.dynamicLightmapUV.xy;
						#endif
					#if defined(LIGHTMAP_ON)
						inputData.staticLightmapUV = input.lightmapUVOrVertexSH.xy;
					#else
						inputData.vertexSH = SH;
					#endif
				#endif

				#ifdef _DBUFFER
					ApplyDecal(input.positionCS,
						BaseColor,
						Specular,
						inputData.normalWS,
						Metallic,
						Occlusion,
						Smoothness);
				#endif

				BRDFData brdfData;
				InitializeBRDFData
				(BaseColor, Metallic, Specular, Smoothness, Alpha, brdfData);

				Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
				half4 color;
				MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI, inputData.shadowMask);
				color.rgb = GlobalIllumination(brdfData, inputData.bakedGI, Occlusion, inputData.positionWS, inputData.normalWS, inputData.viewDirectionWS);
				color.a = Alpha;

				#ifdef ASE_FINAL_COLOR_ALPHA_MULTIPLY
					color.rgb *= color.a;
				#endif

				#ifdef ASE_DEPTH_WRITE_ON
					outputDepth = DepthValue;
				#endif

				return BRDFDataToGbuffer(brdfData, inputData, Smoothness, Emission + color.rgb, Occlusion);
			}

			ENDHLSL
		}

		
		Pass
		{
			
			Name "SceneSelectionPass"
			Tags { "LightMode"="SceneSelectionPass" }

			Cull Off
			AlphaToMask Off

			HLSLPROGRAM

			

			#define _NORMAL_DROPOFF_TS 1
			#define ASE_FOG 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19801
			#define ASE_SRP_VERSION 140011


			

			#pragma vertex vert
			#pragma fragment frag

			#if defined(_SPECULAR_SETUP) && defined(_ASE_LIGHTING_SIMPLE)
				#define _SPECULAR_COLOR 1
			#endif

			#define SCENESELECTIONPASS 1

			#define ATTRIBUTES_NEED_NORMAL
			#define ATTRIBUTES_NEED_TANGENT
			#define SHADERPASS SHADERPASS_DEPTHONLY

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"

			
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
           

			
            #if ASE_SRP_VERSION >=140009
			#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
			#endif
		

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"

			
            #if ASE_SRP_VERSION >=140007
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#endif
		

			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			

			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				float4 positionCS : SV_POSITION;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _Color1;
			float4 _Emissive3;
			float4 _Emissive2;
			float4 _Emissive1;
			half4 _PatternIntensity;
			half4 _Pattern;
			half4 _ColorOffset;
			float4 _Color3;
			float4 _Color2;
			float4 _Metalness;
			float4 _SmoothnessOffset;
			float4 _TrimSheet_ST;
			float4 _Color0;
			float3 _QualityMultipliers;
			float _VinylMaterial;
			float _VinylSelect;
			float _Tiling;
			float _Blend;
			float _ReferencePlane;
			float _ParallaxCropDistance;
			float _ParallaxMinDistance1;
			float _ParallaxMaxDistance1;
			float _AngleTolerance1;
			float _ParallaxOffset1;
			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			

			
			struct SurfaceDescription
			{
				float Alpha;
				float AlphaClipThreshold;
			};

			PackedVaryings VertexFunction(Attributes input  )
			{
				PackedVaryings output;
				ZERO_INITIALIZE(PackedVaryings, output);

				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;

				float3 positionWS = TransformObjectToWorld( input.positionOS.xyz );

				output.positionCS = TransformWorldToHClip(positionWS);

				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				float3 normalOS : NORMAL;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.positionOS = input.positionOS;
				output.normalOS = input.normalOS;
				
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			half4 frag(PackedVaryings input ) : SV_Target
			{
				SurfaceDescription surfaceDescription = (SurfaceDescription)0;

				

				surfaceDescription.Alpha = 1;
				surfaceDescription.AlphaClipThreshold = 0.5;

				#if _ALPHATEST_ON
					float alphaClipThreshold = 0.01f;
					#if ALPHA_CLIP_THRESHOLD
						alphaClipThreshold = surfaceDescription.AlphaClipThreshold;
					#endif
					clip(surfaceDescription.Alpha - alphaClipThreshold);
				#endif

				half4 outColor = 0;

				#ifdef SCENESELECTIONPASS
					outColor = half4(_ObjectId, _PassValue, 1.0, 1.0);
				#elif defined(SCENEPICKINGPASS)
					outColor = _SelectionID;
				#endif

				return outColor;
			}

			ENDHLSL
		}

		
		Pass
		{
			
			Name "ScenePickingPass"
			Tags { "LightMode"="Picking" }

			AlphaToMask Off

			HLSLPROGRAM

			

			#define _NORMAL_DROPOFF_TS 1
			#define ASE_FOG 1
			#define _EMISSION
			#define _NORMALMAP 1
			#define ASE_VERSION 19801
			#define ASE_SRP_VERSION 140011


			

			#pragma vertex vert
			#pragma fragment frag

			#if defined(_SPECULAR_SETUP) && defined(_ASE_LIGHTING_SIMPLE)
				#define _SPECULAR_COLOR 1
			#endif

		    #define SCENEPICKINGPASS 1

			#define ATTRIBUTES_NEED_NORMAL
			#define ATTRIBUTES_NEED_TANGENT
			#define SHADERPASS SHADERPASS_DEPTHONLY

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Texture.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Input.hlsl"
			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/TextureStack.hlsl"

			
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
           

			
            #if ASE_SRP_VERSION >=140009
			#include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
			#endif
		

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderGraphFunctions.hlsl"

			
            #if ASE_SRP_VERSION >=140007
			#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
			#endif
		

			#include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"

			

			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS : NORMAL;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct PackedVaryings
			{
				float4 positionCS : SV_POSITION;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			CBUFFER_START(UnityPerMaterial)
			float4 _Color1;
			float4 _Emissive3;
			float4 _Emissive2;
			float4 _Emissive1;
			half4 _PatternIntensity;
			half4 _Pattern;
			half4 _ColorOffset;
			float4 _Color3;
			float4 _Color2;
			float4 _Metalness;
			float4 _SmoothnessOffset;
			float4 _TrimSheet_ST;
			float4 _Color0;
			float3 _QualityMultipliers;
			float _VinylMaterial;
			float _VinylSelect;
			float _Tiling;
			float _Blend;
			float _ReferencePlane;
			float _ParallaxCropDistance;
			float _ParallaxMinDistance1;
			float _ParallaxMaxDistance1;
			float _AngleTolerance1;
			float _ParallaxOffset1;
			#ifdef ASE_TRANSMISSION
				float _TransmissionShadow;
			#endif
			#ifdef ASE_TRANSLUCENCY
				float _TransStrength;
				float _TransNormal;
				float _TransScattering;
				float _TransDirect;
				float _TransAmbient;
				float _TransShadow;
			#endif
			#ifdef ASE_TESSELLATION
				float _TessPhongStrength;
				float _TessValue;
				float _TessMin;
				float _TessMax;
				float _TessEdgeLength;
				float _TessMaxDisp;
			#endif
			CBUFFER_END

			#ifdef SCENEPICKINGPASS
				float4 _SelectionID;
			#endif

			#ifdef SCENESELECTIONPASS
				int _ObjectId;
				int _PassValue;
			#endif

			

			
			struct SurfaceDescription
			{
				float Alpha;
				float AlphaClipThreshold;
			};

			PackedVaryings VertexFunction(Attributes input  )
			{
				PackedVaryings output;
				ZERO_INITIALIZE(PackedVaryings, output);

				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					float3 defaultVertexValue = input.positionOS.xyz;
				#else
					float3 defaultVertexValue = float3(0, 0, 0);
				#endif

				float3 vertexValue = defaultVertexValue;

				#ifdef ASE_ABSOLUTE_VERTEX_POS
					input.positionOS.xyz = vertexValue;
				#else
					input.positionOS.xyz += vertexValue;
				#endif

				input.normalOS = input.normalOS;

				float3 positionWS = TransformObjectToWorld( input.positionOS.xyz );
				output.positionCS = TransformWorldToHClip(positionWS);

				return output;
			}

			#if defined(ASE_TESSELLATION)
			struct VertexControl
			{
				float4 positionOS : INTERNALTESSPOS;
				float3 normalOS : NORMAL;
				
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct TessellationFactors
			{
				float edge[3] : SV_TessFactor;
				float inside : SV_InsideTessFactor;
			};

			VertexControl vert ( Attributes input )
			{
				VertexControl output;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				output.positionOS = input.positionOS;
				output.normalOS = input.normalOS;
				
				return output;
			}

			TessellationFactors TessellationFunction (InputPatch<VertexControl,3> input)
			{
				TessellationFactors output;
				float4 tf = 1;
				float tessValue = _TessValue; float tessMin = _TessMin; float tessMax = _TessMax;
				float edgeLength = _TessEdgeLength; float tessMaxDisp = _TessMaxDisp;
				#if defined(ASE_FIXED_TESSELLATION)
				tf = FixedTess( tessValue );
				#elif defined(ASE_DISTANCE_TESSELLATION)
				tf = DistanceBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, tessValue, tessMin, tessMax, GetObjectToWorldMatrix(), _WorldSpaceCameraPos );
				#elif defined(ASE_LENGTH_TESSELLATION)
				tf = EdgeLengthBasedTess(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams );
				#elif defined(ASE_LENGTH_CULL_TESSELLATION)
				tf = EdgeLengthBasedTessCull(input[0].positionOS, input[1].positionOS, input[2].positionOS, edgeLength, tessMaxDisp, GetObjectToWorldMatrix(), _WorldSpaceCameraPos, _ScreenParams, unity_CameraWorldClipPlanes );
				#endif
				output.edge[0] = tf.x; output.edge[1] = tf.y; output.edge[2] = tf.z; output.inside = tf.w;
				return output;
			}

			[domain("tri")]
			[partitioning("fractional_odd")]
			[outputtopology("triangle_cw")]
			[patchconstantfunc("TessellationFunction")]
			[outputcontrolpoints(3)]
			VertexControl HullFunction(InputPatch<VertexControl, 3> patch, uint id : SV_OutputControlPointID)
			{
				return patch[id];
			}

			[domain("tri")]
			PackedVaryings DomainFunction(TessellationFactors factors, OutputPatch<VertexControl, 3> patch, float3 bary : SV_DomainLocation)
			{
				Attributes output = (Attributes) 0;
				output.positionOS = patch[0].positionOS * bary.x + patch[1].positionOS * bary.y + patch[2].positionOS * bary.z;
				output.normalOS = patch[0].normalOS * bary.x + patch[1].normalOS * bary.y + patch[2].normalOS * bary.z;
				
				#if defined(ASE_PHONG_TESSELLATION)
				float3 pp[3];
				for (int i = 0; i < 3; ++i)
					pp[i] = output.positionOS.xyz - patch[i].normalOS * (dot(output.positionOS.xyz, patch[i].normalOS) - dot(patch[i].positionOS.xyz, patch[i].normalOS));
				float phongStrength = _TessPhongStrength;
				output.positionOS.xyz = phongStrength * (pp[0]*bary.x + pp[1]*bary.y + pp[2]*bary.z) + (1.0f-phongStrength) * output.positionOS.xyz;
				#endif
				UNITY_TRANSFER_INSTANCE_ID(patch[0], output);
				return VertexFunction(output);
			}
			#else
			PackedVaryings vert ( Attributes input )
			{
				return VertexFunction( input );
			}
			#endif

			half4 frag(PackedVaryings input ) : SV_Target
			{
				SurfaceDescription surfaceDescription = (SurfaceDescription)0;

				

				surfaceDescription.Alpha = 1;
				surfaceDescription.AlphaClipThreshold = 0.5;

				#if _ALPHATEST_ON
					float alphaClipThreshold = 0.01f;
					#if ALPHA_CLIP_THRESHOLD
						alphaClipThreshold = surfaceDescription.AlphaClipThreshold;
					#endif
						clip(surfaceDescription.Alpha - alphaClipThreshold);
				#endif

				half4 outColor = 0;

				#ifdef SCENESELECTIONPASS
					outColor = half4(_ObjectId, _PassValue, 1.0, 1.0);
				#elif defined(SCENEPICKINGPASS)
					outColor = _SelectionID;
				#endif

				return outColor;
			}

			ENDHLSL
		}
		
	}
	
	CustomEditor "UnityEditor.ShaderGraphLitGUI"
	FallBack "Hidden/Shader Graph/FallbackError"
	
	Fallback Off
}
/*ASEBEGIN
Version=19801
Node;AmplifyShaderEditor.CommentaryNode;10;-4691.346,-1806.906;Inherit;False;2575.833;972.4527;Multi Material;33;202;201;199;198;196;169;140;138;129;127;122;115;112;100;96;94;92;91;90;89;88;85;82;81;79;76;74;73;72;63;13;11;204;;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;19;-3923.346,-542.9056;Inherit;False;926;377;Camera Distance;5;35;34;33;32;31;;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;20;-2895.346,-526.9056;Inherit;False;1701.329;681.2104;Trim Sheet + Parallax Occlusion Mapping;17;203;197;193;192;191;190;188;187;137;136;135;128;109;108;38;37;36;;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;21;-2899.346,177.0944;Inherit;False;2763.459;970.9841;Parallax Mapping;45;195;194;189;186;185;184;183;178;175;174;173;172;171;167;166;165;164;163;162;155;133;132;131;130;126;125;124;123;56;54;53;52;51;50;49;48;47;46;45;44;43;42;41;40;39;;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;22;-4670.346,-142.9056;Inherit;False;1741.525;475.2219;Vinyls;13;177;176;153;152;151;134;62;61;60;59;58;57;55;;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;23;-3603.346,-814.9056;Inherit;False;1493.76;242.1955;Occlusion;2;154;78;;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;24;-931.3463,-862.9056;Inherit;False;734.8041;401.9758;Surface UV Coordinates;6;182;111;95;71;70;68;;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;26;-2051.346,-878.9056;Inherit;False;842.9048;309.95;Pattern;6;118;98;93;86;84;83;;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;27;-931.3463,-1182.906;Inherit;False;1332;290.95;Final Color;8;168;139;120;104;103;102;99;97;;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;29;-2067.346,-1230.906;Inherit;False;1065.752;325.6902;Smoothness;7;121;119;117;116;114;113;110;;1,1,1,1;0;0
Node;AmplifyShaderEditor.VertexColorNode;11;-4115.346,-1374.906;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RegisterLocalVarNode;13;-3459.346,-1246.906;Inherit;False;Bluchannel;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WorldSpaceCameraPos;31;-3891.346,-494.9056;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.WorldPosInputsNode;32;-3795.346,-318.9056;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.DistanceOpNode;33;-3603.346,-398.9056;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.VertexToFragmentNode;34;-3459.346,-398.9056;Inherit;False;False;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;35;-3219.346,-398.9056;Inherit;False;CameraDistance;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TexturePropertyNode;36;-2787.346,-462.9056;Inherit;True;Property;_TrimSheet;TrimSheet;5;2;[Header];[SingleLineTexture];Create;True;1;Trim Sheet _____________________________________________;0;0;False;0;False;None;None;False;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.RegisterLocalVarNode;37;-2371.346,-398.9056;Inherit;False;SS;-1;True;1;0;SAMPLERSTATE;;False;1;SAMPLERSTATE;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;38;-2371.346,-478.9056;Inherit;False;Tex;-1;True;1;0;SAMPLER2D;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.RangedFloatNode;39;-2819.346,721.0944;Inherit;False;Property;_ParallaxMinDistance1;Parallax Min Distance;30;0;Create;True;0;0;0;False;0;False;5;2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;40;-2819.346,801.0944;Inherit;False;35;CameraDistance;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;41;-2483.346,721.0944;Inherit;False;Inverse Lerp;-1;;298;09cbe79402f023141a4dc1fddd4c9511;0;3;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode;42;-2243.346,865.0944;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node;43;-2163.346,561.0944;Inherit;False;Global;ParallaxOffsets;Parallax Offsets;25;0;Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SaturateNode;44;-2307.346,721.0944;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;45;-2531.346,897.0944;Inherit;True;Property;_Channels2;Channels;2;3;[Header];[NoScaleOffset];[SingleLineTexture];Create;True;1;Trim Sheet _____________________________________________;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.WireNode;46;-2147.346,1009.094;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;47;-1859.346,929.0944;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;48;-2131.346,785.0944;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;49;-2163.346,257.0944;Inherit;False;Property;_AngleTolerance1;Angle Tolerance;28;0;Create;True;1;Header(Parallax Occlusion Mapping ___________________________);0;0;False;0;False;0.5;0.28;0.1;0.99;0;1;FLOAT;0
Node;AmplifyShaderEditor.ComponentMaskNode;50;-1811.346,689.0944;Inherit;False;True;True;True;False;1;0;FLOAT4;0,0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.StepOpNode;51;-1731.346,961.0944;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;52;-1203.346,529.0944;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LerpOp;53;-1555.346,737.0944;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;54;-995.3463,529.0944;Inherit;False;3;3;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;2;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;55;-4643.346,177.0944;Inherit;False;Property;_VinylSelect;Vinyl Select;7;1;[IntRange];Create;True;0;0;0;False;0;False;0;1;0;4;0;1;FLOAT;0
Node;AmplifyShaderEditor.BreakToComponentsNode;56;-835.3463,529.0944;Inherit;False;FLOAT3;1;0;FLOAT3;0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.TextureCoordinatesNode;57;-4435.346,-78.90564;Inherit;False;2;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleSubtractOpNode;58;-4339.346,177.0944;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;59;-4179.346,-94.90564;Inherit;True;Property;_VinylTrimsheet;Vinyl Trimsheet;9;2;[NoScaleOffset];[SingleLineTexture];Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;LockedToTexture2D;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.FloorOpNode;60;-4163.346,177.0944;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;61;-4643.346,257.0944;Inherit;False;Property;_VinylMaterial;Vinyl Material;8;1;[IntRange];Create;True;0;0;0;False;0;False;0;0;0;3;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;62;-3315.346,65.09436;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;63;-3459.346,-1326.906;Inherit;False;GreenChannel;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.BreakToComponentsNode;68;-659.3463,-686.9056;Inherit;False;FLOAT4;1;0;FLOAT4;0,0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.RegisterLocalVarNode;70;-467.3463,-718.9056;Inherit;False;Pattern_1_Source;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;71;-467.3463,-638.9056;Inherit;False;Pattern_2_Source;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;72;-3667.346,-1086.906;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;73;-3043.346,-1198.906;Inherit;False;70;Pattern_1_Source;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;74;-3043.346,-1118.906;Inherit;False;71;Pattern_2_Source;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;76;-3411.346,-1086.906;Inherit;False;Selective Index;-1;;302;a387ce4cd4b83ea4ab83f0095ae73bcf;0;1;2;FLOAT;0.25;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;79;-2739.346,-1134.906;Inherit;False;Select Multi Pattern;21;;303;02f76630bd4cfa747a9e2e768fdae124;0;3;8;FLOAT;0;False;9;FLOAT;0;False;11;FLOAT;0;False;2;FLOAT;0;FLOAT;6
Node;AmplifyShaderEditor.RegisterLocalVarNode;81;-2371.346,-1150.906;Inherit;False;Pattern;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;84;-2003.346,-750.9056;Inherit;False;81;Pattern;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;85;-3875.346,-1358.906;Inherit;False;Selective Vector;-1;;306;4fbe5713bd64aff4da67a6c041cc0a5a;0;1;2;FLOAT;0.25;False;1;FLOAT4;0
Node;AmplifyShaderEditor.LerpOp;86;-1779.346,-766.9056;Inherit;False;3;0;FLOAT;0.5;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;88;-3043.346,-1406.906;Inherit;False;Select Multi Color Offset Dirt;24;;307;f3acb7224ba615e49b5ae61221ffeda0;0;1;8;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode;89;-4099.346,-1758.906;Float;False;Property;_Emissive1;Emissive 1;15;1;[HDR];Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ColorNode;90;-3891.346,-1694.906;Float;False;Property;_Emissive2;Emissive 2;16;1;[HDR];Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ColorNode;91;-3683.346,-1630.906;Float;False;Property;_Emissive3;Emissive 3;17;1;[HDR];Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.StepOpNode;92;-3603.346,-1390.906;Inherit;False;2;0;FLOAT4;0.8,0.8,0.8,0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;93;-1603.346,-702.9056;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;2;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;94;-2371.346,-1406.906;Inherit;False;ColorOffset;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;95;-467.3463,-798.9056;Inherit;False;Texture_R;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;97;-883.3463,-1038.906;Inherit;False;94;ColorOffset;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;98;-1443.346,-702.9056;Inherit;False;PatternColor;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;99;-883.3463,-1118.906;Inherit;False;95;Texture_R;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;102;-467.3463,-1006.906;Inherit;False;98;PatternColor;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;103;-643.3463,-1086.906;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;1;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;104;-195.3463,-1102.906;Inherit;False;3;3;0;FLOAT4;0,0,0,0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.SamplerNode;108;-1955.346,-142.9056;Inherit;True;Property;_Normal;Normal;6;3;[NoScaleOffset];[Normal];[SingleLineTexture];Create;True;0;0;0;False;0;False;-1;None;None;True;0;True;bump;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.LerpOp;109;-1587.346,-78.90564;Inherit;False;3;0;FLOAT3;0,0,1;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;110;-2051.346,-1182.906;Inherit;False;138;Color;1;0;OBJECT;;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;111;-467.3463,-558.9056;Inherit;False;Smoothness_Source;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;112;-3043.346,-1310.906;Inherit;False;Select Multi Metal Smoothness Offset;18;;310;8d8dbb58d489ec649b1549b7087b270c;0;1;7;FLOAT;0;False;2;FLOAT;0;FLOAT;5
Node;AmplifyShaderEditor.BreakToComponentsNode;113;-1843.346,-1182.906;Inherit;False;FLOAT4;1;0;FLOAT4;0,0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.GetLocalVarNode;114;-1923.346,-1022.906;Inherit;False;111;Smoothness_Source;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;115;-2371.346,-1246.906;Inherit;False;SmoothnessOffset;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;116;-1619.346,-1054.906;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;117;-1683.346,-1150.906;Inherit;False;115;SmoothnessOffset;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;118;-1603.346,-814.9056;Inherit;False;2;0;FLOAT;0.5;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;119;-1347.346,-1086.906;Inherit;False;3;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;120;-19.34625,-1102.906;Inherit;False;1;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;121;-1216,-1088;Inherit;False;Smoothness;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;122;-2371.346,-1326.906;Inherit;False;Metalness;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.FloorOpNode;123;-611.3463,737.0944;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;124;-1491.346,961.0944;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode;125;-1939.346,1057.094;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;126;-1283.346,993.0944;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StickyNoteNode;127;-3219.346,-1694.906;Inherit;False;232.2209;100;Glow Color;;0.02800441,1,0,1;Calculate the Glow color$;0;0
Node;AmplifyShaderEditor.StickyNoteNode;128;-2771.346,-254.9056;Inherit;False;293.9666;128.1185;Trim Format;;1,1,1,1;R=Occlusion$G=Heightmap (middle gray base)$B=Material Change$A=Normal Mask;0;0
Node;AmplifyShaderEditor.StickyNoteNode;129;-4227.346,-1518.906;Inherit;False;293.9666;128.1185;Vertex Color Format;;1,1,1,1;R=Glow Material$G=Occlusion/Dirt$B=Multi-Mesh$A=Multi-Material;0;0
Node;AmplifyShaderEditor.StickyNoteNode;130;-2483.346,577.0944;Inherit;False;249.823;108.23;Parallax Quality Parameters;;0,0.9245283,0.009288036,1;Use "ParallaxOffsets" via code to fine tune Parallax quality.;0;0
Node;AmplifyShaderEditor.StickyNoteNode;131;-579.3463,833.0944;Inherit;False;249.823;108.23;Parallax Debug;;0.9254902,0,0.0263739,1;Connect this node to Emission to visualize Sample count.;0;0
Node;AmplifyShaderEditor.StickyNoteNode;132;-1091.346,865.0944;Inherit;False;249.823;108.23;Parallax Debug;;0.9254902,0,0.0263739,1;Connect this node to Emission to visualize Optimization Distance.;0;0
Node;AmplifyShaderEditor.SaturateNode;133;-963.3463,993.0944;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;134;-3683.346,97.09436;Inherit;False;Vector Select;-1;;313;b444b7286dd57b441b67a33f5fa7e6f9;0;2;2;FLOAT4;0,0,0,0;False;3;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch;135;-2291.346,17.09436;Inherit;False;Property;_ParallaxMapping;Parallax Mapping;26;0;Create;True;0;0;0;False;1;Header(Parallax Occlusion Mapping ___________________________);False;0;1;1;True;;Toggle;2;Key0;Key1;Create;False;True;All;9;1;FLOAT2;0,0;False;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT2;0,0;False;6;FLOAT2;0,0;False;7;FLOAT2;0,0;False;8;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;136;-2595.346,1.09436;Inherit;False;TexUV;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;137;-2835.346,1.09436;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RegisterLocalVarNode;138;-2371.346,-958.9056;Inherit;False;Color;-1;True;1;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;139;156.6537,-1102.906;Inherit;False;FinalColor;-1;True;1;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.GetLocalVarNode;140;-3379.346,-926.9056;Inherit;False;154;Occlusion;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.StepOpNode;151;-3475.346,65.09436;Inherit;False;2;0;FLOAT;0.8;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CustomExpressionNode;152;-3715.346,257.0944;Inherit;False;float arr[4]={0.25,.5,.75,1}@$return arr[In]@;1;Create;1;True;In;FLOAT;0;In;;Inherit;False;To Material;True;False;0;;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;153;-3139.346,65.09436;Inherit;False;Vinyl Material;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;154;-2339.346,-686.9056;Inherit;False;Occlusion;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector3Node;155;-1363.346,785.0944;Inherit;False;Property;_QualityMultipliers;Quality Multipliers;33;0;Create;True;0;0;0;False;0;False;1,1,1;1,1,1;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.GetLocalVarNode;158;-51.34625,401.0944;Inherit;False;192;NormalMap;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;162;-851.3463,225.0944;Inherit;False;136;TexUV;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;163;-1075.346,241.0944;Inherit;False;38;Tex;1;0;OBJECT;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.GetLocalVarNode;164;-1043.346,321.0944;Inherit;False;37;SS;1;0;OBJECT;;False;1;SAMPLERSTATE;0
Node;AmplifyShaderEditor.FunctionNode;165;-1619.346,337.0944;Inherit;False;Inverse Lerp;-1;;320;09cbe79402f023141a4dc1fddd4c9511;0;3;1;FLOAT;0;False;2;FLOAT;0.7;False;3;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ComponentMaskNode;166;-1939.346,385.0944;Inherit;False;False;False;True;True;1;0;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;167;-467.3463,737.0944;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;16;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;168;-435.3463,-1118.906;Inherit;False;138;Color;1;0;OBJECT;;False;1;FLOAT4;0
Node;AmplifyShaderEditor.FunctionNode;169;-3043.346,-958.9056;Inherit;False;Select Multi Color;10;;321;89bec0ff38b4baf4282a7e2d406f3e25;0;3;7;FLOAT4;0,0,0,0;False;10;FLOAT;0;False;8;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.GetLocalVarNode;170;-51.34625,321.0944;Inherit;False;139;FinalColor;1;0;OBJECT;;False;1;FLOAT4;0
Node;AmplifyShaderEditor.Vector3Node;171;-1795.346,529.0944;Inherit;False;Constant;_ParallaxNearValues1;Parallax Near Values;23;0;Create;True;0;0;0;False;0;False;8,16,4;8,16,4;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.ViewDirInputsCoordNode;173;-2451.346,385.0944;Inherit;False;Tangent;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.WireNode;174;-1987.346,465.0944;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;175;-355.3463,369.0944;Inherit;False;ParallaxUV;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;176;-3843.346,49.09436;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SaturateNode;177;-4339.346,97.09436;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;178;-2819.346,641.0944;Inherit;False;Property;_ParallaxMaxDistance1;Parallax Max Distance;29;0;Create;True;0;0;0;False;0;False;8;5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;182;-899.3463,-686.9056;Inherit;False;Texturing Options;0;;323;c9cf78d015b579f4081a5353746cbd35;0;0;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;183;-1331.346,337.0944;Inherit;False;4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;184;-2835.346,881.0944;Inherit;False;38;Tex;1;0;OBJECT;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.GetLocalVarNode;185;-2835.346,961.0944;Inherit;False;136;TexUV;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;186;-2835.346,1041.094;Inherit;False;37;SS;1;0;OBJECT;;False;1;SAMPLERSTATE;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;187;-1427.346,-238.9056;Inherit;False;Trim_Normal_Mask;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;188;-1891.346,65.09436;Inherit;False;187;Trim_Normal_Mask;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;189;-1763.346,225.0944;Inherit;False;Property;_ParallaxOffset1;Parallax Offset;31;0;Create;True;0;0;0;False;0;False;0.05;0.02;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;190;-1411.346,-318.9056;Inherit;False;Trim_Material;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;191;-1427.346,-478.9056;Inherit;False;Trim_Occlusion;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;192;-1427.346,-78.90564;Inherit;False;NormalMap;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;193;-1427.346,-398.9056;Inherit;False;Trim_Height;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ParallaxOcclusionMappingNode;194;-611.3463,369.0944;Inherit;False;1;8;False;;16;False;;3;0.02;0.5;False;1,1;False;1,1;11;0;FLOAT2;0,0;False;1;SAMPLER2D;;False;7;SAMPLERSTATE;;False;2;FLOAT;0.02;False;3;FLOAT3;0,0,0;False;8;INT;0;False;9;INT;0;False;10;INT;0;False;4;FLOAT;0;False;5;FLOAT2;0,0;False;6;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;195;-995.3463,673.0944;Inherit;False;Property;_ReferencePlane;Reference Plane;32;0;Create;True;0;0;0;False;0;False;0.5;0;0;0.7;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;196;-4675.346,-1006.906;Inherit;False;190;Trim_Material;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;197;-1939.346,-494.9056;Inherit;True;Property;_Channels;Channels;2;3;[Header];[NoScaleOffset];[SingleLineTexture];Create;True;1;Trim Sheet _____________________________________________;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.StepOpNode;198;-3827.346,-990.9056;Inherit;False;2;0;FLOAT;0.1;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;199;-4195.346,-1022.906;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StepOpNode;201;-4419.346,-942.9056;Inherit;False;2;0;FLOAT;0.2;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;202;-4643.346,-1134.906;Inherit;False;153;Vinyl Material;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;203;-2547.346,-94.90564;Inherit;False;175;ParallaxUV;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;156;-64,560;Inherit;False;122;Metalness;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;160;-64,640;Inherit;False;121;Smoothness;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;179;-51.34625,481.0944;Inherit;False;100;Glow;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode;172;-2131.346,945.0944;Inherit;False;Property;_ParallaxCropDistance;Parallax Crop Distance;27;1;[Header];Create;True;1;Parallax Occlusion Mapping __________________________;0;0;False;0;False;5;4;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;82;-2371.346,-1070.906;Inherit;False;PatternIntensity;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;83;-2035.346,-670.9056;Inherit;False;82;PatternIntensity;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;161;-51.34625,721.0944;Inherit;False;154;Occlusion;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;78;-2835.346,-654.9056;Inherit;False;191;Trim_Occlusion;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.VertexToFragmentNode;204;-2768,-1760;Inherit;False;False;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;100;-2371.346,-1758.906;Inherit;False;Glow;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.CustomExpressionNode;96;-3363.346,-1758.906;Float;False;return (In.x*C0+In.y*C1+In.z*C2).xyz@$//float4 arr[4]={C0,C1,C2,C2}@$//return arr[In]@;3;Create;4;False;C0;FLOAT4;0,0,0,0;In;;Float;False;False;C1;FLOAT4;0,0,0,0;In;;Float;False;False;C2;FLOAT4;0,0,0,0;In;;Float;False;True;In;FLOAT4;0,0,0,0;In;;Float;False;Merge 3 Colors;True;False;0;;False;4;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0,0,0,0;False;2;FLOAT4;0,0,0,0;False;3;FLOAT4;0,0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;215;224,384;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;ExtraPrePass;0;0;ExtraPrePass;5;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;0;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;0;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;217;224,384;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;ShadowCaster;0;2;ShadowCaster;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;False;False;True;False;False;False;False;0;False;;False;False;False;False;False;False;False;False;False;True;1;False;;True;3;False;;False;True;1;LightMode=ShadowCaster;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;218;224,384;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;DepthOnly;0;3;DepthOnly;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;False;False;True;True;False;False;False;0;False;;False;False;False;False;False;False;False;False;False;True;1;False;;False;False;True;1;LightMode=DepthOnly;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;219;224,384;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;Meta;0;4;Meta;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=Meta;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;220;224,384;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;Universal2D;0;5;Universal2D;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;1;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;False;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;1;LightMode=Universal2D;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;221;224,384;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;DepthNormals;0;6;DepthNormals;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;0;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;False;;True;3;False;;False;True;1;LightMode=DepthNormals;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;222;224,384;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;GBuffer;0;7;GBuffer;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;1;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;1;LightMode=UniversalGBuffer;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;223;224,384;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;SceneSelectionPass;0;8;SceneSelectionPass;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;2;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=SceneSelectionPass;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;224;224,384;Float;False;False;-1;3;UnityEditor.ShaderGraphLitGUI;0;1;New Amplify Shader;94348b07e5e8bab40bd6c8a1e3df54cd;True;ScenePickingPass;0;9;ScenePickingPass;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;1;LightMode=Picking;False;False;0;;0;0;Standard;0;False;0
Node;AmplifyShaderEditor.TemplateMultiPassMasterNode;216;224,384;Float;False;True;-1;3;UnityEditor.ShaderGraphLitGUI;0;12;Drakkar/URP/MultiMaterial/MultiMaterial Trim;94348b07e5e8bab40bd6c8a1e3df54cd;True;Forward;0;1;Forward;21;False;False;False;False;False;False;False;False;False;False;False;False;True;0;False;;False;True;0;False;;False;False;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;True;True;1;False;;True;3;False;;True;False;0;False;;0;False;;True;4;RenderPipeline=UniversalPipeline;RenderType=Opaque=RenderType;Queue=Geometry=Queue=0;UniversalMaterialType=Lit;True;5;True;12;all;0;False;True;1;1;False;;0;False;;1;1;False;;0;False;;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;True;True;True;0;False;;False;False;False;False;False;False;False;True;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;True;1;False;;True;3;False;;True;True;0;False;;0;False;;True;1;LightMode=UniversalForward;False;False;0;;0;0;Standard;43;Lighting Model;0;638748759647006384;Workflow;1;638748759674032897;Surface;0;0;  Refraction Model;0;0;  Blend;0;0;Two Sided;1;0;Alpha Clipping;0;638748756894095352;  Use Shadow Threshold;0;0;Fragment Normal Space,InvertActionOnDeselection;0;0;Forward Only;0;0;Transmission;0;0;  Transmission Shadow;0.5,False,;0;Translucency;0;0;  Translucency Strength;1,False,;0;  Normal Distortion;0.5,False,;0;  Scattering;2,False,;0;  Direct;0.9,False,;0;  Ambient;0.1,False,;0;  Shadow;0.5,False,;0;Cast Shadows;1;0;Receive Shadows;1;0;Receive SSAO;1;0;GPU Instancing;1;0;LOD CrossFade;0;638748757239616767;Built-in Fog;1;0;_FinalColorxAlpha;0;0;Meta Pass;1;0;Override Baked GI;0;0;Extra Pre Pass;0;0;Tessellation;0;0;  Phong;0;0;  Strength;0.5,False,;0;  Type;0;0;  Tess;16,False,;0;  Min;10,False,;0;  Max;25,False,;0;  Edge Length;16,False,;0;  Max Displacement;25,False,;0;Write Depth;0;0;  Early Z;0;0;Vertex Position,InvertActionOnDeselection;1;0;Debug Display;0;638748758487371037;Clear Coat;0;0;0;10;False;True;True;True;True;True;True;True;True;True;False;;False;0
WireConnection;13;0;11;3
WireConnection;33;0;31;0
WireConnection;33;1;32;0
WireConnection;34;0;33;0
WireConnection;35;0;34;0
WireConnection;37;0;36;1
WireConnection;38;0;36;0
WireConnection;41;1;178;0
WireConnection;41;2;39;0
WireConnection;41;3;40;0
WireConnection;42;0;40;0
WireConnection;44;0;41;0
WireConnection;45;0;184;0
WireConnection;45;1;185;0
WireConnection;45;7;186;0
WireConnection;46;0;42;0
WireConnection;47;0;43;4
WireConnection;47;1;172;0
WireConnection;48;0;44;0
WireConnection;48;1;45;4
WireConnection;50;0;43;0
WireConnection;51;0;47;0
WireConnection;51;1;46;0
WireConnection;52;0;171;0
WireConnection;52;1;50;0
WireConnection;53;0;44;0
WireConnection;53;1;48;0
WireConnection;53;2;51;0
WireConnection;54;0;52;0
WireConnection;54;1;53;0
WireConnection;54;2;155;0
WireConnection;56;0;54;0
WireConnection;58;0;55;0
WireConnection;59;1;57;0
WireConnection;60;0;58;0
WireConnection;62;0;151;0
WireConnection;62;1;152;0
WireConnection;63;0;11;2
WireConnection;68;0;182;0
WireConnection;70;0;68;1
WireConnection;71;0;68;2
WireConnection;72;0;11;4
WireConnection;72;1;199;0
WireConnection;72;2;198;0
WireConnection;76;2;72;0
WireConnection;79;8;73;0
WireConnection;79;9;74;0
WireConnection;79;11;76;0
WireConnection;81;0;79;0
WireConnection;85;2;11;1
WireConnection;86;1;84;0
WireConnection;86;2;83;0
WireConnection;88;8;76;0
WireConnection;92;1;85;0
WireConnection;93;0;86;0
WireConnection;94;0;88;0
WireConnection;95;0;68;0
WireConnection;98;0;93;0
WireConnection;103;0;99;0
WireConnection;103;2;97;0
WireConnection;104;0;168;0
WireConnection;104;1;103;0
WireConnection;104;2;102;0
WireConnection;108;1;203;0
WireConnection;108;7;37;0
WireConnection;109;1;108;0
WireConnection;109;2;188;0
WireConnection;111;0;68;3
WireConnection;112;7;76;0
WireConnection;113;0;110;0
WireConnection;115;0;112;5
WireConnection;116;0;113;3
WireConnection;116;1;114;0
WireConnection;118;1;86;0
WireConnection;119;0;117;0
WireConnection;119;1;116;0
WireConnection;119;2;118;0
WireConnection;120;0;104;0
WireConnection;121;0;119;0
WireConnection;122;0;112;0
WireConnection;123;0;56;1
WireConnection;124;0;51;0
WireConnection;125;0;45;4
WireConnection;126;0;124;0
WireConnection;126;1;125;0
WireConnection;133;0;126;0
WireConnection;134;2;176;0
WireConnection;134;3;60;0
WireConnection;135;1;136;0
WireConnection;135;0;203;0
WireConnection;136;0;137;0
WireConnection;138;0;169;0
WireConnection;139;0;120;0
WireConnection;151;1;134;0
WireConnection;152;0;61;0
WireConnection;153;0;62;0
WireConnection;154;0;78;0
WireConnection;165;2;49;0
WireConnection;165;3;166;0
WireConnection;166;0;173;0
WireConnection;167;0;123;0
WireConnection;169;10;76;0
WireConnection;169;8;140;0
WireConnection;174;0;173;0
WireConnection;175;0;194;0
WireConnection;176;0;59;0
WireConnection;176;1;177;0
WireConnection;177;0;55;0
WireConnection;183;0;189;0
WireConnection;183;1;165;0
WireConnection;183;2;53;0
WireConnection;183;3;53;0
WireConnection;187;0;197;4
WireConnection;190;0;197;3
WireConnection;191;0;197;1
WireConnection;192;0;109;0
WireConnection;193;0;197;2
WireConnection;194;0;162;0
WireConnection;194;1;163;0
WireConnection;194;7;164;0
WireConnection;194;2;183;0
WireConnection;194;3;174;0
WireConnection;194;8;56;0
WireConnection;194;9;56;1
WireConnection;194;10;56;2
WireConnection;194;4;195;0
WireConnection;197;0;38;0
WireConnection;197;1;203;0
WireConnection;197;7;37;0
WireConnection;198;1;199;0
WireConnection;199;0;202;0
WireConnection;199;1;196;0
WireConnection;199;2;201;0
WireConnection;201;1;196;0
WireConnection;82;0;79;6
WireConnection;204;0;96;0
WireConnection;100;0;204;0
WireConnection;96;0;89;0
WireConnection;96;1;90;0
WireConnection;96;2;91;0
WireConnection;96;3;92;0
WireConnection;216;0;170;0
WireConnection;216;1;158;0
WireConnection;216;2;179;0
WireConnection;216;3;156;0
WireConnection;216;4;160;0
WireConnection;216;5;161;0
ASEEND*/
//CHKSM=ED41C43DB9E51528C1414F418E54500EFCE473F7