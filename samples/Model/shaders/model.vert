#version 450

#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable

layout (location = 0) in vec3 inPos;
layout (location = 1) in vec3 inNormal;
layout (location = 2) in vec2 inUV;

layout (binding = 0) uniform UBO 
{
	mat4 projectionMatrix;
    mat4 viewMatrix;
	mat4 modelMatrix;
} ubo;

layout (location = 0) out vec2 outUV;
layout (location = 1) out vec3 outN;
layout (location = 2) out vec3 outV;
layout (location = 3) out vec3 outL;
layout (location = 4) out vec3 outP;

out gl_PerVertex 
{
    vec4 gl_Position;   
};

layout(push_constant) uniform PushConsts {
    mat4 model;
} pc;

vec3 light = vec3(2.0,2.0,-2.0);

void main() 
{
    outUV = inUV;
    
    mat4 mod = ubo.modelMatrix * pc.model;
    vec4 pos = mod * vec4(inPos.xyz, 1.0);
    vec3 lPos = mat3(mod) * light;
    
    //outN = normalize(transpose(inverse(mat3(mod))) * inNormal);    
    outN = mat3(mod)* inNormal;    
    
    mat4 vi = inverse(ubo.viewMatrix);
    vec4 vit = vi[3];
    outV = vec3(vit[0],vit[1],vit[2]); //normalize(vec3(viewInv * vec4(0.0, 0.0, 0.0, 1.0) - pos));
    outL = lPos - pos.xyz;
    outP = -pos.xyz;
    
	gl_Position = ubo.projectionMatrix * ubo.viewMatrix * pos;    
}
