#version 450

#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable

layout (set = 1, binding = 0) uniform sampler2D samplerColor;
layout (set = 1, binding = 1) uniform sampler2D samplerNormal;
layout (set = 1, binding = 2) uniform sampler2D samplerOcclusion;

layout (location = 0) in vec2 inUV;
layout (location = 1) in vec3 inN;
layout (location = 2) in vec3 inV;
layout (location = 3) in vec3 inL;
layout (location = 4) in vec3 inP;

layout (location = 0) out vec4 outFragColor;



vec3 perturb_normal(vec3 N, vec3 P, vec2 uv)
{
    // assume N, the interpolated vertex normal and 
    // V, the view vector (vertex to eye)
    vec3 map = texture(samplerNormal, uv).xyz * 2.0 - 1.0;
    // get edge vectors of the pixel triangle
    vec3 dp1 = dFdx( P );
    vec3 dp2 = dFdy( P );
    vec2 duv1 = dFdx( uv );
    vec2 duv2 = dFdy( uv );
 
    // solve the linear system
    vec3 dp2perp = cross( dp2, N );
    vec3 dp1perp = cross( N, dp1 );
    vec3 T = dp2perp * duv1.x + dp1perp * duv2.x;
    vec3 B = dp2perp * duv1.y + dp1perp * duv2.y;
 
    // construct a scale-invariant frame 
    float invmax = inversesqrt( max( dot(T,T), dot(B,B) ) );
    mat3 TBN = mat3( T * invmax, B * invmax, N );
         
    return normalize(TBN * map);
}

void main() 
{
    vec4 color = texture(samplerColor, inUV);

    vec3 N = normalize(inN);
    vec3 V = normalize(inV);
    vec3 L = normalize(inL);
    vec3 P = normalize(inP);
    
    N = perturb_normal(N, P, inUV); 
    
    vec3 R = reflect(-L, N);
    vec3 diff = max(dot(N, L), 0.0) * texture(samplerOcclusion, inUV).rgb;
    vec3 specular = pow(max(dot(R, V), 0.0), 16.0) * vec3(1.0);
    
    outFragColor = vec4(diff * color.rgb + specular, 1.0);
    
    /*float fr = dot(N,V);
    */
    
}
