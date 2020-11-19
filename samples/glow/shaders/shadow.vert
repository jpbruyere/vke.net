#version 450

layout (location = 0) in vec3 inPos;

layout(push_constant) uniform PushConsts {
    mat4 model;
} pc;

out gl_PerVertex
{
  vec4 gl_Position;
};

void main()
{
	gl_Position = pc.model * vec4(inPos,1);
}