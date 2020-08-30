#include "preamble.inc"

layout(push_constant) uniform PushConsts {
	float exposure;
	float gamma;	
};

#include "tonemap.inc"

layout (set = 0, binding = 0) uniform sampler2DMS samplerHDR;

layout (location = 0) in vec2 inUV;
layout (location = 0) out vec4 outColor;
									                                  
void main() 
{
	ivec2 ts = textureSize(samplerHDR);
	vec4 hdrColor = texelFetch (samplerHDR, ivec2(gl_FragCoord.xy), gl_SampleID);
	outColor = tonemap(hdrColor, exposure, gamma);
}