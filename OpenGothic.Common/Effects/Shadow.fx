#include "Include/Macros.fxh"
#include "Include/Sampling.fxh"

uniform float4x3 cModel;
uniform float4x4 cViewProj;

#ifdef SKINNED
	uniform float4x3 cSkinMatrices[MAXBONES];
#endif

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
		float4x3 ModelInstance : TEXCOORD1;
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

	#if !defined(SKINNED)
		float3 inputPos = input.Pos;
	#else
		float3 inputPos = (mul(float4(input.Pos0, 1.0), cSkinMatrices[input.BlendIndices.x]) * input.BlendWeights.x) +
			(mul(float4(input.Pos1, 1.0), cSkinMatrices[input.BlendIndices.y]) * input.BlendWeights.y) +
			(mul(float4(input.Pos2, 1.0), cSkinMatrices[input.BlendIndices.z]) * input.BlendWeights.z) +
			(mul(float4(input.Pos3, 1.0), cSkinMatrices[input.BlendIndices.w]) * input.BlendWeights.w);
	#endif

	float3 worldPos = mul(float4(inputPos, 1.0), cModel);
	output.Pos = mul(float4(worldPos, 1.0), cViewProj);

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