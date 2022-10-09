#version 450

#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable

layout (location = 0) in vec2 inPos;

layout(push_constant) uniform PushConsts {
    vec2 size;
};

out gl_PerVertex 
{
    vec4 gl_Position;   
};


void main() 
{	
    gl_Position = vec4(inPos.xy * vec2(2) / size - vec2(1), 0.0, 1.0);
}
