#version 450

#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable

layout (set = 1, binding = 0) uniform sampler2D samplerColor;
layout (set = 1, binding = 1) uniform sampler2D samplerNormal;
layout (set = 1, binding = 2) uniform sampler2D samplerOcclusion;
layout (set = 1, binding = 3, rgba8) uniform image3D vxTex3d;

layout (location = 0) in vec3 worldPositionFrag;
layout (location = 1) in vec3 normalFrag;

vec3 scaleAndBias(vec3 p) { return 0.5f * p + vec3(0.5f); }

bool isInsideCube(const vec3 p, float e) { return abs(p.x) < 1 + e && abs(p.y) < 1 + e && abs(p.z) < 1 + e; }

void main() 
{
	if(!isInsideCube(worldPositionFrag, 0)) return;
	
	vec3 voxel = scaleAndBias (worldPositionFrag);
	ivec3 dim = imageSize (vxTex3d);
	
	imageStore(vxTex3d, ivec3(dim * voxel), vec4(1,0,0,1));
}
