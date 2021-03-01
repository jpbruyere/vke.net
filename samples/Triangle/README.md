### Creating buffers

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
Graphic pipeline configuration are predefined by the [`GraphicPipelineConfig`](../../../../wiki/vke.GraphicPipelineConfig) class, which ease sharing configs for several pipelines having lots in common. The pipeline layout will be automatically activated on pipeline creation, so that sharing layout among different pipelines will benefit from the reference counting to automatically dispose unused layout on pipeline clean up. It's the same for [`DescriptorSetLayout`](../../wiki/api/DescriptorSetLayout).
```csharp
GraphicPipelineConfig cfg = GraphicPipelineConfig.CreateDefault (
      VkPrimitiveTopology.TriangleList, VkSampleCountFlags.SampleCount1, false);
      
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
shader are automatically compiled by [`SpirVTasks`](../../SpirVTasks/README.md) if added to the project. The resulting shaders are automatically embedded in the assembly. To specifiy that the shader path is a resource name, put the **'#'** prefix. Else the path will be search on disk.
```csharp
cfg.AddShader (dev, VkShaderStageFlags.Vertex, "#shaders.main.vert.spv");
cfg.AddShader (dev, VkShaderStageFlags.Fragment, "#shaders.main.frag.spv");
```
Once the pipeline configuration is complete, we use it to effectively create and activate a graphic pipeline. Activables used by the pipeline (like the RenderPass, or the PipelineLayout) are referenced in the newly created managed pipeline. So the Configuration object doesn't need cleanup.
```csharp
	pipeline = new GraphicPipeline (cfg);
```
Because descriptor layouts used for a pipeline are only activated on pipeline activation, descriptor sets must not be allocated before, except if the layout has been manually activated, but in this case, layouts will also need to be explicitly disposed.
```csharp
	descriptorSet = descriptorPool.Allocate (pipeline.Layout.DescriptorSetLayouts[0]);
```
The descriptor update is a two step operation. First we create a [`DescriptorSetWrites`](../../../../wiki/vke.DescriptorSetWrites) object defining the layout(s), than we write the descriptor(s).
The `Descriptor` property of the mvp HostBuffer will return a default descriptor with no offset of the full size of the buffer.

```csharp
DescriptorSetWrites uboUpdate =
    new DescriptorSetWrites (descriptorSet, pipeline.Layout.DescriptorSetLayouts[0]);

uboUpdate.Write (dev, uboMats.Descriptor);
```
