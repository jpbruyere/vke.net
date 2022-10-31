// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using vke;
using Vulkan;
using Glfw;
using System.Linq;
using System.Collections.Generic;

namespace MeshShader {
	class Program : SampleBase {
#if DEBUG
		/*public override string[] EnabledLayers  =>
			new string[] {
				"VK_LAYER_KHRONOS_validation"
			};*/

#endif
		public override string[] EnabledInstanceExtensions => new string[] {
			Ext.I.VK_KHR_get_physical_device_properties2,
			Ext.I.VK_EXT_debug_utils,
		};
		public override string[] EnabledDeviceExtensions => new string[] {
			Ext.D.VK_KHR_swapchain,
			Ext.D.VK_KHR_spirv_1_4,
			Ext.D.VK_EXT_mesh_shader
			//"VK_NV_mesh_shader"
		};
		protected override void configureEnabledFeatures(VkPhysicalDeviceFeatures available_features, ref VkPhysicalDeviceFeatures enabled_features)
		{
			base.configureEnabledFeatures(available_features, ref enabled_features);
			
			
		}
		vke.DebugUtils.Messenger dbgmsg;
		protected override void selectPhysicalDevice () {
			PhysicalDeviceCollection phys = instance.GetAvailablePhysicalDevice ();
			phy = instance.GetAvailablePhysicalDevice ().FirstOrDefault (p => p.Properties.deviceType == VkPhysicalDeviceType.DiscreteGpu && p.HasSwapChainSupport);
			Console.WriteLine($"Using gpu: {phy.Properties.deviceName}");
			
			dbgmsg = new vke.DebugUtils.Messenger (instance,
				VkDebugUtilsMessageTypeFlagsEXT.PerformanceEXT | VkDebugUtilsMessageTypeFlagsEXT.ValidationEXT | VkDebugUtilsMessageTypeFlagsEXT.GeneralEXT,
				VkDebugUtilsMessageSeverityFlagsEXT.InfoEXT |
				VkDebugUtilsMessageSeverityFlagsEXT.WarningEXT |
				VkDebugUtilsMessageSeverityFlagsEXT.ErrorEXT |
				VkDebugUtilsMessageSeverityFlagsEXT.VerboseEXT);

			VkPhysicalDeviceFeatures2 phyFeat2 = VkPhysicalDeviceFeatures2.New;
			VkPhysicalDeviceMeshShaderFeaturesEXT meshFeat = VkPhysicalDeviceMeshShaderFeaturesEXT.New;
			
			IntPtr pPhyFeat2 = Marshal.AllocHGlobal(Marshal.SizeOf<VkPhysicalDeviceFeatures2>());
			IntPtr pMeshFeat = Marshal.AllocHGlobal(Marshal.SizeOf<VkPhysicalDeviceMeshShaderFeaturesEXT>());
			
			Marshal.StructureToPtr<VkPhysicalDeviceMeshShaderFeaturesEXT>(meshFeat, pMeshFeat,false);
			phyFeat2.pNext = pMeshFeat;
			Marshal.StructureToPtr<VkPhysicalDeviceFeatures2>(phyFeat2, pPhyFeat2,false);

			Vk.vkGetPhysicalDeviceFeatures2(phy.Handle, pPhyFeat2);

			phyFeat2 = Marshal.PtrToStructure<VkPhysicalDeviceFeatures2>(pPhyFeat2);
			meshFeat = Marshal.PtrToStructure<VkPhysicalDeviceMeshShaderFeaturesEXT>(pMeshFeat);
			Marshal.FreeHGlobal(pPhyFeat2);
			Marshal.FreeHGlobal(pMeshFeat);
			
			Console.WriteLine($"Mesh Shader Support:\t{meshFeat.meshShader}");
			Console.WriteLine($"Task Shader Support:\t{meshFeat.taskShader}");

			if (!(meshFeat.meshShader && meshFeat.taskShader)) {
				phy = null;				
			}
		}		
		
		static void Main (string[] args) {
			Instance.VK_MINOR = 3;
			using (Program vke = new Program ()) {
				vke.Run ();
			}
		}

		const float rotSpeed = 0.01f, zoomSpeed = 0.01f;
		float rotX, rotY, zoom = 1f;

		HostBuffer<Matrix4x4> uboMVPmatrix; //a host mappable buffer for mvp matrice.

		DescriptorPool descriptorPool;
		DescriptorSet descriptorSet;//descriptor set for the mvp matrice.

		FrameBuffers frameBuffers;	//the frame buffer collection coupled to the swapchain images
		GraphicPipeline pipeline;

		protected override void initVulkan () {
			base.initVulkan ();

			
			descriptorPool = new DescriptorPool (dev, 1, new VkDescriptorPoolSize (VkDescriptorType.UniformBuffer));
			using (GraphicPipelineConfig cfg = GraphicPipelineConfig.CreateDefault (VkPrimitiveTopology.TriangleList, VkSampleCountFlags.SampleCount1, false)) {
				//Create the pipeline layout, it will be automatically activated on pipeline creation, so that sharing layout among different pipelines will benefit
				//from the reference counting to automatically dispose unused layout on pipeline clean up. It's the same for DescriptorSetLayout.
				/*cfg.Layout = new PipelineLayout (dev,
					new DescriptorSetLayout (dev, new VkDescriptorSetLayoutBinding (0, VkShaderStageFlags.Vertex, VkDescriptorType.UniformBuffer)));*/
				cfg.Layout = new PipelineLayout (dev);
				//create a default renderpass with just a color attachment for the swapchain image, a default subpass is automatically created and the renderpass activation
				//will follow the pipeline life cicle and will be automatically disposed when no longuer used.
				cfg.RenderPass = new RenderPass (dev, swapChain.ColorFormat, cfg.Samples);
				//configuration of vertex bindings and attributes

				//shader are automatically compiled by SpirVTasks if added to the project. The resulting shaders are automatically embedded in the assembly.
				//To specifiy that the shader path is a resource name, put the '#' prefix. Else the path will be search on disk.
				cfg.AddShaders (
					new ShaderInfo (dev, VkShaderStageFlags.MeshEXT, "#shaders.main.mesh.spv"),
					new ShaderInfo (dev, VkShaderStageFlags.Fragment, "#shaders.main.frag.spv")
				);

				//create and activate the pipeline with the configuration we've just done.
				pipeline = new GraphicPipeline (cfg);
			}

			//because descriptor layout used for a pipeline are only activated on pipeline activation, descriptor set must not be allocated before, except if the layout has been manually activated,
			//but in this case, layout will need also to be explicitly disposed.
			//descriptorSet = descriptorPool.Allocate (pipeline.Layout.DescriptorSetLayouts[0]);

			//Write the content of the descriptor, the mvp matrice.
			//DescriptorSetWrites uboUpdate = new DescriptorSetWrites (descriptorSet, pipeline.Layout.DescriptorSetLayouts[0]);
			//Descriptor property of the mvp buffer will return a default descriptor with no offset of the full size of the buffer.
			//uboUpdate.Write (dev, uboMVPmatrix.Descriptor);

			//allocate the default VkWindow buffers, one per swapchain image. Their will be only reset when rebuilding and not reallocated.
			cmds = cmdPool.AllocateCommandBuffer (swapChain.ImageCount);
		}

		//view update override, see base method for more informations.
		public override void UpdateView () {
			/*uboMVPmatrix.AsSpan()[0] =
				Matrix4x4.CreateFromAxisAngle (Vector3.UnitY, rotY) *
				Matrix4x4.CreateFromAxisAngle (Vector3.UnitX, rotX) *
				Matrix4x4.CreateTranslation (0, 0, -3f * zoom) *
				Helpers.CreatePerspectiveFieldOfView (Helpers.DegreesToRadians (45f), (float)swapChain.Width / (float)swapChain.Height, 0.1f, 256.0f);

			base.UpdateView ();*/
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

				//cmds[i].BindDescriptorSet (pipeline.Layout, descriptorSet);

				cmds[i].BindPipeline (pipeline);

				Vk.vkCmdDrawMeshTasksEXT(cmds[i].Handle, 1, 1, 1);

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
					//uboMVPmatrix.Dispose ();
					dbgmsg?.Dispose ();
				}
			}

			base.Dispose (disposing);
		}
	}
}
