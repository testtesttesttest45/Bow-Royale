// Made with Amplify Shader Editor v1.9.3.2
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "ExpSphere"
{
	Properties
	{
		_Scale("Scale", Float) = 0
		_FresnelColor("FresnelColor", Color) = (1,1,1,0)
		_Pow("Pow", Float) = 0
		_EdgeNoise("EdgeNoise", 2D) = "white" {}
		_Sub("Sub", Float) = 0
		_InnerScale("InnerScale", Float) = 1
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Transparent"  "Queue" = "Transparent+0" "IgnoreProjector" = "True" "IsEmissive" = "true"  }
		Cull Off
		CGINCLUDE
		#include "UnityPBSLighting.cginc"
		#include "Lighting.cginc"
		#pragma target 3.0
		#undef TRANSFORM_TEX
		#define TRANSFORM_TEX(tex,name) float4(tex.xy * name##_ST.xy + name##_ST.zw, tex.z, tex.w)
		struct Input
		{
			float3 worldPos;
			float3 worldNormal;
			float4 uv_texcoord;
			half ASEIsFrontFacing : VFACE;
			float4 vertexColor : COLOR;
		};

		uniform float _InnerScale;
		uniform float4 _FresnelColor;
		uniform sampler2D _EdgeNoise;
		uniform float4 _EdgeNoise_ST;
		uniform float _Sub;
		uniform float _Scale;
		uniform float _Pow;

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float3 ase_worldPos = i.worldPos;
			float3 ase_worldViewDir = normalize( UnityWorldSpaceViewDir( ase_worldPos ) );
			float3 ase_worldNormal = i.worldNormal;
			float fresnelNdotV26 = dot( ase_worldNormal, ase_worldViewDir );
			float fresnelNode26 = ( 0.0 + _InnerScale * pow( 1.0 - fresnelNdotV26, i.uv_texcoord.z ) );
			float switchResult30 = (((i.ASEIsFrontFacing>0)?(fresnelNode26):(0.0)));
			float4 temp_output_33_0 = ( switchResult30 * i.vertexColor );
			float2 uv_EdgeNoise = i.uv_texcoord * _EdgeNoise_ST.xy + _EdgeNoise_ST.zw;
			float4 temp_cast_0 = (_Sub).xxxx;
			float4 clampResult17 = clamp( ( tex2D( _EdgeNoise, uv_EdgeNoise ) - temp_cast_0 ) , float4( 0,0,0,0 ) , float4( 1,1,1,0 ) );
			float fresnelNdotV1 = dot( ase_worldNormal, ase_worldViewDir );
			float fresnelNode1 = ( 0.0 + _Scale * pow( 1.0 - fresnelNdotV1, _Pow ) );
			float switchResult10 = (((i.ASEIsFrontFacing>0)?(fresnelNode1):(0.0)));
			float4 temp_output_5_0 = ( clampResult17 * switchResult10 );
			float4 lerpResult51 = lerp( temp_output_33_0 , ( _FresnelColor * temp_output_5_0 ) , temp_output_5_0.g);
			o.Emission = lerpResult51.rgb;
			o.Alpha = switchResult30;
		}

		ENDCG
		CGPROGRAM
		#pragma surface surf Standard alpha:fade keepalpha fullforwardshadows 

		ENDCG
		Pass
		{
			Name "ShadowCaster"
			Tags{ "LightMode" = "ShadowCaster" }
			ZWrite On
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.0
			#pragma multi_compile_shadowcaster
			#pragma multi_compile UNITY_PASS_SHADOWCASTER
			#pragma skip_variants FOG_LINEAR FOG_EXP FOG_EXP2
			#include "HLSLSupport.cginc"
			#if ( SHADER_API_D3D11 || SHADER_API_GLCORE || SHADER_API_GLES || SHADER_API_GLES3 || SHADER_API_METAL || SHADER_API_VULKAN )
				#define CAN_SKIP_VPOS
			#endif
			#include "UnityCG.cginc"
			#include "Lighting.cginc"
			#include "UnityPBSLighting.cginc"
			sampler3D _DitherMaskLOD;
			struct v2f
			{
				V2F_SHADOW_CASTER;
				float4 customPack1 : TEXCOORD1;
				float3 worldPos : TEXCOORD2;
				float3 worldNormal : TEXCOORD3;
				half4 color : COLOR0;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};
			v2f vert( appdata_full v )
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID( v );
				UNITY_INITIALIZE_OUTPUT( v2f, o );
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( o );
				UNITY_TRANSFER_INSTANCE_ID( v, o );
				Input customInputData;
				float3 worldPos = mul( unity_ObjectToWorld, v.vertex ).xyz;
				half3 worldNormal = UnityObjectToWorldNormal( v.normal );
				o.worldNormal = worldNormal;
				o.customPack1.xyzw = customInputData.uv_texcoord;
				o.customPack1.xyzw = v.texcoord;
				o.worldPos = worldPos;
				TRANSFER_SHADOW_CASTER_NORMALOFFSET( o )
				o.color = v.color;
				return o;
			}
			half4 frag( v2f IN
			#if !defined( CAN_SKIP_VPOS )
			, UNITY_VPOS_TYPE vpos : VPOS
			#endif
			) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				Input surfIN;
				UNITY_INITIALIZE_OUTPUT( Input, surfIN );
				surfIN.uv_texcoord = IN.customPack1.xyzw;
				float3 worldPos = IN.worldPos;
				half3 worldViewDir = normalize( UnityWorldSpaceViewDir( worldPos ) );
				surfIN.worldPos = worldPos;
				surfIN.worldNormal = IN.worldNormal;
				surfIN.vertexColor = IN.color;
				SurfaceOutputStandard o;
				UNITY_INITIALIZE_OUTPUT( SurfaceOutputStandard, o )
				surf( surfIN, o );
				#if defined( CAN_SKIP_VPOS )
				float2 vpos = IN.pos;
				#endif
				half alphaRef = tex3D( _DitherMaskLOD, float3( vpos.xy * 0.25, o.Alpha * 0.9375 ) ).a;
				clip( alphaRef - 0.01 );
				SHADOW_CASTER_FRAGMENT( IN )
			}
			ENDCG
		}
	}
	Fallback "Diffuse"
	CustomEditor "ASEMaterialInspector"
}
/*ASEBEGIN
Version=19302
Node;AmplifyShaderEditor.CommentaryNode;45;-835.8222,-15.05005;Inherit;False;1227.023;378.6903;Comment;5;5;10;1;4;3;;1,1,1,1;0;0
Node;AmplifyShaderEditor.CommentaryNode;46;-792.9548,374.4124;Inherit;False;859.1293;566.7819;Comment;4;30;26;49;28;;1,1,1,1;0;0
Node;AmplifyShaderEditor.RangedFloatNode;3;-785.8222,159.4842;Inherit;False;Property;_Scale;Scale;0;0;Create;True;0;0;0;False;0;False;0;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;4;-773.3353,242.7297;Inherit;False;Property;_Pow;Pow;2;0;Create;True;0;0;0;False;0;False;0;1.07;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode;12;-775.4518,-339.2635;Inherit;True;Property;_EdgeNoise;EdgeNoise;3;0;Create;True;0;0;0;False;0;False;-1;None;4e3951c538fc8a647a4a10a99b480987;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.RangedFloatNode;16;-711.3019,-459.879;Inherit;False;Property;_Sub;Sub;4;0;Create;True;0;0;0;False;0;False;0;0.25;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode;28;-753.6583,447.5388;Inherit;False;Property;_InnerScale;InnerScale;5;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode;49;-767.2659,642.6408;Inherit;True;0;-1;4;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT4;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.FresnelNode;1;-560.2678,34.94995;Inherit;False;Standard;WorldNormal;ViewDir;False;False;5;0;FLOAT3;0,0,1;False;4;FLOAT3;0,0,0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;5;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleSubtractOpNode;15;-448.2939,-390.6045;Inherit;False;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SwitchByFaceNode;10;-315.732,128.524;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode;17;-202.5191,-358.9041;Inherit;False;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;COLOR;1,1,1,0;False;1;COLOR;0
Node;AmplifyShaderEditor.FresnelNode;26;-542.9048,527.7903;Inherit;True;Standard;WorldNormal;ViewDir;False;False;5;0;FLOAT3;0,0,1;False;4;FLOAT3;0,0,0;False;1;FLOAT;0;False;2;FLOAT;1;False;3;FLOAT;5;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;5;-73.84079,41.14369;Inherit;True;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SwitchByFaceNode;30;-246.1274,456.1209;Inherit;True;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.VertexColorNode;43;-263.7462,1096.603;Inherit;False;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.ColorNode;2;42.53061,-369.5524;Inherit;False;Property;_FresnelColor;FresnelColor;1;0;Create;True;0;0;0;False;0;False;1,1,1,0;0.1843137,0.4943919,0.9529412,0;True;0;5;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;33;159.8916,899.5277;Inherit;True;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;22;356.7278,-348.7027;Inherit;True;2;2;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.BreakToComponentsNode;53;227.5381,74.70203;Inherit;False;COLOR;1;0;COLOR;0,0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.BreakToComponentsNode;44;463.8247,911.6488;Inherit;True;COLOR;1;0;COLOR;0,0,0,0;False;16;FLOAT;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT;5;FLOAT;6;FLOAT;7;FLOAT;8;FLOAT;9;FLOAT;10;FLOAT;11;FLOAT;12;FLOAT;13;FLOAT;14;FLOAT;15
Node;AmplifyShaderEditor.RangedFloatNode;50;-940.8395,576.373;Inherit;False;Constant;_Float0;Float 0;6;0;Create;True;0;0;0;False;0;False;0.5;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.LerpOp;51;1156.757,40.08821;Inherit;True;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleAddOpNode;41;919.9882,919.9053;Inherit;True;2;2;0;COLOR;0,0,0,0;False;1;FLOAT;0;False;1;COLOR;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode;47;1165.369,1123.593;Inherit;True;2;2;0;FLOAT;0;False;1;COLOR;0,0,0,0;False;1;COLOR;0
Node;AmplifyShaderEditor.ClampOpNode;42;1412.391,871.4437;Inherit;True;3;0;COLOR;0,0,0,0;False;1;COLOR;0,0,0,0;False;2;COLOR;1,1,1,0;False;1;COLOR;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode;0;1781.128,209.0454;Float;False;True;-1;2;ASEMaterialInspector;0;0;Standard;ExpSphere;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;False;False;False;False;False;False;Off;0;False;;0;False;;False;0;False;;0;False;;False;0;Transparent;0.5;True;True;0;False;Transparent;;Transparent;All;12;all;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;True;2;5;False;;10;False;;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;17;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;1;2;3;0
WireConnection;1;3;4;0
WireConnection;15;0;12;0
WireConnection;15;1;16;0
WireConnection;10;0;1;0
WireConnection;17;0;15;0
WireConnection;26;2;28;0
WireConnection;26;3;49;3
WireConnection;5;0;17;0
WireConnection;5;1;10;0
WireConnection;30;0;26;0
WireConnection;33;0;30;0
WireConnection;33;1;43;0
WireConnection;22;0;2;0
WireConnection;22;1;5;0
WireConnection;53;0;5;0
WireConnection;44;0;33;0
WireConnection;51;0;33;0
WireConnection;51;1;22;0
WireConnection;51;2;53;1
WireConnection;41;0;5;0
WireConnection;41;1;30;0
WireConnection;47;0;30;0
WireConnection;47;1;41;0
WireConnection;42;0;47;0
WireConnection;0;2;51;0
WireConnection;0;9;30;0
ASEEND*/
//CHKSM=3FC1EECB8E7BE75BBB6AE907331D61217899E0FE