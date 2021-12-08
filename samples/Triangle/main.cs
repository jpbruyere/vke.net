// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using vke;
using Vulkan;
using Glfw;

//the traditional triangle sample
namespace Triangle {
	class Program : SampleBase {
		static void Main (string[] args) {
#if DEBUG
			Instance.VALIDATION = true;
#endif
			using (Program vke = new Program ()) {
				vke.Run ();
			}
		}

		const float rotSpeed = 0.01f, zoomSpeed = 0.01f;
		float rotX, rotY, zoom = 1f;

		//vertex structure
		[StructLayout(LayoutKind.Sequential)]
		struct Vertex {
			Vector3 position;
			Vector3 color;

			public Vertex (float x, float y, float z, float r, float g, float b) {
				position = new Vector3 (x, y, z);
				color = new Vector3 (r, g, b);
			}
		}

		HostBuffer ibo;     //a host mappable buffer to hold the indices.
		HostBuffer vbo;     //a host mappable buffer to hold vertices.
		HostBuffer<Matrix4x4> uboMVPmatrix; //a host mappable buffer for mvp matrice.

		DescriptorPool descriptorPool;
		DescriptorSet descriptorSet;//descriptor set for the mvp matrice.

		FrameBuffers frameBuffers;	//the frame buffer collection coupled to the swapchain images
		GraphicPipeline pipeline;   //the triangle rendering pipeline

		//triangle vertices (position + color per vertex) and indices.
		Vertex[] vertices = {
			new Vertex (-1.0f, -1.0f, 0.0f ,  1.0f, 0.0f, 0.0f),
			new Vertex ( 1.0f, -1.0f, 0.0f ,  0.0f, 1.0f, 0.0f),
			new Vertex ( 0.0f,  1.0f, 0.0f ,  0.0f, 0.0f, 1.0f),
		};
		ushort[] indices = new ushort[] { 0, 1, 2 };

		protected override void initVulkan () {
			base.initVulkan ();

			//first create the needed buffers
			vbo = new HostBuffer<Vertex> (dev, VkBufferUsageFlags.VertexBuffer, vertices);
			ibo = new HostBuffer<ushort> (dev, VkBufferUsageFlags.IndexBuffer, indices);
			//because mvp matrice may be updated by mouse move, we keep it mapped after creation.
			uboMVPmatrix = new HostBuffer<Matrix4x4> (dev, VkBufferUsageFlags.UniformBuffer, 1, true);

			//a descriptor pool to allocate the mvp matrice descriptor from.
			descriptorPool = new DescriptorPool (dev, 1, new VkDescriptorPoolSize (VkDescriptorType.UniformBuffer));

			//Graphic pipeline configuration are predefined by the GraphicPipelineConfig class,
			//which ease sharing config for several pipelines having lots in common.
			//Because 'ShaderInfo' instantiate temporary native ShaderModule, the GraphicPipelineConfig
			//class implement 'IDisposable' interface to dispose those modules once the pipeline(s) is created.
			using (GraphicPipelineConfig cfg = GraphicPipelineConfig.CreateDefault (VkPrimitiveTopology.TriangleList, VkSampleCountFlags.SampleCount1, false)) {
				//Create the pipeline layout, it will be automatically activated on pipeline creation, so that sharing layout among different pipelines will benefit
				//from the reference counting to automatically dispose unused layout on pipeline clean up. It's the same for DescriptorSetLayout.
				cfg.Layout = new PipelineLayout (dev,
					new DescriptorSetLayout (dev, new VkDescriptorSetLayoutBinding (0, VkShaderStageFlags.Vertex, VkDescriptorType.UniformBuffer)));
				//create a default renderpass with just a color attachment for the swapchain image, a default subpass is automatically created and the renderpass activation
				//will follow the pipeline life cicle and will be automatically disposed when no longuer used.
				cfg.RenderPass = new RenderPass (dev, swapChain.ColorFormat, cfg.Samples);
				//configuration of vertex bindings and attributes
				cfg.AddVertexBinding<Vertex> (0);
				cfg.AddVertexAttributes (0, VkFormat.R32g32b32Sfloat, VkFormat.R32g32b32Sfloat);//position + color

				//shader are automatically compiled by SpirVTasks if added to the project. The resulting shaders are automatically embedded in the assembly.
				//To specifiy that the shader path is a resource name, put the '#' prefix. Else the path will be search on disk.
				cfg.AddShaders (
					new ShaderInfo (dev, VkShaderStageFlags.Vertex, "#shaders.main.vert.spv"),
					new ShaderInfo (dev, VkShaderStageFlags.Fragment, "#shaders.main.frag.spv")
				);

				//create and activate the pipeline with the configuration we've just done.
				pipeline = new GraphicPipeline (cfg);
			}

			//because descriptor layout used for a pipeline are only activated on pipeline activation, descriptor set must not be allocated before, except if the layout has been manually activated,
			//but in this case, layout will need also to be explicitly disposed.
			descriptorSet = descriptorPool.Allocate (pipeline.Layout.DescriptorSetLayouts[0]);

			//Write the content of the descriptor, the mvp matrice.
			DescriptorSetWrites uboUpdate = new DescriptorSetWrites (descriptorSet, pipeline.Layout.DescriptorSetLayouts[0]);
			//Descriptor property of the mvp buffer will return a default descriptor with no offset of the full size of the buffer.
			uboUpdate.Write (dev, uboMVPmatrix.Descriptor);

			//allocate the default VkWindow buffers, one per swapchain image. Their will be only reset when rebuilding and not reallocated.
			cmds = cmdPool.AllocateCommandBuffer (swapChain.ImageCount);
		}

		//view update override, see base method for more informations.
		public override void UpdateView () {
			uboMVPmatrix.AsSpan()[0] =
				Matrix4x4.CreateFromAxisAngle (Vector3.UnitY, rotY) *
				Matrix4x4.CreateFromAxisAngle (Vector3.UnitX, rotX) *
				Matrix4x4.CreateTranslation (0, 0, -3f * zoom) *
				Helpers.CreatePerspectiveFieldOfView (Helpers.DegreesToRadians (45f), (float)swapChain.Width / (float)swapChain.Height, 0.1f, 256.0f);

			base.UpdateView ();
		}
		protected override void onMouseMove (double xPos, double yPos) {
			double diffX = lastMouseX - xPos;
			double diffY = lastMouseY - yPos;
			if (GetButton (MouseButton.Left) == InputAction.Press) {
				rotY -= rotSpeed * (float)diffX;
				rotX += rotSpeed * (float)diffY;
				updateViewRequested = true;
			} else if (GetButton (MouseButton.Right) == InputAction.Press) {
				zoom += zoomSpeed * (float)diffY;
				updateViewRequested = true;
			}
		}

		void buildCommandBuffers() {
			cmdPool.Reset (VkCommandPoolResetFlags.ReleaseResources);

			for (int i = 0; i < swapChain.ImageCount; ++i) {
				FrameBuffer fb = frameBuffers[i];
				cmds[i].Start ();

				pipeline.RenderPass.Begin (cmds[i], fb);

				cmds[i].SetViewport (swapChain.Width, swapChain.Height);
				cmds[i].SetScissor (swapChain.Width, swapChain.Height);

				cmds[i].BindDescriptorSet (pipeline.Layout, descriptorSet);

				cmds[i].BindPipeline (pipeline);

				cmds[i].BindVertexBuffer (vbo);
				cmds[i].BindIndexBuffer (ibo, VkIndexType.Uint16);
				cmds[i].DrawIndexed ((uint)indices.Length);

				pipeline.RenderPass.End (cmds[i]);

				cmds[i].End ();
			}
		}

		protected override void OnResize () {
			base.OnResize ();

			updateViewRequested = true;

			frameBuffers?.Dispose();
			frameBuffers = pipeline.RenderPass.CreateFrameBuffers(swapChain);

			buildCommandBuffers ();
		}
		//clean up
		protected override void Dispose (bool disposing) {
			dev.WaitIdle ();
			if (disposing) {
				if (!isDisposed) {
					//pipeline clean up will dispose PipelineLayout, DescriptorSet layouts and render pass automatically. If their reference count is zero, their handles will be destroyed.
					pipeline.Dispose ();
					//frame buffers are automatically activated on creation as for resources, so it requests an explicit call to dispose.
					frameBuffers?.Dispose();
					//the descriptor pool
					descriptorPool.Dispose ();
					//resources have to be explicityly disposed.
					vbo.Dispose ();
					ibo.Dispose ();
					uboMVPmatrix.Dispose ();
				}
			}

			base.Dispose (disposing);
		}
	}
}
