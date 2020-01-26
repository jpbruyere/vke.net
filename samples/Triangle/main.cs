// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using vke;
using Vulkan;

namespace Triangle {
	class Program : VkWindow {
		static void Main (string[] args) {
			using (Program vke = new Program ()) {
				vke.Run ();
			}
		}

		const float rotSpeed = 0.01f, zoomSpeed = 0.01f;
		float rotX, rotY, rotZ = 0f, zoom = 1f;

		[StructLayout (LayoutKind.Sequential)]
		struct Matrices {
			public Matrix4x4 projection;
			public Matrix4x4 view;
			public Matrix4x4 model;
		}

		[StructLayout(LayoutKind.Sequential)]
		struct Vertex {
			Vector3 position;
			Vector3 color;

			public Vertex (float x, float y, float z, float r, float g, float b) {
				position = new Vector3 (x, y, z);
				color = new Vector3 (r, g, b);
			}
		}

		Matrices matrices;

		HostBuffer ibo;
		HostBuffer vbo;
		HostBuffer uboMats;

		DescriptorPool descriptorPool;
		DescriptorSet descriptorSet;

		FrameBuffers frameBuffers;
		GraphicPipeline pipeline;

		Vertex[] vertices = {
			new Vertex (-1.0f, -1.0f, 0.0f ,  1.0f, 0.0f, 0.0f),
			new Vertex ( 1.0f, -1.0f, 0.0f ,  0.0f, 1.0f, 0.0f),
			new Vertex ( 0.0f,  1.0f, 0.0f ,  0.0f, 0.0f, 1.0f),
		};
		ushort[] indices = new ushort[] { 0, 1, 2 };

		Program () : base () {}

		protected override void initVulkan () {
			base.initVulkan ();

			vbo = new HostBuffer<Vertex> (dev, VkBufferUsageFlags.VertexBuffer, vertices);
			ibo = new HostBuffer<ushort> (dev, VkBufferUsageFlags.IndexBuffer, indices);
			uboMats = new HostBuffer (dev, VkBufferUsageFlags.UniformBuffer, matrices);

			descriptorPool = new DescriptorPool (dev, 1, new VkDescriptorPoolSize (VkDescriptorType.UniformBuffer));

			GraphicPipelineConfig cfg = GraphicPipelineConfig.CreateDefault (VkPrimitiveTopology.TriangleList, VkSampleCountFlags.SampleCount1, false);

			cfg.Layout = new PipelineLayout (dev,
				new DescriptorSetLayout (dev,
					new VkDescriptorSetLayoutBinding (0, VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment, VkDescriptorType.UniformBuffer)));

			cfg.RenderPass = new RenderPass (dev, swapChain.ColorFormat, cfg.Samples);
			cfg.AddVertexBinding<Vertex> (0);
			cfg.AddVertexAttributes (0, VkFormat.R32g32b32Sfloat, VkFormat.R32g32b32Sfloat);

			cfg.AddShader (VkShaderStageFlags.Vertex, "#shaders.main.vert.spv");
			cfg.AddShader (VkShaderStageFlags.Fragment, "#shaders.main.frag.spv");

			pipeline = new GraphicPipeline (cfg);

			//note that descriptor set is allocated after the pipeline creation that use this layout, layout is activated
			//automaticaly on pipeline creation, and will be disposed automatically when no longuer in use.
			descriptorSet = descriptorPool.Allocate (pipeline.Layout.DescriptorSetLayouts[0]);

			DescriptorSetWrites uboUpdate = new DescriptorSetWrites (descriptorSet, pipeline.Layout.DescriptorSetLayouts[0]);
			uboUpdate.Write (dev, uboMats.Descriptor);

			uboMats.Map ();

			cmds = cmdPool.AllocateCommandBuffer (swapChain.ImageCount);
		}

		public override void UpdateView () {
			matrices.projection = Utils.CreatePerspectiveFieldOfView (Utils.DegreesToRadians (45f),
				(float)swapChain.Width / (float)swapChain.Height, 0.1f, 256.0f);
			matrices.view = 
				Matrix4x4.CreateFromAxisAngle (Vector3.UnitZ, rotZ) *
				Matrix4x4.CreateFromAxisAngle (Vector3.UnitY, rotY) *
				Matrix4x4.CreateFromAxisAngle (Vector3.UnitX, rotX) *
				Matrix4x4.CreateTranslation (0, 0, -3f * zoom);
			matrices.model = Matrix4x4.Identity;
			uboMats.Update (matrices, (uint)Marshal.SizeOf<Matrices> ());
			updateViewRequested = false;
		}

		protected override void onMouseMove (double xPos, double yPos) {
			double diffX = lastMouseX - xPos;
			double diffY = lastMouseY - yPos;
			if (MouseButton [0]) {
				rotY -= rotSpeed * (float)diffX;
				rotX += rotSpeed * (float)diffY;
			} else if (MouseButton [1]) {
				zoom += zoomSpeed * (float)diffY;
			} else
				return;
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
				cmds[i].BindIndexBuffer (ibo, VkIndexType.Uint16);
				cmds[i].DrawIndexed ((uint)indices.Length);

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

		protected override void Dispose (bool disposing) {		
			dev.WaitIdle ();
			if (disposing) {
				if (!isDisposed) {
					pipeline.Dispose ();

					frameBuffers?.Dispose();
					descriptorPool.Dispose ();
					vbo.Dispose ();
					ibo.Dispose ();
					uboMats.Dispose ();
				}
			}

			base.Dispose (disposing);
		}
	}
}
