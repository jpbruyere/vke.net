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
			Instance.VALIDATION = false;
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

		FSQPipeline fsqPl;

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

		Program () : base ()
		{

		}

		protected override void initVulkan () {
			base.initVulkan ();
			cmds = cmdPool.AllocateCommandBuffer (swapChain.ImageCount);
			updateUI = cmdPool.AllocateCommandBuffer ();

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

			fsqPl = new FSQPipeline (pipeline, 0);

			//note that descriptor set is allocated after the pipeline creation that use this layout, layout is activated
			//automaticaly on pipeline creation, and will be disposed automatically when no longuer in use.
			descriptorSet = descriptorPool.Allocate (dsLayout);

			DescriptorSetWrites uboUpdate = new DescriptorSetWrites (descriptorSet, dsLayout.Bindings[0]);
			uboUpdate.Write (dev, uboMats.Descriptor);

			uboMats.Map ();

			iFace = new Crow.Interface ((int)Width, (int)Height, this.WindowHandle);
			iFace.Init ();
			iFace.Load ("#ui.testImage.crow").DataSource = this;
			UpdateFrequency = 30;

			updateUIFence = new Fence (dev, true);

			Thread ui = new Thread (uiThread);
			ui.IsBackground = true;
			ui.Start ();
		}
		volatile bool running;

		void uiThread () {
			running = true;
			while (running) {
				iFace.Update ();
				Thread.Sleep (10);
			}
		}

		protected override void render () {
		
			int idx = swapChain.GetNextImage ();
			if (idx < 0) {
				OnResize ();
				return;
			}

			if (cmds[idx] == null)
				return;

			//updateUIFence.Wait ();
			drawFence.Wait ();
			drawFence.Reset ();

			if (Monitor.IsEntered (iFace.UpdateMutex))
				Monitor.Exit (iFace.UpdateMutex);

			presentQueue.Submit (cmds[idx], swapChain.presentComplete, drawComplete[idx], drawFence);
			presentQueue.Present (swapChain, drawComplete[idx]);

			//presentQueue.WaitIdle ();
		
		}

		PrimaryCommandBuffer updateUI;
		Fence updateUIFence;

		public override void Update ()
		{
			//presentQueue.WaitIdle ();
			//updateUIFence.Wait ();
			//updateUIFence.Reset ();
			if (iFace.IsDirty) {
				drawFence.Wait ();
				drawFence.Reset ();
				Monitor.Enter (iFace.UpdateMutex);
				presentQueue.Submit (updateUI, default, default, drawFence);
				iFace.IsDirty = false;
			}
			NotifyValueChanged ("fps", fps);
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

			iFace.OnMouseMove ((int)xPos, (int)yPos);

			if (iFace.HoverWidget != null)
				return;

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
		protected override void onMouseButtonDown (MouseButton button)
		{
			iFace.OnMouseButtonDown (button);
		}
		protected override void onMouseButtonUp (MouseButton button)
		{
			iFace.OnMouseButtonUp (button);
		}
		protected override void onKeyUp (Key key, int scanCode, Modifier modifiers) {
			if (!iFace.OnKeyUp (key))
				base.onKeyUp (key, scanCode, modifiers);
		}
		protected override void onKeyDown (Key key, int scanCode, Modifier modifiers) {
			if (!iFace.OnKeyDown (key))
				base.onKeyDown (key, scanCode, modifiers);
		}
		protected override void onChar (CodePoint cp) {
			if (!iFace.OnKeyPress(cp.ToChar()))
				base.onChar (cp);
		}
		void buildCommandBuffers ()
		{
			cmdPool.Reset ();
			for (int i = 0; i < swapChain.ImageCount; ++i) {
				FrameBuffer fb = frameBuffers [i];
				cmds [i].Start ();

					
				pipeline.RenderPass.Begin (cmds [i], fb);

				cmds [i].SetViewport (swapChain.Width, swapChain.Height);
				cmds [i].SetScissor (swapChain.Width, swapChain.Height);

				cmds [i].BindDescriptorSet (pipeline.Layout, descriptorSet);

				cmds [i].BindPipeline (pipeline);

				cmds [i].BindVertexBuffer (vbo);
				cmds [i].BindIndexBuffer (ibo, VkIndexType.Uint16);
				cmds [i].DrawIndexed ((uint)indices.Length);

				fsqPl.Bind (cmds [i]);
				fsqPl.RecordDraw (cmds [i]);

				pipeline.RenderPass.End (cmds [i]);


				cmds[i].End ();
			}

			updateUI.Start ();
			uiImage.SetLayout (updateUI, VkImageAspectFlags.Color,
				VkImageLayout.ShaderReadOnlyOptimal, VkImageLayout.TransferDstOptimal,
				VkPipelineStageFlags.FragmentShader, VkPipelineStageFlags.Transfer);

			uiBuffer.CopyTo (updateUI, uiImage, VkImageLayout.ShaderReadOnlyOptimal);

			uiImage.SetLayout (updateUI, VkImageAspectFlags.Color,
				VkImageLayout.TransferDstOptimal, VkImageLayout.ShaderReadOnlyOptimal,
				VkPipelineStageFlags.Transfer, VkPipelineStageFlags.FragmentShader);
			updateUI.End ();
		}

		protected override void OnResize ()
		{
			base.OnResize ();

			dev.WaitIdle ();

			lock (iFace.UpdateMutex)
				initUISurface ();
			iFace.ProcessResize (new Crow.Rectangle (0,0,(int)Width, (int)Height));

			frameBuffers?.Dispose ();
			frameBuffers = pipeline.RenderPass.CreateFrameBuffers (swapChain);

			buildCommandBuffers ();
		}

		protected override void Dispose (bool disposing)
		{
			dev.WaitIdle ();
			running = false;
			if (disposing) {
				if (!isDisposed) {
					pipeline.Dispose ();
					fsqPl.Dispose ();

					frameBuffers?.Dispose ();
					descriptorPool.Dispose ();
					vbo.Dispose ();
					ibo.Dispose ();
					uboMats.Dispose ();
					uiImage?.Dispose ();
					uiBuffer?.Dispose ();
					updateUIFence.Dispose ();
				}
			}
			iFace.Dispose ();
			base.Dispose (disposing);
		}

		public Image uiImage;
		public HostBuffer uiBuffer;

		Crow.Interface iFace;

		void initUISurface ()
		{
			iFace.surf?.Dispose ();
			uiImage?.Dispose ();
			uiBuffer?.Dispose ();

			uiBuffer = new HostBuffer (dev, VkBufferUsageFlags.TransferSrc | VkBufferUsageFlags.TransferDst, Width * Height * 4,  true);

			uiImage = new Image (dev, VkFormat.B8g8r8a8Unorm, VkImageUsageFlags.Sampled,
				VkMemoryPropertyFlags.DeviceLocal, Width, Height, VkImageType.Image2D, VkSampleCountFlags.SampleCount1, VkImageTiling.Linear);
			uiImage.CreateView (VkImageViewType.ImageView2D, VkImageAspectFlags.Color);
			uiImage.CreateSampler (VkFilter.Nearest, VkFilter.Nearest, VkSamplerMipmapMode.Nearest, VkSamplerAddressMode.ClampToBorder);
			uiImage.Descriptor.imageLayout = VkImageLayout.ShaderReadOnlyOptimal;
			//uiImage.Map ();

			NotifyValueChanged ("uiImage", uiImage);

			DescriptorSetWrites uboUpdate = new DescriptorSetWrites (descriptorSet, dsLayout.Bindings [1]);
			uboUpdate.Write (dev, uiImage.Descriptor);

			iFace.surf = new Crow.Cairo.ImageSurface (uiBuffer.MappedData, Crow.Cairo.Format.ARGB32,
				(int)Width, (int)Height, (int)Width * 4);				

		}
	}
}
