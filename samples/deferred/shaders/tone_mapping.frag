#include "preamble.inc"

layout(push_constant) uniform PushConsts {
	float exposure;
	float gamma;
	float debug;
};

#include "tonemap.inc"

layout (set = 0, binding = 0) uniform sampler2D samplerHDR;
layout (set = 0, binding = 1) uniform sampler2D bloom;

layout (location = 0) in vec2 inUV;
layout (location = 0) out vec4 outColor;
									                                  
void main() 
{
	if (debug < 0.0f) {    
	    //vec4 hdrColor = texelFetch (samplerHDR, ivec2(inUV), gl_SampleID);    
	    vec4 hdrColor = texture (samplerHDR, inUV);    
	    //vec4 c = texture (bloom, inUV);
	    //float lum = (0.299*c.r + 0.587*c.g + 0.114*c.b);
	    //if (lum>1.0)
	    //    hdrColor.rgb += c.rgb * 0.05;
	    //outColor = SRGBtoLINEAR(tonemap(hdrColor, exposure, gamma));	    
	    outColor = tonemap(hdrColor, exposure, gamma);	    
    }else
    	outColor = texture (bloom, inUV);
    
    
    /*
    outColor = vec4(SRGBtoLINEAR(tonemap(hdrColor.rgb)), hdrColor.a);;*/
    
/*  vec3 mapped = vec3(1.0) - exp(-hdrColor.rgb * pc.exposure);        
    mapped = pow(mapped, vec3(1.0 / pc.gamma));
    outColor = vec4(mapped, hdrColor.a);*/
}