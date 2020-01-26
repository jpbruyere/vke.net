<h1 align="center">
    vke.net
    <br>  
    Vulkan Engine for .NET
    <br>  
<p align="center">
  <a href="https://www.nuget.org/packages/vke"><img src="https://buildstats.info/nuget/vke"></a>
  <a href="https://travis-ci.org/jpbruyere/vke.net">
      <img src="https://travis-ci.org/jpbruyere/vke.net.svg?branch=master">
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

**vke.net** (_vulkan engine for .net_) is composed of high level classes encapsulating [vulkan]() objects with `IDispose` model and **reference counting**. [GLFW](https://www.glfw.org/)  handles the windowing system.



Use the 'download_datas.sh' script for downloading sample's datas.

### Requirements
- [GLFW](https://www.glfw.org/) if you use the `VkWindow` class.
- If you want to use `jpg`, `jpeg`, `png` image [libstb](https://github.com/nothings/stb) (on debian install **libstb-dev**). Note that `ktx` image loading has no dependencies.
- [Vulkan Sdk](https://www.lunarg.com/vulkan-sdk/), **glslc** has to be in the path.
- optionaly for ui, you will need [vkvg](https://github.com/jpbruyere/vkvg).

### Features

- physicaly based rendering, direct and deferred
- glTF 2.0
- ktx image loading.
- Memory pools


