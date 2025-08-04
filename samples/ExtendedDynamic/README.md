# Shaders
For this tutorials we'll need a `vertex` and a `fragment` shaders. Vulkan need them to be compiled into [SPIR-V](https://www.khronos.org/spir/). Install the [Vulkan Sdk](https://www.lunarg.com/vulkan-sdk/) and after building it, ensure the `VULKAN_SDK` environment variable points to its binary subdir.
```bash
export VULKAN_SDK=/VulkanSDK/1.2.176.1/x86_64
```
To enable automatic shader compilation during build, add the [SpirVTasks package](https://www.nuget.org/packages/SpirVTasks/) and a generic **GLSLShader** item globing a full directory.
```xml
<ItemGroup>
  <PackageReference Include="SpirVTasks" />
</ItemGroup>
<ItemGroup>
  <GLSLShader Include="shaders\*.*" />
</ItemGroup>
```
See [SpirVTasks documentation](https://github.com/jpbruyere/vke.net/tree/master/SpirVTasks) for more informations.

# Creating buffers

Vke has two classes to handle buffers. Mappable [`HostBuffer`](../../../../wiki/vke.HostBuffer) and device only [`GPUBuffer`](../../../../wiki/vke.GPUBuffer).
For this first simple example, we will only use host mappable buffers. Those classes can handle a Generic argument of a blittable type to handle arrays. Resources like buffers or images are activated in constructor, and they need to be explicitly disposed on cleanup. Create them in the `initVulkan` override.

```csharp
//the vertex buffer
vbo = new HostBuffer<Vertex> (dev, VkBufferUsageFlags.VertexBuffer, vertices);
//the index buffer
ibo = new HostBuffer<ushort> (dev, VkBufferUsageFlags.IndexBuffer, indices);
//a permanantly mapped buffer for the mvp matrice
uboMats = new HostBuffer (dev, VkBufferUsageFlags.UniformBuffer, mvp, true);
```

To be able to access the mvp matrix in a shader, we need a descriptor. This implies to create a descriptor  pool to allocate it from and configure the triangle pipeline layout with a corresponding descriptor layout for our matrix.
```csharp
descriptorPool = new DescriptorPool (dev, 1, new VkDescriptorPoolSize (VkDescriptorType.UniformBuffer));
```
# Configuring pipelines

Graphic pipeline configuration are predefined by the [`GraphicPipelineConfig`](../../../../wiki/vke.GraphicPipelineConfig) class, which ease sharing configs for several pipelines having lots in common. The pipeline layout will be automatically activated on pipeline creation, so that sharing layout among different pipelines will benefit from the reference counting to automatically dispose unused layout on pipeline clean up. It's the same for [`DescriptorSetLayout`](../../wiki/api/DescriptorSetLayout).
```csharp
using (GraphicPipelineConfig cfg = GraphicPipelineConfig.CreateDefault (
      VkPrimitiveTopology.TriangleList, VkSampleCountFlags.SampleCount1, false)) {

  cfg.Layout = new PipelineLayout (dev,
    new DescriptorSetLayout (dev,
       new VkDescriptorSetLayoutBinding (
         0, VkShaderStageFlags.Vertex, VkDescriptorType.UniformBuffer)));
```
Next we configure a default [`RenderPass`](../../../../wiki/vke.RenderPass) with just a color attachment for the swap chain image, a default sub-pass is automatically created and the render pass activation will follow the pipeline life cycle and will be automatically disposed when no longer in use.
```csharp
	cfg.RenderPass = new RenderPass (dev, swapChain.ColorFormat, cfg.Samples);
```
Configuration of vertex bindings and attributes
```csharp
cfg.AddVertexBinding<Vertex> (0);
cfg.AddVertexAttributes (0, VkFormat.R32g32b32Sfloat,	//position
                            VkFormat.R32g32b32Sfloat);//color
```
# Adding the shaders
Add both vertex and fragment shaders to the globbed directory of your `.csproj`

##### triangle.vert
```glsl
#version 450

#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable

layout (location = 0) in vec3 inPos;
layout (location = 1) in vec3 inColor;

layout (binding = 0) uniform UBO
{
	mat4 mvp;
};

layout (location = 0) out vec3 outColor;

out gl_PerVertex
{
  vec4 gl_Position;
};

void main()
{
	outColor = inColor;
	gl_Position = mvp * vec4(inPos.xyz, 1.0);
}
```
##### triangle.frag
```glsl
#version 450

#extension GL_ARB_separate_shader_objects : enable
#extension GL_ARB_shading_language_420pack : enable

layout (location = 0) in vec3 inColor;
layout (location = 0) out vec4 outFragColor;

void main()
{
  outFragColor = vec4(inColor, 1.0);
}
```

Shaders will be compiled into spir-v automatically during build by the `SpirVTasks`. The resulting shaders will be embedded in the assembly. To specifiy that the shader path is a resource name, put the '**`#`**' prefix. Else the path will be search on disk.
```csharp
cfg.AddShader (dev, VkShaderStageFlags.Vertex, "#shaders.triangle.vert.spv");
cfg.AddShader (dev, VkShaderStageFlags.Fragment, "#shaders.triangle.frag.spv");
```
Because native ShaderModule used during pipeline creation may be distroyed once the pipeline is created, The PipelineConfig class implement the
'IDisposable' interface to release those pointers automaticaly.

# Creating the pipeline
Once the pipeline configuration is complete, we use it to effectively create and activate a new graphic pipeline. Activables used by the pipeline (like the RenderPass, or the PipelineLayout) are referenced in the newly created managed pipeline. So the Configuration object doesn't need cleanup.
```csharp
	pipeline = new GraphicPipeline (cfg);
```
# Descriptor allocation
Because descriptor layouts used for a pipeline are only activated on pipeline activation, descriptor sets must not be allocated before, except if the layout has been manually activated, but in this case, layouts will also need to be explicitly disposed.
```csharp
	descriptorSet = descriptorPool.Allocate (pipeline.Layout.DescriptorSetLayouts[0]);
```
# Descriptor update

The descriptor update is a two step operation. First we create a [`DescriptorSetWrites`](../../../../wiki/vke.DescriptorSetWrites) object defining the layout(s), than we write the descriptor(s).
The `Descriptor` property of the mvp HostBuffer will return a default descriptor with no offset of the full size of the buffer.

```csharp
DescriptorSetWrites uboUpdate =
    new DescriptorSetWrites (descriptorSet, pipeline.Layout.DescriptorSetLayouts[0]);

uboUpdate.Write (dev, uboMats.Descriptor);
```

# Updating the view

Override the `UpdateView` method of the `VkWindow` class to update view related stuff like matrices.

```csharp
public override void UpdateView () {
  mvp = Matrix4x4.Create ...
  uboMats.Update (mvp, (uint)Marshal.SizeOf<Matrix4x4> ());
  base.UpdateView ();
}
```
This method is called at least once before the rendering loop just after 'OnResize'.
Then, it is triggered in the render loop each time the `updateViewRequested` field of `VkWindow` is set to 'true',
don't forget to reset `updateViewRequested` to 'false' or call the `base.UpdateView()` which will reset this boolean.

In a typical application, the mouse movements will set `updateViewRequested` to true.
```csharp
protected override void onMouseMove (double xPos, double yPos) {
  updateViewRequested = true;
```
