// Morza Bogów: the paint of a ship's look (ShipPaint.shadergraph, Custom Function "ShipPaint").
//
// One palette material per tone gives the colour, smoothness and maps (ShipPaints.cs); what differs ship to ship comes
// per renderer from Renderer.SetShaderUserValue (ShipSurface.cs). HDRP keeps that value in unity_RenderingLayer.y of the
// UnityPerDraw buffer, so ships that differ still share their materials and the SRP Batcher keeps batching them.
//
// Renderer value, 32 bits, four bits a field (0..15 read as 0..1):
//   bits 0-3   wear: how weathered the ship is (its finish and seed); shows where the mask's blue channel says
//   bits 4-7   wetness, reserved (VISUAL_TECH_SPEC V03): decoded below, not shown until the game has rain or spray
//   bits 8-11  soot, reserved (V03): decoded below, not shown until the game has fire
//   bits 12-31 zero
// Mask map packed as HDRP Lit packs it: R metallic, G occlusion, B detail mask (here: where wear shows), A smoothness
// relative to the tone's. Normal and mask maps are always sampled, so no keyword decides whether they exist.

#ifndef MORZABOGOW_SHIP_PAINT_INCLUDED
#define MORZABOGOW_SHIP_PAINT_INCLUDED

float ShipSurfaceField(uint value, uint shift)
{
    return ((value >> shift) & 15u) / 15.0;
}

float ShipWear(uint value) { return ShipSurfaceField(value, 0u); }
// Reserved fields: their place in the value is kept, and the paint shows them once the game has a source for them.
float ShipWetness(uint value) { return ShipSurfaceField(value, 4u); }
float ShipSoot(uint value) { return ShipSurfaceField(value, 8u); }

void ShipPaint_float(float4 BaseColor, UnityTexture2D BaseMap, UnityTexture2D NormalMap, float NormalScale, UnityTexture2D MaskMap,
    float Metallic, float Smoothness, float WearStrength, float2 UV,
    out float3 Albedo, out float3 Normal, out float MetallicOut, out float SmoothnessOut, out float Occlusion)
{
    // Every map follows the colour map's tiling, as in HDRP/Lit.
    float2 uv = BaseMap.GetTransformedUV(UV);
    float4 colour = SAMPLE_TEXTURE2D(BaseMap.tex, BaseMap.samplerstate, uv);
    float4 packedNormal = SAMPLE_TEXTURE2D(NormalMap.tex, NormalMap.samplerstate, uv);
    float4 mask = SAMPLE_TEXTURE2D(MaskMap.tex, MaskMap.samplerstate, uv);

    uint value = unity_RendererUserValue;
    float wear = saturate(ShipWear(value) * mask.b * WearStrength);

    // Weathering bleaches the paint or timber toward a grey of its own lightness, lifts it a little, roughens it and lets
    // the grain stand out more. It never adds metal.
    float3 albedo = BaseColor.rgb * colour.rgb;
    float grey = dot(albedo, float3(0.2126, 0.7152, 0.0722));
    Albedo = saturate(lerp(albedo, grey.xxx, wear * 0.3) * (1.0 + 0.08 * wear));
    Normal = UnpackNormalScale(packedNormal, NormalScale * (1.0 + 0.5 * wear));
    MetallicOut = Metallic * mask.r;
    SmoothnessOut = saturate(Smoothness * mask.a * (1.0 - 0.45 * wear));
    Occlusion = mask.g;
}

#endif
