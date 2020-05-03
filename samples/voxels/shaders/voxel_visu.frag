// adaptation of:
// A simple fragment shader path tracer used to visualize 3D textures.
// Author:	Fredrik Präntare <prantare@gmail.com>
// Date:	11/26/2016
#version 450

#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable

#define STEP_LENGTH 0.005f
#define INV_STEP_LENGTH (1.0f/STEP_LENGTH)

layout (set = 0, binding = 0) uniform sampler2D textureBack;
layout (set = 0, binding = 1) uniform sampler2D textureFront;
layout (set = 0, binding = 2) uniform sampler3D vxTex3d;

layout(push_constant) uniform PushConsts {
    vec3 cameraPosition; // World camera position.
};


layout (location = 0) in vec2 textureCoordinateFrag; 
layout (location = 0) out vec4 color;

// Scales and bias a given vector (i.e. from [-1, 1] to [0, 1]).
vec3 scaleAndBias(vec3 p) { return 0.5f * p + vec3(0.5f); }

// Returns true if p is inside the unity cube (+ e) centered on (0, 0, 0).
bool isInsideCube(vec3 p, float e) { return abs(p.x) < 1 + e && abs(p.y) < 1 + e && abs(p.z) < 1 + e; }

void main() {
	const float mipmapLevel = 0;

	// Initialize ray.
	const vec3 origin = isInsideCube(cameraPosition, 0.2f) ? 
		cameraPosition : texture(textureFront, textureCoordinateFrag).xyz;
	vec3 direction = texture(textureBack, textureCoordinateFrag).xyz - origin;
	const uint numberOfSteps = uint(INV_STEP_LENGTH * length(direction));
	direction = normalize(direction);

	// Trace.
	color = vec4(0.0f);
	for(uint step = 0; step < numberOfSteps && color.a < 0.99f; ++step) {
		const vec3 currentPoint = origin + STEP_LENGTH * step * direction;
		vec3 coordinate = scaleAndBias(currentPoint);
		vec4 currentSample = textureLod(vxTex3d, scaleAndBias(currentPoint), mipmapLevel);
		color += (1.0f - color.a) * currentSample;
	} 
	color.rgb = pow(color.rgb, vec3(1.0 / 2.2));
}