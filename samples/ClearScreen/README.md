# The Project File.
Create a new dotnet console project, and add the [vke nuget package](https://www.nuget.org/packages/vke) to it.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <TargetFrameworks>netcoreapp3.1</TargetFrameworks>
  <OutputType>Exe</OutputType>

  <ItemGroup>
    <PackageReference Include="vke" />
  </ItemGroup>
</Project>
```
# VkWindow class

**vke** use [GLFW](https://www.glfw.org/) to interface with the windowing system of the OS. Derive your application from the `VkWindow` base class to start with a vulkan enabled window. **Validation** and **RenderDoc** layers loading may be control at startup with public static boolean properties from the `Instance`class.

```csharp
class Program : VkWindow {
  static void Main (string[] args) {

    Instance.Validation = true;

    using (Program vke = new Program ()) {
      vke.Run ();
    }
  }
}
```

### Vulkan Initialization

**`initVulkan`** is the first method called by the **'Run'** method. Default initialization will provide a vulkan window, a default swap chain bound to it, and a draw and present semaphore to sync the rendering.
```csharp
protected override void initVulkan () {
    base.initVulkan ();
```
There are several method to clear the screen with vulkan. One is to use the renderpass CLEAR load operation so that attachment layout transitioning is handled automatically by the render pass.
```csharp
    renderPass = new RenderPass (dev, swapChain.ColorFormat);
    renderPass.ClearValues[0] = new VkClearValue (0.1f, 0.2f, 1);
    renderPass.Activate ();

    cmds = cmdPool.AllocateCommandBuffer (swapChain.ImageCount);
}
```

Note that because we only reset the command buffers when rebuilding these, we need to preallocate them during the initialization.

### Frame buffer creation

The **`OnResize`** method is called at least once before any rendering, so it's a safe place to initialize output size related vulkan objects like the frame buffers. vke provide a FrameBuffer collection object to ease handling of multiple related buffers like those used for a swap chain for example..
```csharp
FrameBuffers frameBuffers;

protected override void OnResize () {
	base.OnResize ();

	frameBuffers?.Dispose();
	frameBuffers = renderPass.CreateFrameBuffers(swapChain);

	buildCommandBuffers ();
}
```
It's common to rebuild the command buffers targeting the swap chain images after a resize so that the drawing is scaled. So it's a good idea to build/rebuild your commands here.



### The command buffers

The `VkWindow` class has a default array of command buffers, one for each swap chain image. But it's up to you to allocate and populate them.
Here we simply record a begin/end render pass to clear the screen with the load operation of it.

```csharp
void buildCommandBuffers() {
	cmdPool.Reset (VkCommandPoolResetFlags.ReleaseResources);

	for (int i = 0; i < swapChain.ImageCount; ++i) {
        cmds[i].Start ();
        renderPass.Begin (cmds[i], frameBuffers[i]);
        renderPass.End (cmds[i]);
        cmds[i].End ();
	}
}
```
