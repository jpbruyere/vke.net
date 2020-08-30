#version 450

#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable

layout (location = 0) in vec3 inPos;
layout (location = 1) in vec2 inTex;

layout (binding = 0) uniform UBO 
{
	mat4 projectionMatrix;
    mat4 viewMatrix;
	mat4 modelMatrix;
} ubo;

layout (location = 0) out vec2 outTex;

out gl_PerVertex 
{
    vec4 gl_Position;   
};


void main() 
{
	outTex = inTex;
	gl_Position = ubo.projectionMatrix * ubo.viewMatrix * ubo.modelMatrix * vec4(inPos.xyz, 1.0);
}
