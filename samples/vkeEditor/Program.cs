// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using Glfw;
using vke;
using Vulkan;

namespace vkeEditor {
	public class Program : VkWindow, Crow.IValueChange {
		#region IValueChange implementation
		public event EventHandler<Crow.ValueChangeEventArgs> ValueChanged;
		public virtual void NotifyValueChanged (string MemberName, object _value)
		{
			ValueChanged?.Invoke (this, new Crow.ValueChangeEventArgs (MemberName, _value));
		}
		#endregion

		static void Main (string [] args)
		{
#if DEBUG
			Instance.VALIDATION = true;
			//Instance.RENDER_DOC_CAPTURE = true;
#endif

			using (Program vke = new Program ()) {
				vke.Run ();
			}
		}

		float rotSpeed = 0.01f, zoomSpeed = 0.01f;
		float rotX, rotY, rotZ = 0f, zoom = 1f;

		struct Matrices {
			public Matrix4x4 projection;
			public Matrix4x4 view;
			public Matrix4x4 model;
		}
		struct Vertex {
			Vector3 position;
			Vector3 color;

			public Vertex (float x, float y, float z, float r, float g, float b)
			{
				position = new Vector3 (x, y, z);
				color = new Vector3 (r, g, b);
			}
		}

		public RenderPass RenderPass => pipeline.RenderPass;

		Matrices matrices;

		HostBuffer ibo;
		HostBuffer vbo;
		HostBuffer uboMats;

		public DescriptorPool descriptorPool;
		DescriptorSetLayout dsLayout;
		DescriptorSet descriptorSet;

		FrameBuffers frameBuffers;
		GraphicPipeline pipeline;

		Vertex [] vertices = {
			new Vertex (-1.0f, -1.0f, 0.0f ,  1.0f, 0.0f, 0.0f),
			new Vertex ( 1.0f, -1.0f, 0.0f ,  0.0f, 1.0f, 0.0f),
			new Vertex ( 0.0f,  1.0f, 0.0f ,  0.0f, 0.0f, 1.0f),
		};
		ushort [] indices = new ushort [] { 0, 1, 2 };

		CrowPipeline fsqPl;

		string source;

		public string Source {
			get => Source;
			set {
				if (source == value)
					return;
				source = value;
				NotifyValueChanged ("Source", source);
			}
		}

		GraphicPipelineConfig cfg = GraphicPipelineConfig.CreateDefault (VkPrimitiveTopology.TriangleList, VkSampleCountFlags.SampleCount1, true);

		Program () : base () {}

		protected override void initVulkan () {
			base.initVulkan ();

			cmds = cmdPool.AllocateCommandBuffer (swapChain.ImageCount);

			vbo = new HostBuffer<Vertex> (dev, VkBufferUsageFlags.VertexBuffer, vertices);
			ibo = new HostBuffer<ushort> (dev, VkBufferUsageFlags.IndexBuffer, indices);
			uboMats = new HostBuffer (dev, VkBufferUsageFlags.UniformBuffer, matrices);

			descriptorPool = new DescriptorPool (dev, 1,
				new VkDescriptorPoolSize (VkDescriptorType.UniformBuffer),
				new VkDescriptorPoolSize (VkDescriptorType.CombinedImageSampler));

			dsLayout = new DescriptorSetLayout (dev,
				new VkDescriptorSetLayoutBinding (0, VkShaderStageFlags.Vertex, VkDescriptorType.UniformBuffer),
				new VkDescriptorSetLayoutBinding (1, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler)
			);

			cfg.Layout = new PipelineLayout (dev, dsLayout);
			cfg.RenderPass = new RenderPass (dev, swapChain.ColorFormat, dev.GetSuitableDepthFormat (), cfg.Samples);
			cfg.AddVertexBinding<Vertex> (0);
			cfg.AddVertexAttributes (0, VkFormat.R32g32b32Sfloat, VkFormat.R32g32b32Sfloat);

			using (shaderc.Compiler comp = new shaderc.Compiler ()) {
				cfg.AddShaders (comp.CreateShaderInfo (dev, "shaders/main.vert", shaderc.ShaderKind.VertexShader));
				cfg.AddShaders (comp.CreateShaderInfo (dev, "shaders/main.frag", shaderc.ShaderKind.FragmentShader));
			}

			pipeline = new GraphicPipeline (cfg);

			cfg.DisposeShaders ();


			fsqPl = new CrowPipeline (this, cmdPool.AllocateCommandBuffer (), pipeline.RenderPass, pipeline.Layout, 0);
			//note that descriptor set is allocated after the pipeline creation that use this layout, layout is activated
			//automaticaly on pipeline creation, and will be disposed automatically when no longuer in use.
			descriptorSet = descriptorPool.Allocate (dsLayout);

			DescriptorSetWrites uboUpdate = new DescriptorSetWrites (descriptorSet, dsLayout.Bindings[0]);
			uboUpdate.Write (dev, uboMats.Descriptor);

			uboMats.Map ();

			UpdateFrequency = 20;

		}

		protected override void render () {

			int idx = swapChain.GetNextImage ();
			if (idx < 0) {
				OnResize ();
				return;
			}

			if (cmds[idx] == null)
				return;

			drawFence.Wait ();
			drawFence.Reset ();

			fsqPl.releaseCrowUpdateMutex ();

			presentQueue.Submit (cmds[idx], swapChain.presentComplete, drawComplete[idx], drawFence);
			presentQueue.Present (swapChain, drawComplete[idx]);
		}

		public override void Update () {
			fsqPl.updateCrow (presentQueue, drawFence);
		}
		void buildCommandBuffers () {
			cmdPool.Reset ();
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


				fsqPl.Bind (cmds[i]);
				fsqPl.RecordDraw (cmds[i]);

				fsqPl.RenderPass.End (cmds[i]);


				cmds[i].End ();
			}

			fsqPl.recordUpdateCrowCmd ();
		}
		protected override void OnResize () {
			base.OnResize ();
			UpdateView ();

			fsqPl.initCrowSurface (descriptorSet, dsLayout.Bindings[1]);
			frameBuffers?.Dispose ();
			frameBuffers = pipeline.RenderPass.CreateFrameBuffers (swapChain);

			buildCommandBuffers ();
		}

		public override void UpdateView ()
		{
			matrices.projection = Matrix4x4.CreatePerspectiveFieldOfView (Utils.DegreesToRadians (45f),
				(float)swapChain.Width / (float)swapChain.Height, 0.1f, 256.0f) * Camera.VKProjectionCorrection;
			matrices.view =
				Matrix4x4.CreateFromAxisAngle (Vector3.UnitZ, rotZ) *
				Matrix4x4.CreateFromAxisAngle (Vector3.UnitY, rotY) *
				Matrix4x4.CreateFromAxisAngle (Vector3.UnitX, rotX) *
				Matrix4x4.CreateTranslation (0, 0, -3f * zoom);
			matrices.model = Matrix4x4.Identity;
			uboMats.Update (matrices, (uint)Marshal.SizeOf<Matrices> ());
			updateViewRequested = false;
		}

		protected override void onMouseMove (double xPos, double yPos)
		{
			if (fsqPl.OnMouseMove (xPos, yPos))
				return;

			double diffX = lastMouseX - xPos;
			double diffY = lastMouseY - yPos;
			if (GetButton (MouseButton.Left) == InputAction.Press) {
				rotY -= rotSpeed * (float)diffX;
				rotX += rotSpeed * (float)diffY;
			} else if (GetButton (MouseButton.Right) == InputAction.Press) {
				zoom += zoomSpeed * (float)diffY;
			} else
				return;
			updateViewRequested = true;
		}
		protected override void onMouseButtonDown (MouseButton button)
		{
			if (fsqPl.OnMouseButtonDown (button))
				return;
		}
		protected override void onMouseButtonUp (MouseButton button)
		{
			if (fsqPl.OnMouseButtonUp (button))
				return;
		}
		protected override void onKeyUp (Key key, int scanCode, Modifier modifiers) {
			if (fsqPl.OnKeyUp (key))
				return;
			base.onKeyUp (key, scanCode, modifiers);
		}
		protected override void onKeyDown (Key key, int scanCode, Modifier modifiers) {
			/*if (fsqPl.OnKeyDown (key))
				return;*/
			base.onKeyDown (key, scanCode, modifiers);
		}
		protected override void onChar (CodePoint cp) {
			if (fsqPl.OnKeyPress (cp.ToChar ()))
				return;
			base.onChar (cp);
		}


		protected override void Dispose (bool disposing)
		{
			dev.WaitIdle ();
			if (disposing) {
				if (!isDisposed) {
					fsqPl.Dispose ();
					pipeline.Dispose ();

					frameBuffers?.Dispose ();
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
