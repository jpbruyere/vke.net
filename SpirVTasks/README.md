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

**SpirVTasks** package add **SpirV** compilation support to **msbuild** projects through [Shaderrc](https://github.com/google/shaderc) which is part of the [lunarg vulkan sdk](https://www.lunarg.com/vulkan-sdk/).  Errors and warnings are routed to the `IDE`.

## Usage

To enable SpirV compilation, you need to add the [nuget package](https://www.nuget.org/packages/SpirVTasks) to each project and tell with the new **<GLSLShader>** item where your shader sources reside.

```xml
<ItemGroup>
  <PackageReference Include="SpirVTasks"/>
</ItemGroup>
<ItemGroup>
  <GLSLShader Include="shaders\*.frag;shaders\*.vert;shaders\*.comp;shaders\*.geom" />
</ItemGroup>
```
Resulting `.spv` files are automatically embedded with the resource ID: `ProjectName.file.ext.spv`. You can override this default id by adding a custom **LogicalName**.
```xml
<ItemGroup>
  <GLSLShader Include="shaders\*.vert">
	  <LogicalName>shaders.%(Filename)%(Extension).spv</LogicalName>
  </GLSLShader>
</ItemGroup>
```

`VULKAN_SDK/bin` then `PATH` are searched for the **`glslc`** executable. You can also use the `SpirVglslcPath` property.

```xml
<PropertyGroup>
  <SpirVglslcPath>bin\glslc.exe</SpirVglslcPath>
</PropertyGroup>
```

## Include in glsl

SpirVTasks add the ability to use **include** statements in your shader sources. Files are combined before compilation. Includes are not referenced in the project file with `GLSLShader` elements.

```glsl
#include <preamble.inc>

layout (location = 0) in vec3 inColor;
layout (location = 0) out vec4 outFragColor;

void main()
{
    outFragColor = vec4(inColor, 1.0);
}
```
Included files are searched from the location of the current parsed file, then in the `SpirVAdditionalIncludeDirectories` directories if present.
```xml
<PropertyGroup>
  <SpirVAdditionalIncludeDirectories>$(MSBuildThisFileDirectory)common;testdir;../anotherdir</SpirVAdditionalIncludeDirectories>
</PropertyGroup>
```

It is also valid to add additional include search paths individually for each `GLSLShader`.

```xml
<ItemGroup>
  <GLSLShader Include="shaders\*.vert">
	  <AdditionalIncludeDirectories>../include</AdditionalIncludeDirectories>
  </GLSLShader>
</ItemGroup>

```

## Additional attributes

**Optimisation** attribute will set compiler flag for resulting code optimizations.
```xml
<GLSLShader Include="shaders\skybox.vert" Optimization="size"/>
```
Default optimization if this attribute is not present is **PERF**, accepted values are:
- NONE: no optimization.
- PERF: spirv will be optimized for performances.
- SIZE: resulting code size will be minimized.

**DefineConstants** attribute may contains a semicolon separated list of implicit **MACRO** to define for compilation. Note that **project constants** are automatically added to the compilation unit.

```xml
<GLSLShader Include="shaders\skybox.vert" DefineConstants="DEBUG;SHADOW_FACTOR=0.15"/>
```

