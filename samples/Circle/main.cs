using System.ComponentModel;
// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Collections.Generic;
//using System.Text;
using vke;
using Vulkan;

//the traditional triangle sample
namespace Triangle {
	class Program : VkWindow {
		static void Main (string[] args) {
#if NETCOREAPP
			DllMapCore.Resolve.Enable (true);
#endif
			using (Program vke = new Program ()) {
				vke.Run ();
			}
		}
		Program () : base ("triangle", 800, 600, false) { }

		const float rotSpeed = 0.01f, zoomSpeed = 0.01f;
		float rotX, rotY, zoom = 1f;

		Matrix4x4 mvp;      //the model view projection matrix

		GPUBuffer ibo;     //a host mappable buffer to hold the indices.
		GPUBuffer vbo;     //a host mappable buffer to hold vertices.
		HostBuffer uboMats; //a host mappable buffer for mvp matrice.

		DescriptorPool descriptorPool;
		DescriptorSet descriptorSet;//descriptor set for the mvp matrice.

		FrameBuffers frameBuffers;	//the frame buffer collection coupled to the swapchain images
		GraphicPipeline pipeline;   //the triangle rendering pipeline

		Vector3 center = new Vector3(0.0f,0.0f,0);
		float radius = 2;
		float step = 0.01f;

		List<Vector3> verts = new List<Vector3>();
		List<UInt16> indices = null;

		protected override void configureEnabledFeatures (VkPhysicalDeviceFeatures available_features, ref VkPhysicalDeviceFeatures enabled_features) {
			base.configureEnabledFeatures (available_features, ref enabled_features);
			enabled_features.pipelineStatisticsQuery = true;
		}
		PipelineStatisticsQueryPool statPool;
		TimestampQueryPool timestampQPool;
		ulong[] results;

		protected override void initVulkan () {			
			base.initVulkan ();

			UpdateFrequency = 200;

			statPool = new PipelineStatisticsQueryPool (dev,
				VkQueryPipelineStatisticFlags.InputAssemblyVertices |
				VkQueryPipelineStatisticFlags.InputAssemblyPrimitives |
				VkQueryPipelineStatisticFlags.ClippingInvocations |
				VkQueryPipelineStatisticFlags.ClippingPrimitives |
				VkQueryPipelineStatisticFlags.FragmentShaderInvocations);

			timestampQPool = new TimestampQueryPool (dev);			
			verts.Add (center);
			for (float alpha = 0; alpha < System.MathF.PI *2f; alpha+=step) {
				verts.Add (new Vector3(center.X + MathF.Cos(alpha) * radius, center.Y + MathF.Sin(alpha) * radius, 0));
			}
			verts.Add (new Vector3(center.X + MathF.Cos(0) * radius, center.Y + MathF.Sin(0) * radius, 0));

			/*indices = new List<UInt16> ();
			for (UInt16 i = 1; i < (UInt16)verts.Count - 1; i++)
			{
				indices.Add (0);
				indices.Add (i);
				indices.Add ((UInt16)(i+1));
			}*/


			//first create the needed buffers
			vbo = new GPUBuffer<Vector3> (presentQueue, cmdPool , VkBufferUsageFlags.VertexBuffer, verts.ToArray());
			if (indices != null)
				ibo = new GPUBuffer<ushort> (presentQueue, cmdPool, VkBufferUsageFlags.IndexBuffer, indices.ToArray());
			//because mvp matrice may be updated by mouse move, we keep it mapped after creation.
			uboMats = new HostBuffer (dev, VkBufferUsageFlags.UniformBuffer, mvp, true);

			//a descriptor pool to allocate the mvp matrice descriptor from.
			descriptorPool = new DescriptorPool (dev, 1, new VkDescriptorPoolSize (VkDescriptorType.UniformBuffer));

			//Graphic pipeline configuration are predefined by the GraphicPipelineConfig class, which ease sharing config for several pipelines having lots in common.
			GraphicPipelineConfig cfg = GraphicPipelineConfig.CreateDefault (VkPrimitiveTopology.TriangleFan, VkSampleCountFlags.SampleCount1, false);
		
			//Create the pipeline layout, it will be automatically activated on pipeline creation, so that sharing layout among different pipelines will benefit
			//from the reference counting to automatically dispose unused layout on pipeline clean up. It's the same for DescriptorSetLayout.
			cfg.Layout = new PipelineLayout (dev,
				new DescriptorSetLayout (dev, new VkDescriptorSetLayoutBinding (0, VkShaderStageFlags.Vertex, VkDescriptorType.UniformBuffer)));
			//create a default renderpass with just a color attachment for the swapchain image, a default subpass is automatically created and the renderpass activation
			//will follow the pipeline life cicle and will be automatically disposed when no longuer used.
			cfg.RenderPass = new RenderPass (dev, swapChain.ColorFormat, cfg.Samples);
			//configuration of vertex bindings and attributes
			cfg.AddVertexBinding<Vector3> (0);
			cfg.AddVertexAttributes (0, VkFormat.R32g32b32Sfloat);//position

			//shader are automatically compiled by SpirVTasks if added to the project. The resulting shaders are automatically embedded in the assembly.
			//To specifiy that the shader path is a resource name, put the '#' prefix. Else the path will be search on disk.
			cfg.AddShaders (
				new ShaderInfo (dev, VkShaderStageFlags.Vertex, "#shaders.main.vert.spv"),
				new ShaderInfo (dev, VkShaderStageFlags.Fragment, "#shaders.main.frag.spv")
			);

			//create and activate the pipeline with the configuration we've just done.
			pipeline = new GraphicPipeline (cfg);

			//ShaderInfo used in this configuration with create the VkShaderModule's used
			//for creating the pipeline. They have to be disposed to destroy those modules
			//used only during pipeline creation.
			cfg.DisposeShaders ();

			//because descriptor layout used for a pipeline are only activated on pipeline activation, descriptor set must not be allocated before, except if the layout has been manually activated, 
			//but in this case, layout will need also to be explicitly disposed.
			descriptorSet = descriptorPool.Allocate (pipeline.Layout.DescriptorSetLayouts[0]);

			//Write the content of the descriptor, the mvp matrice.
			DescriptorSetWrites uboUpdate = new DescriptorSetWrites (descriptorSet, pipeline.Layout.DescriptorSetLayouts[0]);
			//Descriptor property of the mvp buffer will return a default descriptor with no offset of the full size of the buffer.
			uboUpdate.Write (dev, uboMats.Descriptor);

			//allocate the default VkWindow buffers, one per swapchain image. Their will be only reset when rebuilding and not reallocated.
			cmds = cmdPool.AllocateCommandBuffer (swapChain.ImageCount);
		}

		//view update override, see base method for more informations.
		public override void UpdateView () {
			mvp =
				Matrix4x4.CreateFromAxisAngle (Vector3.UnitY, rotY) *
				Matrix4x4.CreateFromAxisAngle (Vector3.UnitX, rotX) *
				Matrix4x4.CreateTranslation (0, 0, -5f * zoom) *
				Utils.CreatePerspectiveFieldOfView (Utils.DegreesToRadians (45f), (float)swapChain.Width / (float)swapChain.Height, 0.1f, 256.0f);

			uboMats.Update (mvp, (uint)Marshal.SizeOf<Matrix4x4> ());
			base.UpdateView ();
		}
		protected override void onMouseMove (double xPos, double yPos) {
			double diffX = lastMouseX - xPos;
			double diffY = lastMouseY - yPos;
			if (GetButton(Glfw.MouseButton.Left) == Glfw.InputAction.Press) {
				rotY -= rotSpeed * (float)diffX;
				rotX += rotSpeed * (float)diffY;
			} else if (GetButton (Glfw.MouseButton.Right) == Glfw.InputAction.Press) {
				zoom += zoomSpeed * (float)diffY;
			} else
				return;
			//VkWindow has a boolean for requesting a call to 'UpdateView', it will be
			//reset by the 'UpdateView' base method or the custom override.
			updateViewRequested = true;
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
				//cmds[i].BindIndexBuffer (ibo, VkIndexType.Uint16);
				
				statPool.Begin (cmds[i]);
				for (int j = 0; j < 100; j++)
				{
					cmds[i].Draw((uint)verts.Count, 1);					
					//cmds[i].DrawIndexed ((uint)indices.Count);
				}
				statPool.End (cmds[i]);

				pipeline.RenderPass.End (cmds[i]);

				cmds[i].End ();
			}
		}

		protected override void OnResize () {
			base.OnResize ();
			UpdateView ();

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
					ibo?.Dispose ();
					uboMats.Dispose ();
				}
			}

			base.Dispose (disposing);
		}
		public override void Update () {
			dev.WaitIdle ();
			
			results = statPool.GetResults ();
			for (int i = 0; i < statPool.RequestedStats.Length; i++) {
				Console.WriteLine ($"{statPool.RequestedStats[i].ToString(),-30} :{results[i],12:0,0} ");
			}
			Console.WriteLine();
		}
	}
}
