#include "Include/Macros.fxh"
#include "Include/Transform.fxh"
#include "Include/Sampling.fxh"
#include "Include/Depth.fxh"
#include "Include/BlinnPhongLighting.fxh"

#ifdef SHADOW
	#include "Include/Shadows.fxh"
#endif

#include "Include/Fog.fxh"

uniform float4 cMatDiffColor;
uniform float3 cMatEmissiveColor;

DECLARE_TEXTURE2D_LINEAR_WRAP(DiffMap);

#ifdef CLIPPLANE

float4 cClipPlane;

#endif

struct VSInput
{
	#if !defined(SKINNED)
		float3 Pos : POSITION;
	#else
		float3 Pos0 : POSITION0;
		float3 Pos1 : POSITION1;
		float3 Pos2 : POSITION2;
		float3 Pos3 : POSITION3;
	#endif
	float3 Normal : NORMAL;
	float2 TexCoord : TEXCOORD0;
	#ifdef SKINNED
		int4 BlendIndices : BLENDINDICES;
		float4 BlendWeights : BLENDWEIGHT;
	#endif
	#ifdef INSTANCED
		float4x4 ModelInstance : BLENDWEIGHT;
	#endif
};

struct VSOutput
{
	float4 Pos : OUTPOSITION;
	float2 TexCoord : TEXCOORD0;
	float3 Normal : TEXCOORD1;
	float4 WorldPos : TEXCOORD2;
	#ifdef SHADOW
		float4 ShadowPos[NUMCASCADES] : TEXCOORD3;
	#endif
	#ifdef SPOTLIGHT
		float4 SpotPos : TEXCOORD4;
	#endif
	#if defined(CLIPPLANE)
		float Clip : TEXCOORD7;
	#endif
};

VSOutput VS(VSInput input)
{
	VSOutput output = (VSOutput)0;

	CALCULATE_WORLD_POS(worldPos);
	CALCULATE_WORLD_NORMAL(worldNormal);

	output.Pos = GetClipPos(worldPos);
	output.Normal = worldNormal;

	output.WorldPos = float4(worldPos, GetDepth(output.Pos));

	#if defined(CLIPPLANE)
		output.Clip = dot(float4(worldPos, 1), cClipPlane);
	#endif

	output.TexCoord = GetTexCoord(input.TexCoord);

	// Per-pixel forward lighting
	float4 projWorldPos = float4(worldPos.xyz, 1.0);

	#ifdef SHADOW
		// Shadow projection: transform from world space to shadow space
		GetShadowPos(projWorldPos, output.Normal, output.ShadowPos);
	#endif

	#ifdef SPOTLIGHT
		// Spotlight projection: transform from world space to projector texture coordinates
		output.SpotPos = mul(projWorldPos, cSpotLightMatrix);
	#endif

	return output;
}

float4 PS(VSOutput input): OUTCOLOR0
{
#ifdef CLIPPLANE
	clip(input.Clip); 
#endif

	// Get material diffuse albedo
	float4 diffInput = Sample2D(DiffMap, input.TexCoord.xy);

	if (diffInput.a < 0.5)
		discard;

	float4 diffColor = cMatDiffColor * diffInput;

	// Get normal
	float3 normal = normalize(input.Normal);

	// Get fog factor
	float fogFactor = GetFogFactor(input.WorldPos.w);

	// Per-pixel forward lighting
	float3 lightDir;
	float3 lightColor;
	float3 finalColor;

	float diff = GetDiffuse(normal, input.WorldPos.xyz, lightDir);

	#ifdef SHADOW
		float shadow = GetShadow(input.ShadowPos, input.WorldPos.w);
		diff *= (1.0 - shadow);
	#endif

	#if defined(SPOTLIGHT)
		lightColor = input.SpotPos.w > 0.0 ? Sample2DProj(LightSpotMap, input.SpotPos).rgb * cLightColor.rgb : 0.0;
	#else
		lightColor = cLightColor.rgb;
	#endif

	finalColor = diff * lightColor * diffColor.rgb;

	finalColor += GetAmbientColor() * diffColor.rgb;
	finalColor += cMatEmissiveColor;
	float4 oColor = float4(GetFog(finalColor, fogFactor), diffColor.a);

/*	#if defined(DIRLIGHT) && defined(SHADOW)
		int res = GetShadowSplit(input.WorldPos.w);
		
		if (res == 0)
		{
			oColor.r += 0.2;
		} else if (res == 1)
		{
			oColor.g += 0.2;
		} else if (res == 2)
		{
			oColor.b += 0.2;
		} else
		{
			oColor.r += 0.2;
			oColor.g += 0.2;
			oColor.b += 0.2;
		}
	#endif*/

	return oColor;
}

TECHNIQUE(Default, VS, PS);