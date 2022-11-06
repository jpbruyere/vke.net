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
				//,"VK_LAYER_RENDERDOC_Capture"
			};

#endif
		public override string[] EnabledInstanceExtensions => new string[] {
			Ext.I.VK_EXT_debug_utils,
		};
		public override string[] EnabledDeviceExtensions => new string[] {
			Ext.D.VK_KHR_swapchain
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

		//200 fixed points for repeatability
		public float[] fixedPoints = new float[] {
			1,187, 374,293, 328,53, 145,144, 566,100, 325,270, 486,240, 350,263, 145,195, 512,283, 10,391, 377,336, 408,142, 310,195, 389,152, 429,356, 590,290,
			520,119, 553,197, 506,222, 476,296, 139,261, 382,320, 177,302, 140,174, 168,302, 373,393, 115,9, 503,312, 31,75, 369,301, 426,225, 321,260, 415,33,
			439,114, 441,299, 386,118, 549,111, 448,390, 123,351, 36,304, 152,85, 59,383, 161,0, 321,337, 275,56, 265,262, 563,15, 17,267, 69,393, 65,85, 13,379,
			420,213, 67,130, 40,365, 166,344, 345,69, 77,341, 299,198, 278,231, 230,276, 329,355, 495,237, 183,395, 214,350, 148,31, 516,88, 462,339, 443,122,
			307,226, 507,377, 559,248, 339,239, 339,107, 315,34, 323,212, 122,215, 360,167, 398,52, 73,42, 136,49, 581,76, 399,79, 1,141, 241,112, 69,77, 590,358,
			439,176, 454,371, 194,91, 16,14, 277,108, 201,286, 524,128, 371,140, 110,242, 110,162, 204,289, 584,296, 300, 200, 200, 300
		};
		TimestampQueryPool queryPool;
		ulong accumulatedDurations = 0;
		int frameCount = 0;

		protected override void initVulkan () {
			base.initVulkan ();

			path = new Vector2[maxPointCount];

			queryPool = new TimestampQueryPool(dev,2);
			//queryPool.Reset();
			
			for (int i = 0; i < fixedPoints.Length/2; i++)
			{
				addPoint(fixedPoints[i*2],fixedPoints[i*2+1]);	
			}
			
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

				queryPool.Start(cmd);

				plStroke.Bind (cmd);
				plStroke.BindDescriptorSet (cmd, dsetStroke);
				cmd.PushConstant (plStroke.Layout, VkShaderStageFlags.Compute, lineWidth * 0.5f);
				cmd.Dispatch (pointCount);

				queryPool.End(cmd);

				cmd.End ();
				computeQ.Submit (cmd);
				dev.WaitIdle();
				
				ulong[] results = queryPool.GetResults();
				accumulatedDurations += results[1] - results[0];
				if (++frameCount == 1000) {
					Console.WriteLine($"ms:{accumulatedDurations * phy.Limits.timestampPeriod * 0.00000001f}");
					accumulatedDurations = 0;
					frameCount = 0;
				}

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

					queryPool.Dispose();
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
