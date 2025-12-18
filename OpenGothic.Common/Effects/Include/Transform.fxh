uniform float4x3 cModel;
uniform float4x4 cViewProj;

#ifdef SKINNED
	uniform float4x3 cSkinMatrices[MAXBONES];
#endif

#if defined(INSTANCED)
	#define CALCULATE_WORLD_POS(Name) \
		float3 Name = mul(float4(input.Pos, 1.0), input.ModelInstance).xyz; \
		Name = mul(float4(Name, 1.0), cModel);

	#define CALCULATE_WORLD_NORMAL(Name) \
		float3 Name = mul(input.Normal, (float3x3)input.ModelInstance); \
		Name = normalize(mul(Name, (float3x3)cModel));
#elif defined(SKINNED)
	#define CALCULATE_WORLD_POS(Name) \
		float3 Name = (mul(float4(input.Pos0, 1.0), cSkinMatrices[input.BlendIndices.x]) * input.BlendWeights.x) + \
			(mul(float4(input.Pos1, 1.0), cSkinMatrices[input.BlendIndices.y]) * input.BlendWeights.y) + \
			(mul(float4(input.Pos2, 1.0), cSkinMatrices[input.BlendIndices.z]) * input.BlendWeights.z) + \
			(mul(float4(input.Pos3, 1.0), cSkinMatrices[input.BlendIndices.w]) * input.BlendWeights.w); \
		Name = mul(float4(Name, 1.0), cModel);

	#define CALCULATE_WORLD_NORMAL(Name) \
		float3 Name = normalize(mul(input.Normal, (float3x3)cModel));
#else
	#define CALCULATE_WORLD_POS(Name) \
		float3 Name = mul(float4(input.Pos, 1.0), cModel);

	#define CALCULATE_WORLD_NORMAL(Name) \
		float3 Name = normalize(mul(input.Normal, (float3x3)cModel));
#endif

float4 GetClipPos(float3 Name)
{
	return mul(float4(Name, 1.0), cViewProj);
}
