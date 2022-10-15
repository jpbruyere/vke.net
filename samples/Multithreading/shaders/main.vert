#version 450

#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable

layout (location = 0) in vec3 inPos;
layout (location = 1) in vec3 inColor;

layout(push_constant) uniform PushConsts {
	vec2 size;
} pc;

layout (location = 0) out vec3 outColor;

out gl_PerVertex
{
    vec4 gl_Position;
};


void main()
{
	outColor = inColor;
	gl_Position = vec4(inPos.xy * vec2(2) / pc.size - vec2(1), 0.0, 1.0);
}
