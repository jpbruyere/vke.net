// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using vke;
using Vulkan;
using Glfw;
using System.Collections.Generic;

//the traditional triangle sample
namespace Multithreading {
	[StructLayout(LayoutKind.Sequential)]
	struct Vertex {
		Vector3 position;
		Vector3 color;

		public Vertex (float x, float y, float z, float r, float g, float b) {
			position = new Vector3 (x, y, z);
			color = new Vector3 (r, g, b);
		}
	}

	class threadedCommands : IDisposable {
		public static VkFormat imgFormat = VkFormat.R8g8b8a8Unorm;
		Program pgm;
		Device dev;
		HostBuffer vbo;     //a host mappable buffer to hold vertices.
		DescriptorPool descriptorPool;
		DescriptorSet descriptorSet;//descriptor set for the mvp matrice.
		List<Vertex> vertices = new List<Vertex>();
		vke.Image img;
		FrameBuffer frameBuffer;
		CommandPool cmdPool;

		public threadedCommands(Device dev, Program pgm) {
			this.dev = dev;
			this.pgm = pgm;
			vbo = new HostBuffer<Vertex> (dev, VkBufferUsageFlags.VertexBuffer, 2048);
			img = new vke.Image (dev, imgFormat, VkImageUsageFlags.ColorAttachment, VkMemoryPropertyFlags.DeviceLocal, pgm.Width, pgm.Height);
			img.CreateView();
			frameBuffer = new FrameBuffer(pgm.shapePipeline.RenderPass, pgm.Width, pgm.Height, img);
		}

		public void Dispose()
		{
			vbo.Dispose();
			img.Dispose();
		}

		PrimaryCommandBuffer buildCommandBuffer() {
			PrimaryCommandBuffer cmd = cmdPool.AllocateAndStart(VkCommandBufferUsageFlags.OneTimeSubmit);

			pgm.shapePipeline.RenderPass.Begin (cmd, frameBuffer);

			cmd.SetViewport (pgm.Width, pgm.Height);
			cmd.SetScissor (pgm.Width, pgm.Height);

			cmd.BindDescriptorSet (pgm.shapePipeline.Layout, descriptorSet);

			cmd.BindPipeline (pgm.shapePipeline);

			cmd.BindVertexBuffer (vbo);
			cmd.Draw ((uint)vertices.Count);

			pgm.shapePipeline.RenderPass.End (cmd);

			cmd.End ();
			return cmd;
		}
	}
}
