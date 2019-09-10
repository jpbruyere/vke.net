<h1 align="center">
vke.net 
  <br>  
<p align="center">
  <a href="https://www.nuget.org/packages/CVKL"><img src="https://buildstats.info/nuget/CVKL"></a>
  <a href="https://www.paypal.me/GrandTetraSoftware">
    <img src="https://img.shields.io/badge/Donate-PayPal-green.svg">
  </a>
</p>
</h1>

<p align="center">
  <a href="https://github.com/jpbruyere/vk.net/blob/master/samples/pbr/screenshot.png">
    <kbd><img src="https://raw.githubusercontent.com/jpbruyere/vk.net/master/samples/pbr/screenshot.png" height="300"></kbd>
  </a>
   <br>adaptation of the gltf PBR sample from Sacha Willems</br>
</p>
**Vulkan Engine for .net** is composed of high level classes encapsulating vulkan objects with `IDispose` model and **reference counting**. [GLFW](https://www.glfw.org/)  handles the windowing system.

### Requirements
- [GLFW](https://www.glfw.org/)
- [libstb](https://github.com/nothings/stb), on debian install **libstb-dev**.
- [Vulkan Sdk](https://www.lunarg.com/vulkan-sdk/), **glslc** has to be in the path.
- optionaly for ui, you will need [vkvg](https://github.com/jpbruyere/vkvg).

### Features

- physicaly based rendering, direct and deferred
- glTF 2.0
- ktx image loading.
- Memory pools

### VkWindow class

To create a new vulkan application, derrive your application from `VkWindow`. Validation and
debug reports may be activated with the static Fields of the `Instance` class.

```csharp
class Program : VkWindow {
	static void Main (string[] args) {
		Instance.VALIDATION = true;
		
		using (Program vke = new Program ()) {
			vke.Run ();
		}
	}
}
```

### Enabling features

Override the `configureEnabledFeatures` method of `VkWindow` to enable features.
```csharp
protected override void configureEnabledFeatures (VkPhysicalDeviceFeatures available_features, ref VkPhysicalDeviceFeatures enabled_features) {
	enabled_features.samplerAnisotropy = available_features.samplerAnisotropy;
}
```
### Creating queues

To create queues, override the `createQueues` method of `VkWindow`. This function is called before the logical device creation and will take care of physically available queues, creating duplicates if count exceed availability. The `base` method will create a default presentable queue.

```csharp
protected override void createQueues () {
	base.createQueues ();
	transferQ = new Queue (dev, VkQueueFlags.Transfer);
}
```
### Rendering

The constructor of the `VkWIndow` will finish the vulkan initialisation, so that you may create pipelines, buffers, and so on in your constructor.

VkWindow will provide the default swapchain, but it's up to you to create the frame buffers. For the triangle example, create them in the `OnResize` override.
```csharp
Framebuffer[] frameBuffers;

protected override void OnResize () {
	if (frameBuffers != null)
		for (int i = 0; i < swapChain.ImageCount; ++i)
			frameBuffers[i]?.Dispose ();
	frameBuffers = new Framebuffer[swapChain.ImageCount];

	for (int i = 0; i < swapChain.ImageCount; ++i) 
		frameBuffers[i] = new Framebuffer (pipeline.RenderPass, swapChain.Width, swapChain.Height,
			(pipeline.Samples == VkSampleCountFlags.SampleCount1) ? new Image[] {
				swapChain.images[i],
				null
			} : new Image[] {
				null,
				null,
				swapChain.images[i]
			});

	buildCommandBuffers ();
}
```

