<h1 align="center">
  SpirVTasks MSBuild add-on
  <br>  
<p align="center">
  <a href="https://www.nuget.org/packages/SpirVTasks">
    <img src="https://buildstats.info/nuget/SpirVTasks">
  </a>
  <a href="https://www.paypal.me/GrandTetraSoftware">
    <img src="https://img.shields.io/badge/Donate-PayPal-green.svg">
  </a>  
</p>
</h1>

**SpirVTasks** package add **SpirV** compilation support to msbuild project. Error and warning are routed to the `IDE`.

## Usage
```xml
<ItemGroup>
  <PackageReference Include="SpirVTasks" Version="0.1.15-beta" />		
</ItemGroup>
<ItemGroup>    
  <GLSLShader Include="shaders\*.frag;shaders\*.vert;shaders\*.comp;shaders\*.geom" />
</ItemGroup> 
```
Resulting `.spv` files are embedded with resource ID = `ProjectName.file.ext.spv`. You can override the default resource id by adding a custom **LogicalName**.
```xml
<ItemGroup>    
  <GLSLShader Include="shaders\skybox.vert">
	  <LogicalName>NewName.vert.spv</LogicalName>
  </GLSLShader>
</ItemGroup> 
```

`VULKAN_SDK/bin` then `PATH` are searched for the `glslc` executable. You can also use the `SpirVglslcPath` property.
```xml
<PropertyGroup>
  <SpirVglslcPath>bin\glslc.exe</SpirVglslcPath>
</PropertyGroup>
```

## Include in glsl
```glsl
#include <preamble.inc>

layout (location = 0) in vec3 inColor;
layout (location = 0) out vec4 outFragColor;

void main() 
{
    outFragColor = vec4(inColor, 1.0);
}
```
Included files are searched from the location of the current parsed file, then in the `SpirVAdditionalIncludeDirectories`directories if present.
```xml
<PropertyGroup>
  <SpirVAdditionalIncludeDirectories>$(MSBuildThisFileDirectory)common;testdir;../anotherdir</SpirVAdditionalIncludeDirectories>
</PropertyGroup>
```

## Additional attributes

**Optimisation** attribute will set compiler flag for resulting code optimizations.
```
<GLSLShader Include="shaders\skybox.vert" Optimization="size"/>
```
Default optimization if this attribute is not present is **PERF**, accepted values are:
- NONE: no optimization.
- PERF: spirv will be optimized for performances.
- SIZE: resulting code size will be minimized.

**DefineConstants** attribute may contains a semicolon separated list of implicit **MACRO** to define for compilation.

```
<GLSLShader Include="shaders\skybox.vert" DefineConstants="DEBUG;SHADOW_FACTOR=0.15"/>
```

