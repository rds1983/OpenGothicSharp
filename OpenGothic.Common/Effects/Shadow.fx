#include "Include/Macros.fxh"
#include "Include/Transform.fxh"
#include "Include/Sampling.fxh"

DECLARE_TEXTURE2D_POINT_CLAMP(DiffMap);

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
	float2 TexCoord : TEXCOORD0;
	#ifdef SKINNED
		float4 BlendWeights : BLENDWEIGHT;
		int4 BlendIndices : BLENDINDICES;
	#endif
	#ifdef INSTANCED
		float4x4 ModelInstance : BLENDWEIGHT;
	#endif
};

struct VSOutput
{
	float2 TexCoord : TEXCOORD0;
	float4 Pos : OUTPOSITION;
	float4 PosCopy : TEXCOORD1;
};

VSOutput VS(VSInput input)
{
	VSOutput output = (VSOutput)0;

	CALCULATE_WORLD_POS(worldPos);
	output.Pos = GetClipPos(worldPos);

	output.TexCoord = GetTexCoord(input.TexCoord);

	output.PosCopy = output.Pos;

	return output;
}

float4 PS(VSOutput input): OUTCOLOR0
{
	float alpha = Sample2D(DiffMap, input.TexCoord.xy).a;
	if (alpha < 0.5)
		discard;

	// Finish the projection
	return float4(input.PosCopy.z / input.PosCopy.w, 0, 0, 0);
}

TECHNIQUE(Default, VS, PS);