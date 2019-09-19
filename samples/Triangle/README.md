
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
### Enabling extensions

The `VkWindow` class provides two properties that you may override to enable additional extensions.

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

