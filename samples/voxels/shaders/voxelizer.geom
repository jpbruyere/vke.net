#version 420

layout (triangles) in;
layout (triangle_strip, max_vertices = 3) out;


layout (location = 0) in vec3 worldPositionGeom[];
layout (location = 1) in vec3 normalGeom[];

layout (location = 0) out vec3 worldPositionFrag;
layout (location = 1) out vec3 normalFrag;

#include "constants.inc"

void main() 
{
	const vec3 p1 = worldPositionGeom[1] - worldPositionGeom[0];
	const vec3 p2 = worldPositionGeom[2] - worldPositionGeom[0];
	const vec3 p = abs(cross(p1, p2)); 
	for(uint i = 0; i < 3; ++i){
		worldPositionFrag = worldPositionGeom[i];
		normalFrag = normalGeom[i];
		if(p.z > p.x && p.z > p.y){
			gl_Position = vec4(worldPositionFrag.x, worldPositionFrag.y, 0, 1);
		} else if (p.x > p.y && p.x > p.z){
			gl_Position = vec4(worldPositionFrag.y, worldPositionFrag.z, 0, 1);
		} else {
			gl_Position = vec4(worldPositionFrag.x, worldPositionFrag.z, 0, 1);
		}
		EmitVertex();
	}
	EndPrimitive();
}