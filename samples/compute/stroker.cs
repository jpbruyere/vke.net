using System;
using Glfw;
using Vulkan;
using vke;
using Image = vke.Image;

using System.Numerics;
using System.Runtime.InteropServices;

namespace delaunay {
	class Program : VkWindow {
#if DEBUG
		public override string[] EnabledLayers  =>
			new string[] {
				"VK_LAYER_KHRONOS_validation"
				,"VK_LAYER_RENDERDOC_Capture"
			};

#endif
		public override string[] EnabledInstanceExtensions => new string[] {
			Ext.I.VK_EXT_debug_utils,
		};
		public override string[] EnabledDeviceExtensions => new string[] {
			Ext.D.VK_KHR_swapchain,
			Ext.D.VK_AMD_shader_info
		};
		Program() : base ("Stroker", 800, 600, false) {

		}
		static void Main (string[] args) {
			using (Program vke = new Program ()) {
				vke.Run ();
			}
		}

		FrameBuffers frameBuffers;
		GraphicPipeline grPipeline;
		ComputePipeline plStroke;
		Queue computeQ, transferQ;

		HostBuffer<Vector2> pathBuff; 
		Vector2[] path;
		GPUBuffer vbo, ibo;
		DescriptorPool dsPool;
		DescriptorSetLayout dslStroke;
		DescriptorSet dsetStroke;


		uint zoom = 2;
		int invocationCount = 8;

		uint pointCount = 0;
		uint maxPointCount = 256;
		int curPoint = - 1, hoverPoint = -1;
		float lineWidth = 10f;

		const int primitiveSize = 2;

		void addPoint(float x, float y) {
			if (pointCount < maxPointCount) {
				path[pointCount++] = new Vector2(x,y);
			}
		}
		void addPoint(Vector2 p) {
			if (pointCount < maxPointCount) {
				path[pointCount++] = p;
			}
		}

		protected override void initVulkan () {
			base.initVulkan ();

			path = new Vector2[maxPointCount];
			
			addPoint(50,50);
			addPoint(100,150);
			addPoint(150,60);
			/*addPoint(200,100);
			addPoint(300,200);*/

			pathBuff = new HostBuffer<Vector2>(dev, VkBufferUsageFlags.StorageBuffer, path, true);

			vbo = new GPUBuffer<Vector2> (dev, VkBufferUsageFlags.StorageBuffer | VkBufferUsageFlags.VertexBuffer, (int)(maxPointCount - 1) * 4);
			ibo = new GPUBuffer<UInt16> (dev, VkBufferUsageFlags.StorageBuffer | VkBufferUsageFlags.IndexBuffer, (int)(maxPointCount - 1) * 6);
			pathBuff.SetName("path");
			vbo.SetName("vbo");
			ibo.SetName("ibo");

			dsPool = new DescriptorPool (dev, 1,
				new VkDescriptorPoolSize (VkDescriptorType.StorageBuffer, 3));
			dslStroke = new DescriptorSetLayout (dev,
				new VkDescriptorSetLayoutBinding (0, VkShaderStageFlags.Compute, VkDescriptorType.StorageBuffer),
				new VkDescriptorSetLayoutBinding (1, VkShaderStageFlags.Compute, VkDescriptorType.StorageBuffer),
				new VkDescriptorSetLayoutBinding (2, VkShaderStageFlags.Compute, VkDescriptorType.StorageBuffer)
			);

			using (GraphicPipelineConfig cfg = GraphicPipelineConfig.CreateDefault (VkPrimitiveTopology.TriangleList, VkSampleCountFlags.SampleCount8, false)) {
				cfg.Layout = new PipelineLayout (dev, new VkPushConstantRange (VkShaderStageFlags.Vertex, (uint)Marshal.SizeOf<Vector2>()));
				cfg.RenderPass = new RenderPass (dev, swapChain.ColorFormat, VkSampleCountFlags.SampleCount8);
				cfg.RenderPass.ClearValues[0] = new VkClearValue { color = new VkClearColorValue (0.0f, 0.0f, 0.1f) };

				cfg.AddVertexBinding<Vector2> (0);
				cfg.AddVertexAttributes (0, VkFormat.R32g32Sfloat);//2d position

				cfg.AddShaders (
					new ShaderInfo (dev, VkShaderStageFlags.Vertex, "#shaders.stroker.vert.spv"),
					new ShaderInfo (dev, VkShaderStageFlags.Fragment, "#shaders.stroker.frag.spv")
				);

				grPipeline = new GraphicPipeline (cfg);
			}

			plStroke = new ComputePipeline (
				new PipelineLayout (dev, new VkPushConstantRange (VkShaderStageFlags.Compute, sizeof (float)), dslStroke),
				"#shaders.stroker.comp.spv");

			dsetStroke = dsPool.Allocate (dslStroke);

			DescriptorSetWrites dsUpdate = new DescriptorSetWrites (dsetStroke, dslStroke);
			dsUpdate.Write (dev, pathBuff.Descriptor, vbo.Descriptor, ibo.Descriptor);

			UpdateFrequency = 1;

			cmds = cmdPool.AllocateCommandBuffer (swapChain.ImageCount);
		}

		protected override void createQueues () {
			computeQ = new Queue (dev, VkQueueFlags.Compute);
			transferQ = new Queue (dev, VkQueueFlags.Transfer);

			base.createQueues ();
		}
		bool rebuildBuffers = false;

		void buildCommandBuffers() {
			cmdPool.Reset (VkCommandPoolResetFlags.ReleaseResources);

			for (int i = 0; i < swapChain.ImageCount; ++i) {
				FrameBuffer fb = frameBuffers[i];
				cmds[i].Start ();

				grPipeline.RenderPass.Begin (cmds[i], fb);

				cmds[i].SetViewport (swapChain.Width, swapChain.Height);
				cmds[i].SetScissor (swapChain.Width, swapChain.Height);

				cmds[i].BindPipeline (grPipeline);
				cmds[i].PushConstant (grPipeline.Layout, VkShaderStageFlags.Vertex, new Vector2(swapChain.Width, swapChain.Height));

				cmds[i].BindVertexBuffer (vbo);
				cmds[i].BindIndexBuffer (ibo, VkIndexType.Uint16);
				cmds[i].DrawIndexed ((uint)(pointCount - 1) * 6);

				grPipeline.RenderPass.End (cmds[i]);

				cmds[i].End ();
			}
		}

		public override void Update () {

			using (CommandPool cmdPoolCompute = new CommandPool (dev, computeQ.qFamIndex)) {

				PrimaryCommandBuffer cmd = cmdPoolCompute.AllocateAndStart (VkCommandBufferUsageFlags.OneTimeSubmit);

				plStroke.Bind (cmd);
				plStroke.BindDescriptorSet (cmd, dsetStroke);
				cmd.PushConstant (plStroke.Layout, VkShaderStageFlags.Compute, lineWidth * 0.5f);
				cmd.Dispatch (pointCount);
				cmd.End ();
				computeQ.Submit (cmd);
				dev.WaitIdle();
			}
			if (rebuildBuffers) { 
				buildCommandBuffers();
				rebuildBuffers = false;
			}
		}
		protected override void OnResize () {
			base.OnResize ();

			dev.WaitIdle();

			frameBuffers?.Dispose();
			frameBuffers = grPipeline.RenderPass.CreateFrameBuffers(swapChain);

			buildCommandBuffers ();
		}
		protected override void Dispose (bool disposing) {
			if (disposing) {
				if (!isDisposed) {
					dev.WaitIdle ();

					for (int i = 0; i < swapChain.ImageCount; ++i)
						frameBuffers[i]?.Dispose ();

					grPipeline.Dispose ();
					plStroke.Dispose ();

					dslStroke.Dispose ();
					dsPool.Dispose ();

					pathBuff.Dispose ();
					vbo.Dispose ();
					ibo.Dispose ();
				}
			}

			base.Dispose (disposing);
		}

		protected override void onKeyDown(Key key, int scanCode, Modifier modifiers)
		{
			switch(key) {
				case Key.F3:
					if (phy.GetDeviceExtensionSupported (Ext.D.VK_AMD_shader_info))
						printAMDStats();
					break;
				case Key.KeypadAdd:
					lineWidth *= 2.0f;
					rebuildBuffers = true;
					break;				
				case Key.KeypadSubtract:
					lineWidth /= 2.0f;
					rebuildBuffers = true;
					break;				
				default:
					base.onKeyDown(key, scanCode, modifiers);
					break;
			}
		}
		Vector2 curMousePos;
		const float pointSizeSquared = 10;
		protected override async void onMouseMove(double xPos, double yPos)
		{
			base.onMouseMove(xPos, yPos);

			curMousePos = new Vector2((float)xPos, (float)yPos);

			if (curPoint < 0) {
				for (int i = 0; i < pointCount; i++)
				{
					if (Vector2.DistanceSquared(curMousePos, path[i]) < pointSizeSquared) {
						hoverPoint = i;
						return;
					}
				}
				hoverPoint = -1;
			} else {
				path[curPoint] = curMousePos;
				pathBuff.Update((uint)curPoint, path[curPoint]);
			}
		}
		protected override void onMouseButtonDown(MouseButton button, Modifier mods)
		{
			if (hoverPoint < 0) {
				addPoint(curMousePos);
				pathBuff.Update(pointCount - 1, path[pointCount - 1]);
				rebuildBuffers = true;
			} else {
				curPoint = hoverPoint;
			}
		}
		protected override void onMouseButtonUp(MouseButton button, Modifier mods)
		{
			curPoint = -1;
		}
		void printAMDStats() {
			VkShaderStatisticsInfoAMD stats = new VkShaderStatisticsInfoAMD();
			
			Vk.vkGetShaderInfoAMD(dev.Handle, plStroke.Handle, VkShaderStageFlags.Compute, VkShaderInfoTypeAMD.StatisticsAMD, 
				out UIntPtr statSize, IntPtr.Zero);

			IntPtr statSize2 = (IntPtr)Marshal.SizeOf<VkShaderStatisticsInfoAMD>();

			Vk.vkGetShaderInfoAMD(dev.Handle, plStroke.Handle, VkShaderStageFlags.Compute, VkShaderInfoTypeAMD.StatisticsAMD, 
				(IntPtr)(statSize.ToUInt64()), stats.Pin());

			stats.Unpin();

			Console.WriteLine ($"AMD Statistics");
			Console.WriteLine ($"==============");
			Console.WriteLine ($"Sgprs: {stats.resourceUsage.numUsedSgprs} / Avail:{stats.numAvailableSgprs} Phy:{stats.numPhysicalSgprs}");
			Console.WriteLine ($"Vgprs: {stats.resourceUsage.numUsedVgprs} / Avail:{stats.numAvailableVgprs} Phy:{stats.numPhysicalVgprs}");
			Console.WriteLine ($"_________________________________________________________________");
		}
	}
}
