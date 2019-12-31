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

layout (location = 0) out vec3 pos;
layout (location = 1) out vec3 outN;


layout(push_constant) uniform PushConsts {
    mat4 model;
} pc;

vec3 light = vec3(2.0,2.0,-2.0);

void main() 
{
    mat4 mod = ubo.modelMatrix;// * pc.model;
    pos = (mod * vec4(inPos.xyz, 1.0)).xyz;
    
	//gl_Position = pos;    
	outN = mat3(mod)* inNormal;
}
