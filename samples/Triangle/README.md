### The Project File.

To build a minimal vulkan application, add the [vke](https://www.nuget.org/packages/vke/) nuget package, and to enable automatic shader compilation, add the [SpirVTasks](https://www.nuget.org/packages/SpirVTasks/) package and a generic **GLSLShader** item globing a full directory.

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <TargetFrameworks>net472</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <ItemGroup>    
        <GLSLShader Include="shaders\*.*" />		
    </ItemGroup>
    <ItemGroup>
        <PackageReference Include="SpirVTasks" />
        <PackageReference Include="vke" />
    </ItemGroup>
</Project>

```

### VkWindow class

**vke** use [GLFW](https://www.glfw.org/) to interface with the windowing system of the OS. Derive your application from the `VkWindow` base class to start with a vulkan enabled window. **Validation** and **RenderDoc** layers loading may be control at startup with public static boolean properties from the `Instance`class.

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
### Vulkan Initialization

Default vulkan initialization of the VkWindow class will provide the minimal for running and present simple command buffers. For further initialization steps, override the `init_vulkan`method.

```csharp
protected override void initVulkan () {
   base.initVulkan ();
   vbo = new HostBuffer<Vertex> (dev, VkBufferUsageFlags.VertexBuffer, vertices);
   ibo = new HostBuffer<ushort> (dev, VkBufferUsageFlags.IndexBuffer, indices);
   uboMats = new HostBuffer (dev, VkBufferUsageFlags.UniformBuffer, matrices);   
   ...
```
### Enabling extensions

The `VkWindow` class provides two properties that you may override to enable additional extensions. The `Ext` static class of the vulkan package provides up to date lists of existing vulkan extensions for convenience.

```csharp
public virtual string[] EnabledInstanceExtensions => null;
public virtual string[] EnabledDeviceExtensions =>
    new string[] { Ext.D.VK_KHR_swapchain };
```

### Enabling features

Override the `configureEnabledFeatures` method of `VkWindow` to enable features. Available features queried from the selected physical device are provided as argument.
```csharp
protected override void configureEnabledFeatures (
    VkPhysicalDeviceFeatures available_features,
    ref VkPhysicalDeviceFeatures enabled_features) {
    
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

VkWindow will provide the default swapchain, but it's up to you to create the frame buffers. For the triangle example, create them in the `OnResize` override. The `RenderPass` class has the ability to create a framebuffer collection for a given swapchain. The `OnResize` method is guarantied to be called once before entering the rendering loop, so that it is a safe place to call the building of your command buffers.
```csharp
FrameBuffers frameBuffers;

protected override void OnResize () {
	base.OnResize ();

	frameBuffers?.Dispose();
	frameBuffers = pipeline.RenderPass.CreateFrameBuffers(swapChain);

    buildCommandBuffers ();
}
```

