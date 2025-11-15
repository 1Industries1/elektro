// Made with Amplify Shader Editor v1.9.8.1
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "Drakkar/Built-In/Cybernetics/BuzzWings"
{
	Properties
	{
		_Color("Color", Color) = (0,0,0)
		_FresnelPower("Fresnel Power", Float) = 0
		[SingleLineTexture]_NoiseNormal("NoiseNormal", 2D) = "bump" {}
		_Tiling("Tiling", Vector) = (0,0,0,0)
		_Flap("Flap", Float) = 0.1
		_FlapSpeed("Flap Speed", Float) = 1
		_AlphaPower("Alpha Power", Float) = 1
		_Smoothness("Smoothness", Float) = 0.53
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Transparent"  "Queue" = "Transparent+0" "IgnoreProjector" = "True" }
		Cull Back
		CGPROGRAM
		#include "UnityShaderVariables.cginc"
		#pragma target 3.5
		#define ASE_VERSION 19801
		#pragma surface surf Standard alpha:fade keepalpha noshadow novertexlights nolightmap  nodynlightmap nodirlightmap vertex:vertexDataFunc 
		struct Input
		{
			float2 uv_texcoord;
			float3 worldPos;
			float3 worldNormal;
			INTERNAL_DATA
		};

		uniform float _Flap;
		uniform float _FlapSpeed;
		uniform sampler2D _NoiseNormal;
		uniform float2 _Tiling;
		uniform float3 _Color;
		uniform float _Smoothness;
		uniform float _FresnelPower;
		uniform float _AlphaPower;

		void vertexDataFunc( inout appdata_full v, out Input o )
		{
			UNITY_INITIALIZE_OUTPUT( Input, o );
			float mulTime29 = _Time.y * _FlapSpeed;
			float temp_output_32_0 = ( _Flap * sin( mulTime29 ) );
			float temp_output_45_0 = ( v.texcoord.xy.x * temp_output_32_0 );
			float3 appendResult34 = (float3(0.0 , temp_output_45_0 , temp_output_45_0));
			v.vertex.xyz += appendResult34;
			v.vertex.w = 1;
		}

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float2 break42 = ( i.uv_texcoord * _Tiling );
			float mulTime29 = _Time.y * _FlapSpeed;
			float temp_output_32_0 = ( _Flap * sin( mulTime29 ) );
			float2 appendResult43 = (float2(break42.x , ( break42.y + temp_output_32_0 )));
			o.Normal = UnpackNormal( tex2D( _NoiseNormal, appendResult43 ) );
			o.Albedo = _Color;
			o.Smoothness = _Smoothness;
			float3 ase_positionWS = i.worldPos;
			float3 ase_viewVectorWS = ( _WorldSpaceCameraPos.xyz - ase_positionWS );
			float3 ase_viewDirWS = normalize( ase_viewVectorWS );
			float3 ase_normalWS = WorldNormalVector( i, float3( 0, 0, 1 ) );
			float fresnelNdotV14 = dot( ase_normalWS, ase_viewDirWS );
			float fresnelNode14 = ( 0.0 + 1.0 * pow( 1.0 - fresnelNdotV14, _FresnelPower ) );
			o.Alpha = ( ( 1.0 - saturate( fresnelNode14 ) ) * ( 1.0 - saturate( pow( i.uv_texcoord.x , _AlphaPower ) ) ) );
		}

		ENDCG
	}
	CustomEditor "AmplifyShaderEditor.MaterialInspector"
}
/*ASEBEGIN
Version=19801
Node;AmplifyShaderEditor.RangedFloatNode;35;-1456,720;Inherit;False;Property;_FlapSpeed;Flap Speed;5;0;Create;True;0;0;0;False;0;False;1;150;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;18;-1328,256;Inherit;False;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleTimeNode;29;-1264,720;Inherit;False;1;0;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node;24;-1216,416;Inherit;False;Property;_Tiling;Tiling;3;0;Create;True;0;0;0;False;0;False;0,0;1.81,0.05;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.SinOpNode;30;-1088,720;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;31;-1088,624;Inherit;False;Property;_Flap;Flap;4;0;Create;True;0;0;0;False;0;False;0.1;0.02;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;25;-992,320;Inherit;False;2;2;0;FLOAT2;0,0;False;1;FLOAT2;0,0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.RangedFloatNode;15;-1184,48;Inherit;False;Property;_FresnelPower;Fresnel Power;1;0;Create;True;0;0;0;False;0;False;0;0.6;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;49;-1088,176;Inherit;False;Property;_AlphaPower;Alpha Power;6;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;32;-928,624;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.BreakToComponentsNode;42;-848,320;Inherit;False;FLOAT2;1;0;FLOAT2;0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.FresnelNode;14;-960,-48;Inherit;False;Standard;WorldNormal;ViewDir;False;False;5;0;FLOAT3;0,0,1;False;4;FLOAT3;0,0,0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;5;False;1;FLOAT;0
Node;AmplifyShaderEditor.PowerNode;48;-864,176;Inherit;False;False;2;0;FLOAT;0;False;1;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode;44;-688,400;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;16;-704,-48;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SaturateNode;50;-704,176;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode;46;-928,528;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;43;-544,320;Inherit;False;FLOAT2;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT2;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;45;0,480;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;17;-496,32;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode;20;-544,176;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.DynamicAppendNode;34;176,464;Inherit;False;FLOAT3;4;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;0;False;3;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;19;-320,32;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;23;-304,160;Inherit;True;Property;_NoiseNormal;NoiseNormal;2;1;[SingleLineTexture];Create;True;0;0;0;False;0;False;-1;None;None;True;0;True;bump;Auto;True;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ColorNode;13;-256,-240;Inherit;False;Property;_Color;Color;0;0;Create;True;0;0;0;False;0;False;0,0,0,0;0,0,0,1;True;False;0;6;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode;52;224,112;Inherit;False;Property;_Smoothness;Smoothness;7;0;Create;True;0;0;0;False;0;False;0.53;0.48;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;53;416,-32;Float;False;True;-1;3;AmplifyShaderEditor.MaterialInspector;0;0;Standard;Drakkar/Built-In/Cybernetics/BuzzWings;False;False;False;False;False;True;True;True;True;False;False;False;False;False;True;False;False;False;False;False;False;Back;0;False;;0;False;;False;0;False;;0;False;;False;0;Transparent;0.5;True;False;0;False;Transparent;;Transparent;All;12;all;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;False;2;5;False;;10;False;;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;;0;0;False;;0;0;0;False;0.1;False;;0;False;;False;17;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;29;0;35;0
WireConnection;30;0;29;0
WireConnection;25;0;18;0
WireConnection;25;1;24;0
WireConnection;32;0;31;0
WireConnection;32;1;30;0
WireConnection;42;0;25;0
WireConnection;14;3;15;0
WireConnection;48;0;18;1
WireConnection;48;1;49;0
WireConnection;44;0;42;1
WireConnection;44;1;32;0
WireConnection;16;0;14;0
WireConnection;50;0;48;0
WireConnection;46;0;18;1
WireConnection;43;0;42;0
WireConnection;43;1;44;0
WireConnection;45;0;46;0
WireConnection;45;1;32;0
WireConnection;17;0;16;0
WireConnection;20;0;50;0
WireConnection;34;1;45;0
WireConnection;34;2;45;0
WireConnection;19;0;17;0
WireConnection;19;1;20;0
WireConnection;23;1;43;0
WireConnection;53;0;13;0
WireConnection;53;1;23;0
WireConnection;53;4;52;0
WireConnection;53;9;19;0
WireConnection;53;11;34;0
ASEEND*/
//CHKSM=C8F216328B855BAE272C263754E40510019227C3