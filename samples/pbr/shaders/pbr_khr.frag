// PBR shader based on the Khronos WebGL PBR implementation
// See https://github.com/KhronosGroup/glTF-WebGL-PBR
// Supports both metallic roughness and specular glossiness inputs

#include "preamble.inc"

#define MANUAL_SRGB 0

#include "tonemap.inc"

struct Material {
    vec4 baseColorFactor;
    vec4 emissiveFactor;
    vec4 diffuseFactor;
    vec4 specularFactor;
    float workflow;
    uint tex0;
    uint tex1;
    float metallicFactor;   
    float roughnessFactor;  
    float alphaMask;    
    float alphaMaskCutoff;
    int pad0;
};

const float M_PI = 3.141592653589793;
const float c_MinRoughness = 0.04;

const float PBR_WORKFLOW_METALLIC_ROUGHNESS = 1.0;
const float PBR_WORKFLOW_SPECULAR_GLOSINESS = 2.0f;

const uint MAP_COLOR = 0x1;
const uint MAP_NORMAL = 0x2;
const uint MAP_AO = 0x4;
const uint MAP_METAL = 0x8;
const uint MAP_ROUGHNESS = 0x10;
const uint MAP_METALROUGHNESS = 0x20;
const uint MAP_EMISSIVE = 0x40;

layout (location = 0) in vec3 inWorldPos;
layout (location = 1) in vec3 inNormal;
layout (location = 2) in vec2 inUV0;
layout (location = 3) in vec2 inUV1;

layout (set = 0, binding = 0) uniform UBO {
    mat4 projection;
    mat4 model;
    mat4 view;
    vec4 camPos;
    vec4 lightDir;
    float exposure;
    float gamma;
    float prefilteredCubeMipLevels;
    float scaleIBLAmbient;
#ifdef DEBUG
    float debugViewInputs;
    float debugViewEquation;
#endif
};

layout (set = 0, binding = 4) uniform UBOMaterials {
    Material materials[16];
};

layout (set = 0, binding = 1) uniform samplerCube samplerIrradiance;
layout (set = 0, binding = 2) uniform samplerCube prefilteredMap;
layout (set = 0, binding = 3) uniform sampler2D samplerBRDFLUT;

// Material bindings

layout (set = 1, binding = 0) uniform sampler2D colorMap;
layout (set = 1, binding = 1) uniform sampler2D physicalDescriptorMap;
layout (set = 1, binding = 2) uniform sampler2D normalMap;
layout (set = 1, binding = 3) uniform sampler2D aoMap;
layout (set = 1, binding = 4) uniform sampler2D emissiveMap;


layout (push_constant) uniform PushCsts {
    layout(offset = 64)
    int materialIdx;
};

layout (location = 0) out vec4 outColor;

#include "pbr.inc"
// Find the normal for this fragment, pulling either from a predefined normal map
// or from the interpolated mesh normal and tangent attributes.
vec3 getNormal()
{
    vec3 tangentNormal;
    // Perturb normal, see http://www.thetenthplanet.de/archives/1180
    if ((materials[materialIdx].tex0 & MAP_NORMAL) == MAP_NORMAL)
        tangentNormal = texture(normalMap, inUV0).xyz * 2.0 - 1.0;
    else if ((materials[materialIdx].tex1 & MAP_NORMAL) == MAP_NORMAL)
        tangentNormal = texture(normalMap, inUV1).xyz * 2.0 - 1.0;
    else
        return normalize(inNormal);
        
    vec3 q1 = dFdx(inWorldPos);
    vec3 q2 = dFdy(inWorldPos);
    vec2 st1 = dFdx(inUV0);
    vec2 st2 = dFdy(inUV0);

    vec3 N = normalize(inNormal);
    vec3 T = normalize(q1 * st2.t - q2 * st1.t);
    vec3 B = -normalize(cross(N, T));
    mat3 TBN = mat3(T, B, N);

    return normalize(TBN * tangentNormal);
}

// Calculation of the lighting contribution from an optional Image Based Light source.
// Precomputed Environment Maps are required uniform inputs and are computed as outlined in [1].
// See our README.md on Environment Maps [3] for additional discussion.
vec3 getIBLContribution(PBRInfo pbrInputs, vec3 n, vec3 reflection)
{
    float lod = (pbrInputs.perceptualRoughness * prefilteredCubeMipLevels);
    // retrieve a scale and bias to F0. See [1], Figure 3
    vec3 brdf = (texture(samplerBRDFLUT, vec2(pbrInputs.NdotV, 1.0 - pbrInputs.perceptualRoughness))).rgb;
    
    vec3 diffuseLight = SRGBtoLINEAR(tonemap(texture(samplerIrradiance, n), exposure, gamma)).rgb;
    vec3 specularLight = SRGBtoLINEAR(tonemap(textureLod(prefilteredMap, reflection, lod), exposure, gamma)).rgb;    
    //vec3 diffuseLight = texture(samplerIrradiance, n).rgb;
    //vec3 specularLight = textureLod(prefilteredMap, reflection, lod).rgb;


    vec3 diffuse = diffuseLight * pbrInputs.diffuseColor;
    vec3 specular = specularLight * (pbrInputs.specularColor * brdf.x + brdf.y);

    // For presentation, this allows us to disable IBL terms    
    diffuse *= scaleIBLAmbient;
    specular *= scaleIBLAmbient;

    return diffuse + specular;
}

void main()
{
    float perceptualRoughness;
    float metallic;
    vec3 diffuseColor;
    vec4 baseColor;    

    vec3 f0 = vec3(0.04);
    
    baseColor = materials[materialIdx].baseColorFactor;
    
    if (materials[materialIdx].workflow == PBR_WORKFLOW_METALLIC_ROUGHNESS) {
        perceptualRoughness = materials[materialIdx].roughnessFactor;
        metallic = materials[materialIdx].metallicFactor;        
        // Roughness is stored in the 'g' channel, metallic is stored in the 'b' channel.
        // This layout intentionally reserves the 'r' channel for (optional) occlusion map data
        if ((materials[materialIdx].tex0 & MAP_METALROUGHNESS) == MAP_METALROUGHNESS){
            perceptualRoughness *= texture(physicalDescriptorMap, inUV0).g;
            metallic *= texture(physicalDescriptorMap, inUV0).b;
        }else if ((materials[materialIdx].tex1 & MAP_METALROUGHNESS) == MAP_METALROUGHNESS){
            perceptualRoughness *= texture(physicalDescriptorMap, inUV1).g;
            metallic *= texture(physicalDescriptorMap, inUV1).b;
        }               
        perceptualRoughness = clamp(perceptualRoughness, c_MinRoughness, 1.0);
        metallic = clamp(metallic, 0.0, 1.0);        

        // The albedo may be defined from a base texture or a flat color
        if ((materials[materialIdx].tex0 & MAP_COLOR) == MAP_COLOR)        
            baseColor *= SRGBtoLINEAR(texture(colorMap, inUV0));
        else if ((materials[materialIdx].tex1 & MAP_COLOR) == MAP_COLOR)
            baseColor *= SRGBtoLINEAR(texture(colorMap, inUV1));        
    }
    
    if (materials[materialIdx].alphaMask == 1.0f) {            
        if (baseColor.a < materials[materialIdx].alphaMaskCutoff) 
            discard;        
    }

    if (materials[materialIdx].workflow == PBR_WORKFLOW_SPECULAR_GLOSINESS) {
        // Values from specular glossiness workflow are converted to metallic roughness
        if ((materials[materialIdx].tex0 & MAP_METALROUGHNESS) == MAP_METALROUGHNESS)
            perceptualRoughness = 1.0 - texture(physicalDescriptorMap, inUV0).a;            
        else if ((materials[materialIdx].tex1 & MAP_METALROUGHNESS) == MAP_METALROUGHNESS)
            perceptualRoughness = 1.0 - texture(physicalDescriptorMap, inUV1).a;            
        else
            perceptualRoughness = 0.0;

        const float epsilon = 1e-6;

        vec4 diffuse = SRGBtoLINEAR(texture(colorMap, inUV0));
        vec3 specular = SRGBtoLINEAR(texture(physicalDescriptorMap, inUV0)).rgb;

        float maxSpecular = max(max(specular.r, specular.g), specular.b);

        // Convert metallic value from specular glossiness inputs
        metallic = convertMetallic(diffuse.rgb, specular, maxSpecular);

        vec3 baseColorDiffusePart = diffuse.rgb * ((1.0 - maxSpecular) / (1 - c_MinRoughness) / max(1 - metallic, epsilon)) * materials[materialIdx].diffuseFactor.rgb;
        vec3 baseColorSpecularPart = specular - (vec3(c_MinRoughness) * (1 - metallic) * (1 / max(metallic, epsilon))) * materials[materialIdx].specularFactor.rgb;
        baseColor = vec4(mix(baseColorDiffusePart, baseColorSpecularPart, metallic * metallic), diffuse.a);

    }

    diffuseColor = baseColor.rgb * (vec3(1.0) - f0);
    diffuseColor *= 1.0 - metallic;
        
    float alphaRoughness = perceptualRoughness * perceptualRoughness;

    vec3 specularColor = mix(f0, baseColor.rgb, metallic);

    // Compute reflectance.
    float reflectance = max(max(specularColor.r, specularColor.g), specularColor.b);

    // For typical incident reflectance range (between 4% to 100%) set the grazing reflectance to 100% for typical fresnel effect.
    // For very low reflectance range on highly diffuse objects (below 4%), incrementally reduce grazing reflecance to 0%.
    float reflectance90 = clamp(reflectance * 25.0, 0.0, 1.0);
    vec3 specularEnvironmentR0 = specularColor.rgb;
    vec3 specularEnvironmentR90 = vec3(1.0) * reflectance90;

    vec3 n = getNormal();
    vec3 v = normalize(camPos.xyz - inWorldPos);    // Vector from surface point to camera
    vec3 l = normalize(lightDir.xyz);     // Vector from surface point to light
    vec3 h = normalize(l+v);                        // Half vector between both l and v
    vec3 reflection = -normalize(reflect(v, n));
    reflection.y *= -1.0f;

    float NdotL = clamp(dot(n, l), 0.001, 1.0);
    float NdotV = clamp(abs(dot(n, v)), 0.001, 1.0);
    float NdotH = clamp(dot(n, h), 0.0, 1.0);
    float LdotH = clamp(dot(l, h), 0.0, 1.0);
    float VdotH = clamp(dot(v, h), 0.0, 1.0);

    PBRInfo pbrInputs = PBRInfo(
        NdotL,
        NdotV,
        NdotH,
        LdotH,
        VdotH,
        perceptualRoughness,
        metallic,
        specularEnvironmentR0,
        specularEnvironmentR90,
        alphaRoughness,
        diffuseColor,
        specularColor
    );

    // Calculate the shading terms for the microfacet specular shading model
    vec3 F = specularReflection(pbrInputs);
    float G = geometricOcclusion(pbrInputs);
    float D = microfacetDistribution(pbrInputs);

    const vec3 u_LightColor = vec3(1.0);

    // Calculation of analytical lighting contribution
    vec3 diffuseContrib = (1.0 - F) * diffuse(pbrInputs);
    vec3 specContrib = F * G * D / (4.0 * NdotL * NdotV);
    // Obtain final intensity as reflectance (BRDF) scaled by the energy of the light (cosine law)
    vec3 color = NdotL * u_LightColor * (diffuseContrib + specContrib);

    // Calculate lighting contribution from image based lighting source (IBL)
    color += getIBLContribution(pbrInputs, n, reflection);

    const float u_OcclusionStrength = 1.0f;
    const float u_EmissiveFactor = 1.0f;
    
    // Apply optional PBR terms for additional (optional) shading
    if ((materials[materialIdx].tex0 & MAP_AO) == MAP_AO)
        color = mix(color, color * texture(aoMap, inUV0).r, u_OcclusionStrength);
    else if ((materials[materialIdx].tex1 & MAP_AO) == MAP_AO)
        color = mix(color, color * texture(aoMap, inUV1).r, u_OcclusionStrength);    
    
    if ((materials[materialIdx].tex0 & MAP_EMISSIVE) == MAP_EMISSIVE)    
        color += SRGBtoLINEAR(texture(emissiveMap, inUV0)).rgb * u_EmissiveFactor;             
    else if ((materials[materialIdx].tex1 & MAP_EMISSIVE) == MAP_EMISSIVE)    
        color += SRGBtoLINEAR(texture(emissiveMap, inUV1)).rgb * u_EmissiveFactor;             
    
    //outColor = vec4(color, baseColor.a);
    outColor = tonemap(vec4(color, baseColor.a), exposure, gamma);
#ifdef DEBUG
    // Shader inputs debug visualization
    if (debugViewInputs > 0.0) {
        int index = int(debugViewInputs);
        switch (index) {
            case 1:
                if ((materials[materialIdx].tex0 & MAP_COLOR) == MAP_COLOR)        
                    outColor.rgba = texture(colorMap, inUV0);
                else if ((materials[materialIdx].tex1 & MAP_COLOR) == MAP_COLOR)
                    outColor.rgba = texture(colorMap, inUV1);
                else
                    outColor.rgba = vec4(1.0f);                
                break;
            case 2:
                if ((materials[materialIdx].tex0 & MAP_NORMAL) == MAP_NORMAL)        
                    outColor.rgb = texture(normalMap, inUV0).rgb;
                else if ((materials[materialIdx].tex1 & MAP_NORMAL) == MAP_NORMAL)
                    outColor.rgb = texture(normalMap, inUV1).rgb;
                else
                    outColor.rgb = normalize(inNormal);                
                break;
            case 3:
                if ((materials[materialIdx].tex0 & MAP_AO) == MAP_AO)        
                    outColor.rgb = texture(aoMap, inUV0).rrr;
                else if ((materials[materialIdx].tex1 & MAP_AO) == MAP_AO)
                    outColor.rgb = texture(aoMap, inUV1).rrr;
                else
                    outColor.rgb = vec3(0);
                break;
            case 4:
                if ((materials[materialIdx].tex0 & MAP_EMISSIVE) == MAP_EMISSIVE)        
                    outColor.rgb = texture(emissiveMap, inUV0).rgb;
                else if ((materials[materialIdx].tex1 & MAP_EMISSIVE) == MAP_EMISSIVE)
                    outColor.rgb = texture(emissiveMap, inUV1).rgb;
                else
                    outColor.rgb = vec3(0);
                break;
            case 5:
                if ((materials[materialIdx].tex0 & MAP_METALROUGHNESS) == MAP_METALROUGHNESS)        
                    outColor.rgb = texture(physicalDescriptorMap, inUV0).bbb;
                else if ((materials[materialIdx].tex1 & MAP_METALROUGHNESS) == MAP_METALROUGHNESS)
                    outColor.rgb = texture(physicalDescriptorMap, inUV1).bbb;
                else
                    outColor.rgb = vec3(0);
                break;
            case 6:
                if ((materials[materialIdx].tex0 & MAP_METALROUGHNESS) == MAP_METALROUGHNESS)        
                    outColor.rgb = texture(physicalDescriptorMap, inUV0).ggg;
                else if ((materials[materialIdx].tex1 & MAP_METALROUGHNESS) == MAP_METALROUGHNESS)
                    outColor.rgb = texture(physicalDescriptorMap, inUV1).ggg;
                else
                    outColor.rgb = vec3(0);
                break;
        }
        //outColor = SRGBtoLINEAR(outColor);
    }

    // PBR equation debug visualization
    // "none", "Diff (l,n)", "F (l,h)", "G (l,v,h)", "D (h)", "Specular"
    if (debugViewEquation > 0.0) {
        int index = int(debugViewEquation);
        switch (index) {
            case 1:
                outColor.rgb = diffuseContrib;
                break;
            case 2:
                outColor.rgb = F;
                break;
            case 3:
                outColor.rgb = vec3(G);
                break;
            case 4: 
                outColor.rgb = vec3(D);
                break;
            case 5:
                outColor.rgb = specContrib;
                break;              
        }
    }
#endif
}
