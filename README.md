<h1 align="center">
    vke.net
    <br>  
    Vulkan Engine for .NET
    <br>  
<p align="center">
  <a href="https://www.nuget.org/packages/vke"><img src="https://buildstats.info/nuget/vke"></a>
  <a href="https://travis-ci.org/jpbruyere/vke.net">
      <img src="https://img.shields.io/travis/jpbruyere/vke.net.svg?&logo=travis&logoColor=white">
  </a>
  <a href="https://ci.appveyor.com/project/jpbruyere/vke-net">
	<img src="https://img.shields.io/appveyor/ci/jpbruyere/vke-net?label=Windows&logo=appveyor&logoColor=lightgrey">
  </a>
  <a href="https://www.paypal.me/GrandTetraSoftware">
    <img src="https://img.shields.io/badge/Donate-PayPal-green.svg">
  </a>
</p>
</h1>

<p align="center">
  <a href="https://github.com/jpbruyere/vke.net/blob/master/samples/pbr/screenshot.png">
    <kbd><img src="https://raw.githubusercontent.com/jpbruyere/vke.net/master/samples/pbr/screenshot.png" height="300"></kbd>
  </a>
   <br>adaptation of the gltf PBR sample from Sacha Willems</br>
</p>

**vke.net** (_vulkan engine for .net_) is composed of high level classes encapsulating [vulkan]() objects and commands with `IDispose` model and **reference counting**. [GLFW](https://www.glfw.org/)  handles the windowing system.

Vke use autogenerated [vk.net](https://github.com/jpbruyere/vk.net) library for low level binding to vulkan.

Use the 'download_datas.sh' script for downloading sample's datas.

### Requirements
- [GLFW](https://www.glfw.org/) if you use the `VkWindow` class.
- If you want to use `jpg`, `jpeg`, `png` image [libstb](https://github.com/nothings/stb) (on debian install **libstb-dev**). Note that `ktx` image loading has no dependencies.
- [Vulkan Sdk](https://www.lunarg.com/vulkan-sdk/), **glslc** has to be in the path.
- optionaly for ui, you will need [vkvg](https://github.com/jpbruyere/vkvg).

### Quick Start

Create a new dotnet console project, and add the [vje nuget package](https://www.nuget.org/packages/vke) to it.

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <TargetFrameworks>net472</TargetFrameworks>
    <OutputType>Exe</OutputType>
    
    <ItemGroup>
	    <PackageReference Include="vke" />
    </ItemGroup>
</Project>
```
For automatic shader compilation to SpirV, add also the [SpirVTasks nuget package](https://www.nuget.org/packages/SpirVTasks/0.1.41-beta). For documentation about this module, follow [this link](SpirVTasks/README.md).

```xml
    <ItemGroup>    
		<PackageReference Include="SpirVTasks" />
        <GLSLShader Include="shaders\*.*" />		
    </ItemGroup>

```
### Samples

|                Title                 |                    Screen shots                    |
| :----------------------------------: | :------------------------------------------------: |
| [ClearScreen](ClearScreen/README.md) | ![screenshot](samples/screenShots/ClearScreen.png) |
|    [Triangle](Triangle/README.md)    |  ![screenshot](samples/screenShots/Triangle.png)   |
|    [Textured](Textured/README.md)    |  ![screenshot](samples/screenShots/Textured.png)   |



### Features

- physicaly based rendering, direct and deferred
- glTF 2.0
- ktx image loading.
- Memory pools


