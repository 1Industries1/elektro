// Made with Amplify Shader Editor v1.9.8.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Drakkar/Built-In/MultiMaterial/MultiMaterial Trim"
{
	Properties
	{
		[Enum(UnityEngine.Rendering.CullMode)]_CullMode("Cull Mode", Int) = 0
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
		_Occlusion("Occlusion", Float) = 0
		[Header(Parallax Occlusion Mapping __________________________)]_ParallaxOptDistance("Parallax Opt Distance", Float) = 5
		_AngleTolerance1("Angle Tolerance", Range( 0.1 , 0.99)) = 0.5
		_ParallaxMaxDistance1("Parallax Max Distance", Float) = 8
		_ParallaxMinDistance1("Parallax Min Distance", Float) = 5
		_ParallaxOffset1("Parallax Offset", Float) = 0.1
		_QualityMultipliers1("Quality Multipliers", Vector) = (1,1,1,0)
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] _texcoord3( "", 2D ) = "white" {}
		[HideInInspector] _texcoord2( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Opaque"  "Queue" = "Geometry+0" "IgnoreProjector" = "True" "IsEmissive" = "true"  }
		Cull [_CullMode]
		CGPROGRAM
		#include "UnityShaderVariables.cginc"
		#pragma target 3.0
		#define ASE_VERSION 19801
		#pragma instancing_options lodfade
		#define ASE_USING_SAMPLING_MACROS 1
		#if defined(SHADER_API_D3D11) || defined(SHADER_API_XBOXONE) || defined(UNITY_COMPILER_HLSLCC) || defined(SHADER_API_PSSL) || (defined(SHADER_TARGET_SURFACE_ANALYSIS) && !defined(SHADER_TARGET_SURFACE_ANALYSIS_MOJOSHADER))//ASE Sampler Macros
		#define SAMPLE_TEXTURE2D(tex,samplerTex,coord) tex.Sample(samplerTex,coord)
		#define SAMPLE_TEXTURE2D_LOD(tex,samplerTex,coord,lod) tex.SampleLevel(samplerTex,coord, lod)
		#define SAMPLE_TEXTURE2D_BIAS(tex,samplerTex,coord,bias) tex.SampleBias(samplerTex,coord,bias)
		#define SAMPLE_TEXTURE2D_GRAD(tex,samplerTex,coord,ddx,ddy) tex.SampleGrad(samplerTex,coord,ddx,ddy)
		#else//ASE Sampling Macros
		#define SAMPLE_TEXTURE2D(tex,samplerTex,coord) tex2D(tex,coord)
		#define SAMPLE_TEXTURE2D_LOD(tex,samplerTex,coord,lod) tex2Dlod(tex,float4(coord,0,lod))
		#define SAMPLE_TEXTURE2D_BIAS(tex,samplerTex,coord,bias) tex2Dbias(tex,float4(coord,0,bias))
		#define SAMPLE_TEXTURE2D_GRAD(tex,samplerTex,coord,ddx,ddy) tex2Dgrad(tex,coord,ddx,ddy)
		#endif//ASE Sampling Macros

		#pragma surface surf Standard keepalpha noshadow novertexlights nodynlightmap nodirlightmap nofog noforwardadd dithercrossfade vertex:vertexDataFunc  addshadow
		struct Input
		{
			float2 uv_texcoord;
			float3 viewDir;
			INTERNAL_DATA
			float vertexToFrag346;
			float3 worldPos;
			float3 worldNormal;
			float vertexToFrag99;
			float4 vertexColor : COLOR;
			float2 uv3_texcoord3;
			float2 uv2_texcoord2;
		};

		uniform int _CullMode;
		UNITY_DECLARE_TEX2D_NOSAMPLER(_Normal);
		UNITY_DECLARE_TEX2D_NOSAMPLER(_TrimSheet);
		uniform float _ParallaxOffset1;
		uniform float _AngleTolerance1;
		uniform float _ParallaxMaxDistance1;
		uniform float _ParallaxMinDistance1;
		SamplerState sampler_TrimSheet;
		uniform float4 ParallaxOffsets;
		uniform float _ParallaxOptDistance;
		uniform float3 _QualityMultipliers1;
		uniform float4 _TrimSheet_ST;
		uniform float _Occlusion;
		UNITY_DECLARE_TEX2D_NOSAMPLER(_VinylTrimsheet);
		SamplerState sampler_VinylTrimsheet;
		uniform float _VinylSelect;
		uniform float _VinylMaterial;
		uniform float4 _Color0;
		uniform float4 _Color1;
		uniform float4 _Color2;
		uniform float4 _Color3;
		UNITY_DECLARE_TEX2D_NOSAMPLER(_SurfaceTexture);
		uniform float _Tiling;
		uniform float _Blend;
		SamplerState sampler_SurfaceTexture;
		uniform half4 _ColorOffset;
		uniform half4 _Pattern;
		uniform half4 _PatternIntensity;
		uniform float4 _Emissive1;
		uniform float4 _Emissive2;
		uniform float4 _Emissive3;
		uniform float4 _Metalness;
		uniform float4 _SmoothnessOffset;


		inline float2 POM( UNITY_DECLARE_TEX2D_NOSAMPLER(heightMap), SamplerState samplerheightMap, float2 uvs, float2 dx, float2 dy, float3 normalWorld, float3 viewWorld, float3 viewDirTan, int minSamples, int maxSamples, int sidewallSteps, float parallax, float refPlane, float2 tilling, float2 curv, int index )
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
			 	currHeight = SAMPLE_TEXTURE2D_GRAD( heightMap, samplerheightMap, uvs + currTexOffset, dx, dy ).g;
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
			 	newHeight = SAMPLE_TEXTURE2D_GRAD( heightMap, samplerheightMap, uvs + finalTexOffset, dx, dy ).g;
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


		inline float VectorIndex1_g248( float4 Vector, float Index )
		{
			return Vector[Index];
		}


		float ToMaterial551( float In )
		{
			float arr[4]={0.25,.5,.75,1};
			return arr[In];
		}


		inline float FindIndex1_g250( float In )
		{
			return (In-0.009)*4;;
		}


		float4 MergeColors6_g257( float In, float4 C0, float4 C1, float4 C2, float4 C3 )
		{
			//return In.x*C0+In.y*C1+In.z*C2+In.w*C3;
			float4 arr[4]={C0,C1,C2,C3};
			return arr[In];
		}


		inline float VectorIndex1_g256( float4 Vector, float Index )
		{
			return Vector[Index];
		}


		inline float VectorIndex1_g252( float4 Vector, float Index )
		{
			return Vector[Index];
		}


		inline float VectorIndex1_g253( float4 Vector, float Index )
		{
			return Vector[Index];
		}


		float4 BlendVector1_g258( float In )
		{
			float4 _in=saturate(float4(In,In-0.25,In-0.5,In-0.75)*4);
			_in.xyz-=_in.yzw;
			return (_in);
		}


		float3 Merge3Colors44( float4 C0, float4 C1, float4 C2, float4 In )
		{
			return (In.x*C0+In.y*C1+In.z*C2).xyz;
			//float4 arr[4]={C0,C1,C2,C2};
			//return arr[In];
		}


		inline float VectorIndex1_g261( float4 Vector, float Index )
		{
			return Vector[Index];
		}


		inline float VectorIndex1_g260( float4 Vector, float Index )
		{
			return Vector[Index];
		}


		void vertexDataFunc( inout appdata_full v, out Input o )
		{
			UNITY_INITIALIZE_OUTPUT( Input, o );
			float3 ase_positionWS = mul( unity_ObjectToWorld, v.vertex );
			o.vertexToFrag346 = distance( _WorldSpaceCameraPos , ase_positionWS );
			float GreenChannel151 = v.color.g;
			o.vertexToFrag99 = ( 1.0 - saturate( ( GreenChannel151 * _Occlusion ) ) );
		}

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float2 TexUV441 = i.uv_texcoord;
			float temp_output_1_0_g172 = 0.0;
			float CameraDistance349 = i.vertexToFrag346;
			float temp_output_1_0_g171 = _ParallaxMaxDistance1;
			float temp_output_450_0 = saturate( ( ( CameraDistance349 - temp_output_1_0_g171 ) / ( _ParallaxMinDistance1 - temp_output_1_0_g171 ) ) );
			float4 tex2DNode451 = SAMPLE_TEXTURE2D( _TrimSheet, sampler_TrimSheet, TexUV441 );
			float temp_output_482_0 = step( ( ParallaxOffsets.w + _ParallaxOptDistance ) , CameraDistance349 );
			float lerpResult483 = lerp( temp_output_450_0 , ( temp_output_450_0 * tex2DNode451.a ) , temp_output_482_0);
			float3 break463 = ( ( float3(8,16,4) + (ParallaxOffsets).xyz ) * lerpResult483 * _QualityMultipliers1 );
			float3 ase_normalWS = WorldNormalVector( i, float3( 0, 0, 1 ) );
			float3 ase_positionWS = i.worldPos;
			float3 ase_viewVectorWS = ( _WorldSpaceCameraPos.xyz - ase_positionWS );
			float3 ase_viewDirWS = normalize( ase_viewVectorWS );
			float2 OffsetPOM464 = POM( _TrimSheet, sampler_TrimSheet, TexUV441, ddx(TexUV441), ddy(TexUV441), ase_normalWS, ase_viewDirWS, i.viewDir, (int)break463.x, (int)break463.y, (int)break463.z, ( _ParallaxOffset1 * ( ( i.viewDir.z - temp_output_1_0_g172 ) / ( _AngleTolerance1 - temp_output_1_0_g172 ) ) * lerpResult483 * lerpResult483 ), 0.5, _TrimSheet_ST.xy, float2(1,1), 0 );
			float2 ParallaxUV470 = OffsetPOM464;
			float4 tex2DNode301 = SAMPLE_TEXTURE2D( _TrimSheet, sampler_TrimSheet, ParallaxUV470 );
			float Trim_Normal_Mask408 = tex2DNode301.a;
			float3 lerpResult405 = lerp( float3( 0,0,1 ) , UnpackNormal( SAMPLE_TEXTURE2D( _Normal, sampler_TrimSheet, ParallaxUV470 ) ) , Trim_Normal_Mask408);
			float3 NormalMap188 = lerpResult405;
			o.Normal = NormalMap188;
			float Trim_Occlusion203 = tex2DNode301.r;
			float Occlusion156 = ( i.vertexToFrag99 * Trim_Occlusion203 );
			float4 Vector1_g248 = ( SAMPLE_TEXTURE2D( _VinylTrimsheet, sampler_VinylTrimsheet, i.uv3_texcoord3 ) * saturate( _VinylSelect ) );
			float Index1_g248 = floor( ( _VinylSelect - 1.0 ) );
			float localVectorIndex1_g248 = VectorIndex1_g248( Vector1_g248 , Index1_g248 );
			float In551 = _VinylMaterial;
			float localToMaterial551 = ToMaterial551( In551 );
			float Vinyl_Material516 = ( step( 0.8 , localVectorIndex1_g248 ) * localToMaterial551 );
			float Trim_Material204 = tex2DNode301.b;
			float lerpResult556 = lerp( Vinyl_Material516 , Trim_Material204 , step( 0.1 , Trim_Material204 ));
			float lerpResult557 = lerp( i.vertexColor.a , lerpResult556 , step( 0.1 , lerpResult556 ));
			float In1_g250 = lerpResult557;
			float localFindIndex1_g250 = FindIndex1_g250( In1_g250 );
			float temp_output_573_0 = localFindIndex1_g250;
			float In6_g257 = temp_output_573_0;
			float4 C06_g257 = _Color0;
			float4 C16_g257 = _Color1;
			float4 C26_g257 = _Color2;
			float4 C36_g257 = _Color3;
			float4 localMergeColors6_g257 = MergeColors6_g257( In6_g257 , C06_g257 , C16_g257 , C26_g257 , C36_g257 );
			float4 Color163 = ( Occlusion156 * localMergeColors6_g257 );
			float2 temp_cast_8 = (_Tiling).xx;
			float2 temp_cast_9 = (_Blend).xx;
			float2 uv2_TexCoord7_g249 = i.uv2_texcoord2 * temp_cast_8 + temp_cast_9;
			float4 tex2DNode10_g249 = SAMPLE_TEXTURE2D( _SurfaceTexture, sampler_SurfaceTexture, uv2_TexCoord7_g249 );
			float4 break285 = tex2DNode10_g249;
			float Texture_R167 = break285.x;
			float4 Vector1_g256 = _ColorOffset;
			float Index1_g256 = temp_output_573_0;
			float localVectorIndex1_g256 = VectorIndex1_g256( Vector1_g256 , Index1_g256 );
			float ColorOffset169 = localVectorIndex1_g256;
			float lerpResult39 = lerp( Texture_R167 , 1.0 , ColorOffset169);
			float Pattern_1_Source180 = break285.y;
			float Pattern_2_Source181 = break285.z;
			float4 Vector1_g252 = _Pattern;
			float temp_output_11_0_g251 = temp_output_573_0;
			float Index1_g252 = temp_output_11_0_g251;
			float localVectorIndex1_g252 = VectorIndex1_g252( Vector1_g252 , Index1_g252 );
			float lerpResult5_g251 = lerp( Pattern_1_Source180 , Pattern_2_Source181 , localVectorIndex1_g252);
			float Pattern177 = lerpResult5_g251;
			float4 Vector1_g253 = _PatternIntensity;
			float Index1_g253 = temp_output_11_0_g251;
			float localVectorIndex1_g253 = VectorIndex1_g253( Vector1_g253 , Index1_g253 );
			float PatternIntensity175 = localVectorIndex1_g253;
			float lerpResult54 = lerp( 0.5 , Pattern177 , PatternIntensity175);
			float PatternColor368 = ( lerpResult54 * 2.0 );
			float4 FinalColor377 = saturate( ( Color163 * lerpResult39 * PatternColor368 ) );
			o.Albedo = FinalColor377.xyz;
			float4 C044 = _Emissive1;
			float4 C144 = _Emissive2;
			float4 C244 = _Emissive3;
			float In1_g258 = i.vertexColor.r;
			float4 localBlendVector1_g258 = BlendVector1_g258( In1_g258 );
			float4 In44 = step( float4( 0.8,0.8,0.8,0 ) , localBlendVector1_g258 );
			float3 localMerge3Colors44 = Merge3Colors44( C044 , C144 , C244 , In44 );
			float3 Glow161 = localMerge3Colors44;
			float3 Emissive372 = Glow161;
			o.Emission = Emissive372;
			float4 Vector1_g261 = _Metalness;
			float temp_output_7_0_g259 = temp_output_573_0;
			float Index1_g261 = temp_output_7_0_g259;
			float localVectorIndex1_g261 = VectorIndex1_g261( Vector1_g261 , Index1_g261 );
			float Metalness173 = localVectorIndex1_g261;
			o.Metallic = Metalness173;
			float4 Vector1_g260 = _SmoothnessOffset;
			float Index1_g260 = temp_output_7_0_g259;
			float localVectorIndex1_g260 = VectorIndex1_g260( Vector1_g260 , Index1_g260 );
			float SmoothnessOffset171 = localVectorIndex1_g260;
			float Smoothness_Source363 = break285.w;
			float Smoothness379 = ( SmoothnessOffset171 + ( Color163.w * Smoothness_Source363 ) + ( 0.5 - lerpResult54 ) );
			o.Smoothness = Smoothness379;
			o.Occlusion = Occlusion156;
			o.Alpha = 1;
		}

		ENDCG
	}
	CustomEditor "AmplifyShaderEditor.MaterialInspector"
}
/*ASEBEGIN
Version=19801
Node;AmplifyShaderEditor.CommentaryNode;355;-2944,224;Inherit;False;926;377;Camera Distance;5;349;346;358;345;344;;1,1,1,1;0;0
Node;AmplifyShaderEditor.WorldSpaceCameraPos;344;-2912,272;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.WorldPosInputsNode;358;-2816,448;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.DistanceOpNode;345;-2624,368;Inherit;False;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT;0
Node;AmplifyShaderEditor.VertexToFragmentNode;346;-2480,368;Inherit;False;False;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;207;-1916,240;Inherit;False;1701.329;681.2104;Trim Sheet + Parallax Occlusion Mapping;17;274;392;188;405;187;409;408;203;204;301;362;479;472;441;473;308;185;;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;480;-1920,944;Inherit;False;2763.459;970.9841;Parallax Mapping;46;476;501;499;478;477;462;483;464;468;469;470;463;460;461;467;457;459;455;456;482;454;452;490;488;451;450;485;481;484;475;471;474;449;444;446;445;498;466;465;502;503;505;508;509;511;552;;1,1,1,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;349;-2240,368;Inherit;False;CameraDistance;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TexturePropertyNode;308;-1808,304;Inherit;True;Property;_TrimSheet;TrimSheet;6;2;[Header];[SingleLineTexture];Create;True;1;Trim Sheet _____________________________________________;0;0;False;0;False;None;3380e71b2dcb12340b010731d7bf8b64;False;white;Auto;Texture2D;-1;0;2;SAMPLER2D;0;SAMPLERSTATE;1
Node;AmplifyShaderEditor.TextureCoordinatesNode;185;-1888,736;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RegisterLocalVarNode;473;-1392,368;Inherit;False;SS;-1;True;1;0;SAMPLERSTATE;;False;1;SAMPLERSTATE;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;472;-1392,288;Inherit;False;Tex;-1;True;1;0;SAMPLER2D;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.RangedFloatNode;445;-1872,1408;Inherit;False;Property;_ParallaxMaxDistance1;Parallax Max Distance;30;0;Create;True;0;0;0;False;0;False;8;5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;444;-1840,1488;Inherit;False;Property;_ParallaxMinDistance1;Parallax Min Distance;31;0;Create;True;0;0;0;False;0;False;5;2;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;441;-1616,736;Inherit;False;TexUV;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;446;-1840,1568;Inherit;False;349;CameraDistance;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;449;-1504,1488;Inherit;False;Inverse Lerp;-1;;171;09cbe79402f023141a4dc1fddd4c9511;0;3;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;474;-1856,1648;Inherit;False;472;Tex;1;0;OBJECT;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.GetLocalVarNode;471;-1856,1728;Inherit;False;441;TexUV;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.GetLocalVarNode;475;-1856,1808;Inherit;False;473;SS;1;0;OBJECT;;False;1;SAMPLERSTATE;0
Node;AmplifyShaderEditor.WireNode;484;-1264,1632;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector4Node;485;-1184,1328;Inherit;False;Global;ParallaxOffsets;Parallax Offsets;25;0;Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,0;0;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;481;-1152,1712;Inherit;False;Property;_ParallaxOptDistance;Parallax Opt Distance;28;1;[Header];Create;True;1;Parallax Occlusion Mapping __________________________;0;0;False;0;False;5;5;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;450;-1328,1488;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;451;-1552,1664;Inherit;True;Property;_Channels2;Channels;2;3;[Header];[NoScaleOffset];[SingleLineTexture];Create;True;1;Trim Sheet _____________________________________________;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.WireNode;488;-1168,1776;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;490;-880,1696;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;452;-1152,1552;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;459;-1184,1024;Inherit;False;Property;_AngleTolerance1;Angle Tolerance;29;0;Create;True;1;Header(Parallax Occlusion Mapping ___________________________);0;0;False;0;False;0.5;0.25;0.1;0.99;0;1;FLOAT;0
Node;AmplifyShaderEditor.ComponentMaskNode;552;-832,1456;Inherit;False;True;True;True;False;1;0;FLOAT4;0,0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.StepOpNode;482;-752,1728;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector3Node;454;-720,1312;Inherit;False;Constant;_ParallaxNearValues1;Parallax Near Values;23;0;Create;True;0;0;0;False;0;False;8,16,4;8,16,4;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.CommentaryNode;517;-3691,624;Inherit;False;1741.525;475.2219;Vinyls;15;540;516;545;551;544;546;530;534;515;522;533;528;553;554;603;;1,1,1,1;0;0
Node;AmplifyShaderEditor.ViewDirInputsCoordNode;455;-1088,1152;Inherit;False;Tangent;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.RangedFloatNode;467;-816,992;Inherit;False;Property;_ParallaxOffset1;Parallax Offset;32;0;Create;True;0;0;0;False;0;False;0.1;0.02;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;460;-752,1184;Inherit;False;Inverse Lerp;-1;;172;09cbe79402f023141a4dc1fddd4c9511;0;3;1;FLOAT;0;False;2;FLOAT;0.7;False;3;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode;502;-800,1152;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleAddOpNode;456;-224,1296;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LerpOp;483;-576,1504;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector3Node;457;-400,1552;Inherit;False;Property;_QualityMultipliers1;Quality Multipliers;33;0;Create;True;0;0;0;False;0;False;1,1,1;1,1,1;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.RangedFloatNode;540;-3664,944;Inherit;False;Property;_VinylSelect;Vinyl Select;8;1;[IntRange];Create;True;0;0;0;False;0;False;0;4;0;4;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;462;-368,992;Inherit;False;4;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode;501;-352,1152;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;461;-16,1296;Inherit;False;3;3;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;2;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.WireNode;553;-3392,928;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.BreakToComponentsNode;463;144,1296;Inherit;False;FLOAT3;1;0;FLOAT3;0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.GetLocalVarNode;478;-144,1088;Inherit;False;473;SS;1;0;OBJECT;;False;1;SAMPLERSTATE;0
Node;AmplifyShaderEditor.WireNode;499;-192,1136;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;476;-176,1008;Inherit;False;472;Tex;1;0;OBJECT;;False;1;SAMPLER2D;0
Node;AmplifyShaderEditor.WireNode;503;112,1248;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;477;48,992;Inherit;False;441;TexUV;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SaturateNode;533;-3216,864;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;522;-3456,688;Inherit;False;2;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleSubtractOpNode;528;-3360,944;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;515;-3200,672;Inherit;True;Property;_VinylTrimsheet;Vinyl Trimsheet;10;2;[NoScaleOffset];[SingleLineTexture];Create;True;0;0;0;False;0;False;-1;None;ebf3742c33d13f34f9c5c8af8bfa3bd4;True;0;False;white;LockedToTexture2D;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.WireNode;534;-2944,864;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ParallaxOcclusionMappingNode;464;368,1136;Inherit;False;1;8;False;;16;False;;3;0.02;0.5;False;1,1;False;1,1;11;0;FLOAT2;0,0;False;1;SAMPLER2D;;False;7;SAMPLERSTATE;;False;2;FLOAT;0.02;False;3;FLOAT3;0,0,0;False;8;INT;0;False;9;INT;0;False;10;INT;0;False;4;FLOAT;0;False;5;FLOAT2;0,0;False;6;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;470;624,1136;Inherit;False;ParallaxUV;-1;True;1;0;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.FloorOpNode;554;-3184,944;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;530;-2880,672;Inherit;False;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.GetLocalVarNode;479;-1584,656;Inherit;False;470;ParallaxUV;1;0;OBJECT;;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;546;-3664,1024;Inherit;False;Property;_VinylMaterial;Vinyl Material;9;1;[IntRange];Create;True;0;0;0;False;0;False;0;3;0;3;0;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;603;-2704,864;Inherit;False;Vector Select;-1;;248;b444b7286dd57b441b67a33f5fa7e6f9;0;2;2;FLOAT4;0,0,0,0;False;3;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;301;-960,304;Inherit;True;Property;_Channels;Channels;2;3;[Header];[NoScaleOffset];[SingleLineTexture];Create;True;1;Trim Sheet _____________________________________________;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.CustomExpressionNode;551;-2736,1024;Inherit;False;float arr[4]={0.25,.5,.75,1}@$return arr[In]@;1;Create;1;True;In;FLOAT;0;In;;Inherit;False;To Material;True;False;0;;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StepOpNode;544;-2496,832;Inherit;False;2;0;FLOAT;0.8;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;145;-3712,-1040;Inherit;False;2575.833;972.4527;Multi Material;29;163;175;177;173;171;169;161;393;391;127;44;140;43;42;41;158;183;182;404;151;518;519;28;276;556;557;573;596;562;;1,1,1,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;204;-448,448;Inherit;False;Trim_Material;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;545;-2336,832;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.VertexColorNode;28;-3136,-608;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.GetLocalVarNode;276;-3696,-240;Inherit;False;204;Trim_Material;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;155;-2624,-48;Inherit;False;1493.76;242.1955;Occlusion;9;92;156;205;206;99;96;139;97;154;;1,1,1,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;151;-2480,-560;Inherit;False;GreenChannel;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;516;-2160,832;Inherit;False;Vinyl Material;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;518;-3664,-368;Inherit;False;516;Vinyl Material;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.StepOpNode;519;-3456,-160;Inherit;False;2;0;FLOAT;0.1;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;190;48,-96;Inherit;False;734.8041;401.9758;Surface UV Coordinates;6;363;167;181;180;285;504;;1,1,1,1;0;0
Node;AmplifyShaderEditor.RangedFloatNode;92;-2592,112;Inherit;False;Property;_Occlusion;Occlusion;27;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;154;-2592,16;Inherit;False;151;GreenChannel;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;556;-3296,-256;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;504;80,80;Inherit;False;Texturing Options;1;;249;c9cf78d015b579f4081a5353746cbd35;0;0;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;97;-2352,16;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.BreakToComponentsNode;285;320,80;Inherit;False;FLOAT4;1;0;FLOAT4;0,0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.StepOpNode;404;-2848,-224;Inherit;False;2;0;FLOAT;0.1;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;139;-2192,16;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;180;512,48;Inherit;False;Pattern_1_Source;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;181;512,128;Inherit;False;Pattern_2_Source;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;557;-2688,-320;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;96;-2032,16;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;203;-448,288;Inherit;False;Trim_Occlusion;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;182;-2064,-432;Inherit;False;180;Pattern_1_Source;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;183;-2064,-352;Inherit;False;181;Pattern_2_Source;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;573;-2432,-320;Inherit;False;Selective Index;-1;;250;a387ce4cd4b83ea4ab83f0095ae73bcf;0;1;2;FLOAT;0.25;False;1;FLOAT;0
Node;AmplifyShaderEditor.VertexToFragmentNode;99;-1856,16;Inherit;False;False;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;206;-1856,112;Inherit;False;203;Trim_Occlusion;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;590;-1760,-368;Inherit;False;Select Multi Pattern;22;;251;02f76630bd4cfa747a9e2e768fdae124;0;3;8;FLOAT;0;False;9;FLOAT;0;False;11;FLOAT;0;False;2;FLOAT;0;FLOAT;6
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;205;-1536,80;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;366;-1072,-112;Inherit;False;842.9048;309.95;Pattern;6;368;67;371;54;178;176;;1,1,1,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;177;-1392,-384;Inherit;False;Pattern;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;175;-1392,-304;Inherit;False;PatternIntensity;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;156;-1360,80;Inherit;False;Occlusion;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;176;-1056,96;Inherit;False;175;PatternIntensity;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;178;-1024,16;Inherit;False;177;Pattern;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;158;-2400,-160;Inherit;False;156;Occlusion;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;54;-800,0;Inherit;False;3;0;FLOAT;0.5;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;592;-2064,-640;Inherit;False;Select Multi Color Offset Dirt;25;;255;f3acb7224ba615e49b5ae61221ffeda0;0;1;8;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;596;-2064,-192;Inherit;False;Select Multi Color;11;;257;89bec0ff38b4baf4282a7e2d406f3e25;0;3;7;FLOAT4;0,0,0,0;False;10;FLOAT;0;False;8;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.FunctionNode;562;-2896,-592;Inherit;False;Selective Vector;-1;;258;4fbe5713bd64aff4da67a6c041cc0a5a;0;1;2;FLOAT;0.25;False;1;FLOAT4;0
Node;AmplifyShaderEditor.CommentaryNode;381;48,-416;Inherit;False;1332;290.95;Final Color;8;168;170;39;369;164;142;3;377;;1,1,1,1;0;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;371;-624,64;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;2;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;169;-1392,-640;Inherit;False;ColorOffset;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;167;512,-32;Inherit;False;Texture_R;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode;367;-1088,-464;Inherit;False;1065.752;325.6902;Smoothness;7;172;31;76;166;4;364;379;;1,1,1,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;163;-1392,-192;Inherit;False;Color;-1;True;1;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.ColorNode;41;-3120,-992;Float;False;Property;_Emissive1;Emissive 1;16;1;[HDR];Create;True;0;0;0;False;0;False;0,0,0,0;0,2.197122,11.98431,0.4784314;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ColorNode;42;-2912,-928;Float;False;Property;_Emissive2;Emissive 2;17;1;[HDR];Create;True;0;0;0;False;0;False;0,0,0,0;23.96863,3.195817,0,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ColorNode;43;-2704,-864;Float;False;Property;_Emissive3;Emissive 3;18;1;[HDR];Create;True;0;0;0;False;0;False;0,0,0,0;11.98431,11.98431,11.98431,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.StepOpNode;140;-2624,-624;Inherit;False;2;0;FLOAT4;0.8,0.8,0.8,0;False;1;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.GetLocalVarNode;170;96,-272;Inherit;False;169;ColorOffset;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;368;-464,64;Inherit;False;PatternColor;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;168;96,-352;Inherit;False;167;Texture_R;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;166;-1072,-416;Inherit;False;163;Color;1;0;OBJECT;;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;363;512,208;Inherit;False;Smoothness_Source;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode;593;-2064,-544;Inherit;False;Select Multi Metal Smoothness Offset;19;;259;8d8dbb58d489ec649b1549b7087b270c;0;1;7;FLOAT;0;False;2;FLOAT;0;FLOAT;5
Node;AmplifyShaderEditor.GetLocalVarNode;164;544,-352;Inherit;False;163;Color;1;0;OBJECT;;False;1;FLOAT4;0
Node;AmplifyShaderEditor.GetLocalVarNode;369;512,-240;Inherit;False;368;PatternColor;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;39;336,-320;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;1;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;408;-448,528;Inherit;False;Trim_Normal_Mask;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.BreakToComponentsNode;76;-864,-416;Inherit;False;FLOAT4;1;0;FLOAT4;0,0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.GetLocalVarNode;364;-944,-256;Inherit;False;363;Smoothness_Source;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;171;-1392,-480;Inherit;False;SmoothnessOffset;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.CustomExpressionNode;44;-2384,-992;Float;False;return (In.x*C0+In.y*C1+In.z*C2).xyz@$//float4 arr[4]={C0,C1,C2,C2}@$//return arr[In]@;3;Create;4;False;C0;FLOAT4;0,0,0,0;In;;Float;False;False;C1;FLOAT4;0,0,0,0;In;;Float;False;False;C2;FLOAT4;0,0,0,0;In;;Float;False;True;In;FLOAT4;0,0,0,0;In;;Float;False;Merge 3 Colors;True;False;0;;False;4;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0,0,0,0;False;2;FLOAT4;0,0,0,0;False;3;FLOAT4;0,0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.CommentaryNode;365;544,-896;Inherit;False;1256.382;348.0761;Emission;2;162;372;;1,1,1,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;161;-1392,-992;Inherit;False;Glow;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;3;784,-336;Inherit;False;3;3;0;FLOAT4;0,0,0,0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.GetLocalVarNode;409;-912,832;Inherit;False;408;Trim_Normal_Mask;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;187;-976,624;Inherit;True;Property;_Normal;Normal;7;3;[NoScaleOffset];[Normal];[SingleLineTexture];Create;True;0;0;0;False;0;False;-1;None;179bb7e066b79ff43810d69b4d091e50;True;0;True;bump;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;4;-640,-288;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;172;-704,-384;Inherit;False;171;SmoothnessOffset;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;67;-624,-48;Inherit;False;2;0;FLOAT;0.5;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;162;592,-768;Inherit;False;161;Glow;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LerpOp;405;-608,688;Inherit;False;3;0;FLOAT3;0,0,1;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleAddOpNode;31;-368,-320;Inherit;False;3;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;142;960,-336;Inherit;False;1;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;188;-448,688;Inherit;False;NormalMap;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;379;-240,-320;Inherit;False;Smoothness;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;377;1136,-336;Inherit;False;FinalColor;-1;True;1;0;FLOAT4;0,0,0,0;False;1;FLOAT4;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;173;-1392,-560;Inherit;False;Metalness;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;372;1008,-768;Inherit;False;Emissive;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;127;-2480,-480;Inherit;False;Bluchannel;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.FloorOpNode;468;368,1504;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;505;-512,1728;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode;509;-960,1824;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;508;-304,1760;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StickyNoteNode;391;-2240,-928;Inherit;False;232.2209;100;Glow Color;;0.02800441,1,0,1;Calculate the Glow color$;0;0
Node;AmplifyShaderEditor.StickyNoteNode;392;-1792,512;Inherit;False;293.9666;128.1185;Trim Format;;1,1,1,1;R=Occlusion$G=Heightmap (middle gray base)$B=Material Change$A=Normal Mask;0;0
Node;AmplifyShaderEditor.StickyNoteNode;393;-3136,-752;Inherit;False;293.9666;128.1185;Vertex Color Format;;1,1,1,1;R=Glow Material$G=Occlusion/Dirt$B=Multi-Mesh$A=Multi-Material;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode;274;-448,368;Inherit;False;Trim_Height;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StickyNoteNode;465;-1504,1344;Inherit;False;249.823;108.23;Parallax Quality Parameters;;0,0.9245283,0.009288036,1;Use "ParallaxOffsets" via code to fine tune Parallax quality.;0;0
Node;AmplifyShaderEditor.StickyNoteNode;466;400,1600;Inherit;False;249.823;108.23;Parallax Debug;;0.9254902,0,0.0263739,1;Connect this node to Debug to visualize Sample count.;0;0
Node;AmplifyShaderEditor.StickyNoteNode;498;-112,1632;Inherit;False;249.823;108.23;Parallax Debug;;0.9254902,0,0.0263739,1;Connect this node to Debug to visualize Optimization Distance.;0;0
Node;AmplifyShaderEditor.SimpleDivideOpNode;469;512,1504;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;16;False;1;FLOAT;0
Node;AmplifyShaderEditor.IntNode;79;1680,64;Float;False;Property;_CullMode;Cull Mode;0;1;[Enum];Create;True;0;0;1;UnityEngine.Rendering.CullMode;True;0;False;0;2;False;0;1;INT;0
Node;AmplifyShaderEditor.GetLocalVarNode;174;1360,320;Inherit;False;173;Metalness;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;373;1360,240;Inherit;False;372;Emissive;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode;380;1328,400;Inherit;False;379;Smoothness;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;157;1360,480;Inherit;False;156;Occlusion;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode;378;1360,80;Inherit;False;377;FinalColor;1;0;OBJECT;;False;1;FLOAT4;0
Node;AmplifyShaderEditor.GetLocalVarNode;189;1360,160;Inherit;False;188;NormalMap;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SaturateNode;511;16,1760;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch;362;-1328,736;Inherit;False;Property;_ParallaxMappingOff;Parallax Mapping Off;27;0;Create;True;0;0;0;False;1;Header(Parallax Occlusion Mapping ___________________________);False;1;0;0;False;;Toggle;2;Key0;Key1;Create;False;True;All;9;1;FLOAT2;0,0;False;0;FLOAT2;0,0;False;2;FLOAT2;0,0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT2;0,0;False;6;FLOAT2;0,0;False;7;FLOAT2;0,0;False;8;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;1680,144;Float;False;True;-1;2;AmplifyShaderEditor.MaterialInspector;0;0;Standard;Drakkar/Built-In/MultiMaterial/MultiMaterial Trim;False;False;False;False;False;True;False;True;True;True;False;True;True;False;True;False;False;False;False;False;False;Back;0;False;;0;False;;False;0;False;;0;False;;False;0;Opaque;0.5;True;False;0;False;Opaque;;Geometry;All;12;all;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;False;0;0;False;;0;False;;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;-1;-1;-1;-1;0;False;0;0;True;_CullMode;-1;0;False;;1;Pragma;instancing_options lodfade;False;;Custom;False;0;0;;1;addshadow;0;False;0.1;False;;0;False;;True;17;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;345;0;344;0
WireConnection;345;1;358;0
WireConnection;346;0;345;0
WireConnection;349;0;346;0
WireConnection;473;0;308;1
WireConnection;472;0;308;0
WireConnection;441;0;185;0
WireConnection;449;1;445;0
WireConnection;449;2;444;0
WireConnection;449;3;446;0
WireConnection;484;0;446;0
WireConnection;450;0;449;0
WireConnection;451;0;474;0
WireConnection;451;1;471;0
WireConnection;451;7;475;0
WireConnection;488;0;484;0
WireConnection;490;0;485;4
WireConnection;490;1;481;0
WireConnection;452;0;450;0
WireConnection;452;1;451;4
WireConnection;552;0;485;0
WireConnection;482;0;490;0
WireConnection;482;1;488;0
WireConnection;460;2;459;0
WireConnection;460;3;455;3
WireConnection;502;0;455;0
WireConnection;456;0;454;0
WireConnection;456;1;552;0
WireConnection;483;0;450;0
WireConnection;483;1;452;0
WireConnection;483;2;482;0
WireConnection;462;0;467;0
WireConnection;462;1;460;0
WireConnection;462;2;483;0
WireConnection;462;3;483;0
WireConnection;501;0;502;0
WireConnection;461;0;456;0
WireConnection;461;1;483;0
WireConnection;461;2;457;0
WireConnection;553;0;540;0
WireConnection;463;0;461;0
WireConnection;499;0;462;0
WireConnection;503;0;501;0
WireConnection;533;0;553;0
WireConnection;528;0;540;0
WireConnection;515;1;522;0
WireConnection;534;0;533;0
WireConnection;464;0;477;0
WireConnection;464;1;476;0
WireConnection;464;7;478;0
WireConnection;464;2;499;0
WireConnection;464;3;503;0
WireConnection;464;8;463;0
WireConnection;464;9;463;1
WireConnection;464;10;463;2
WireConnection;470;0;464;0
WireConnection;554;0;528;0
WireConnection;530;0;515;0
WireConnection;530;1;534;0
WireConnection;603;2;530;0
WireConnection;603;3;554;0
WireConnection;301;0;472;0
WireConnection;301;1;479;0
WireConnection;301;7;473;0
WireConnection;551;0;546;0
WireConnection;544;1;603;0
WireConnection;204;0;301;3
WireConnection;545;0;544;0
WireConnection;545;1;551;0
WireConnection;151;0;28;2
WireConnection;516;0;545;0
WireConnection;519;1;276;0
WireConnection;556;0;518;0
WireConnection;556;1;276;0
WireConnection;556;2;519;0
WireConnection;97;0;154;0
WireConnection;97;1;92;0
WireConnection;285;0;504;0
WireConnection;404;1;556;0
WireConnection;139;0;97;0
WireConnection;180;0;285;1
WireConnection;181;0;285;2
WireConnection;557;0;28;4
WireConnection;557;1;556;0
WireConnection;557;2;404;0
WireConnection;96;0;139;0
WireConnection;203;0;301;1
WireConnection;573;2;557;0
WireConnection;99;0;96;0
WireConnection;590;8;182;0
WireConnection;590;9;183;0
WireConnection;590;11;573;0
WireConnection;205;0;99;0
WireConnection;205;1;206;0
WireConnection;177;0;590;0
WireConnection;175;0;590;6
WireConnection;156;0;205;0
WireConnection;54;1;178;0
WireConnection;54;2;176;0
WireConnection;592;8;573;0
WireConnection;596;10;573;0
WireConnection;596;8;158;0
WireConnection;562;2;28;1
WireConnection;371;0;54;0
WireConnection;169;0;592;0
WireConnection;167;0;285;0
WireConnection;163;0;596;0
WireConnection;140;1;562;0
WireConnection;368;0;371;0
WireConnection;363;0;285;3
WireConnection;593;7;573;0
WireConnection;39;0;168;0
WireConnection;39;2;170;0
WireConnection;408;0;301;4
WireConnection;76;0;166;0
WireConnection;171;0;593;5
WireConnection;44;0;41;0
WireConnection;44;1;42;0
WireConnection;44;2;43;0
WireConnection;44;3;140;0
WireConnection;161;0;44;0
WireConnection;3;0;164;0
WireConnection;3;1;39;0
WireConnection;3;2;369;0
WireConnection;187;1;479;0
WireConnection;187;7;473;0
WireConnection;4;0;76;3
WireConnection;4;1;364;0
WireConnection;67;1;54;0
WireConnection;405;1;187;0
WireConnection;405;2;409;0
WireConnection;31;0;172;0
WireConnection;31;1;4;0
WireConnection;31;2;67;0
WireConnection;142;0;3;0
WireConnection;188;0;405;0
WireConnection;379;0;31;0
WireConnection;377;0;142;0
WireConnection;173;0;593;0
WireConnection;372;0;162;0
WireConnection;127;0;28;3
WireConnection;468;0;463;1
WireConnection;505;0;482;0
WireConnection;509;0;451;4
WireConnection;508;0;505;0
WireConnection;508;1;509;0
WireConnection;274;0;301;2
WireConnection;469;0;468;0
WireConnection;511;0;508;0
WireConnection;362;1;479;0
WireConnection;362;0;441;0
WireConnection;0;0;378;0
WireConnection;0;1;189;0
WireConnection;0;2;373;0
WireConnection;0;3;174;0
WireConnection;0;4;380;0
WireConnection;0;5;157;0
ASEEND*/
//CHKSM=FF8BA0D7216776400E6EB059FA21BE9A3B608D83