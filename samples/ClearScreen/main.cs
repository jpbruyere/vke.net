// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using vke;
using Vulkan;

//Most simple example of the `VkWindow` class usage to output something on screen.
namespace ClearScreen {
	class Program : SampleBase {
		//excutable entry point
		static void Main (string[] args) {
			//the base constructor will create the window with GLFW
			using (Program vke = new Program ()) {
				vke.Run ();
			}
		}

		//frame buffer collection to handle one frame buffer per swapchain image.
		FrameBuffers frameBuffers;
		RenderPass renderPass;

		//Vulkan initialization is the first method called by the 'Run' method.
		//Default initialization will provide a vulkan window, a default swapchain
		//bound to it, and a draw and present semaphore to sync the rendering.
		protected override void initVulkan () {
			base.initVulkan ();
			//there are several method to clear the screen with vulkan. One is to
			//use the renderpass CLEAR load operation so that attachment layout transitioning
			//is handled automatically by the render pass.
			renderPass = new RenderPass (dev, swapChain.ColorFormat);
			//default clear values are automatically added for each attacments
			renderPass.ClearValues[0] = new VkClearValue (0.1f, 0.2f, 1);
			//bound to a pipeline, renderpasses are automatically activated, here we use
			//a stand alone renderpass just to clear the screen, so we have to
			//activate it manually
			renderPass.Activate ();

			//allocate default cmd buffers of the VkWindow class.
			cmds = cmdPool.AllocateCommandBuffer (swapChain.ImageCount);
		}

		void buildCommandBuffers() {
			cmdPool.Reset ();

			for (int i = 0; i < swapChain.ImageCount; ++i) {
				FrameBuffer fb = frameBuffers[i];
				cmds[i].Start ();

				renderPass.Begin (cmds[i], fb);
				renderPass.End (cmds[i]);

				cmds[i].End ();
			}
		}

		//The resize method is called at least once before any rendering, so it's
		//a safe place to initialize output size related vulkan objects like the
		//frame buffers.
		protected override void OnResize () {
			base.OnResize ();

			frameBuffers?.Dispose();
			frameBuffers = renderPass.CreateFrameBuffers(swapChain);

			buildCommandBuffers ();
		}
		//clean up
		protected override void Dispose (bool disposing) {
			dev.WaitIdle ();

			renderPass.Dispose ();
			frameBuffers?.Dispose();

			base.Dispose (disposing);
		}
	}
}
