#version 450

#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable

layout (location = 0) in flat uint inContourID;
layout (location = 0) out uvec4 outFragColor;

void main() 
{
  outFragColor = uvec4(1u << gl_PrimitiveID, inContourID, 0, 1);
}