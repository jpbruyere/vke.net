// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Runtime.InteropServices;
using Vulkan;

using static Vulkan.Vk;
using static Vulkan.Utils;

namespace vke {
	public class SecondaryCommandBuffer : CommandBuffer {
		internal SecondaryCommandBuffer (VkDevice _dev, CommandPool _pool, VkCommandBuffer _buff) : base (_dev, _pool, _buff) {
		}
		public void Start (VkCommandBufferUsageFlags usage = 0, RenderPass rp = null, uint subpass = 0, FrameBuffer fb = null,
			bool occlusionQueryEnable = false, VkQueryControlFlags queryFlags = 0, VkQueryPipelineStatisticFlags statFlags = 0) {
			VkCommandBufferInheritanceInfo inheri = default;
			inheri.renderPass = rp == null ? 0 : rp.handle;
			inheri.subpass = subpass;
			inheri.framebuffer = fb == null ? 0 : fb.handle;
			inheri.occlusionQueryEnable = occlusionQueryEnable;
			inheri.queryFlags = queryFlags;
			inheri.pipelineStatistics = statFlags;
			VkCommandBufferBeginInfo cmdBufInfo = new VkCommandBufferBeginInfo (usage);
			cmdBufInfo.pInheritanceInfo = inheri;
			CheckResult (vkBeginCommandBuffer (handle, ref cmdBufInfo));
			cmdBufInfo.Dispose();
		}
	}
	public class PrimaryCommandBuffer : CommandBuffer {
		internal PrimaryCommandBuffer (VkDevice _dev, CommandPool _pool, VkCommandBuffer _buff) : base (_dev, _pool, _buff) {
		}
		/// <summary>
		/// Submit an executable command buffer with optional wait and signal semaphores, and an optional fence to be signaled when the commands have been completed.
		/// </summary>
		/// <param name="queue">Queue.</param>
		/// <param name="wait">Wait.</param>
		/// <param name="signal">Signal.</param>
		/// <param name="fence">Fence.</param>
		public void Submit (VkQueue queue, VkSemaphore wait = default, VkSemaphore signal = default, Fence fence = null) {
			VkSubmitInfo submit_info = default;
			submit_info.pWaitDstStageMask = VkPipelineStageFlags.ColorAttachmentOutput;
			if (signal == VkSemaphore.Null)
				submit_info.pSignalSemaphores = null;
			else
				submit_info.pSignalSemaphores = signal;

			if (wait == VkSemaphore.Null)
				submit_info.pWaitSemaphores = null;
			else
				submit_info.pWaitSemaphores = wait;
			submit_info.pCommandBuffers = handle;

			CheckResult (vkQueueSubmit (queue, 1, submit_info, fence));
			submit_info.Dispose();
		}
		/// <summary>
		/// Put the command buffer in the recording state.
		/// </summary>
		/// <param name="usage">optional command buffer usage flags.</param>
		public void Start (VkCommandBufferUsageFlags usage = 0) {
			VkCommandBufferBeginInfo cmdBufInfo = new VkCommandBufferBeginInfo (usage);
			CheckResult (vkBeginCommandBuffer (handle, ref cmdBufInfo));
		}
		/// <summary>
		/// Execute secondary command buffers.
		/// </summary>
		public void Execute (params SecondaryCommandBuffer[] secondaryCmds) {
			if (secondaryCmds.Length == 1) {
				VkCommandBuffer hnd = secondaryCmds[0].Handle;
				vkCmdExecuteCommands (handle, 1, ref hnd);
				return;
			}
			int sizeElt = Marshal.SizeOf<IntPtr> ();
			IntPtr cmdsPtr = Marshal.AllocHGlobal (secondaryCmds.Length * sizeElt);
			int count = 0;
			for (int i = 0; i < secondaryCmds.Length; i++) {
				if (secondaryCmds[i] == null)
					continue;
				Marshal.WriteIntPtr (cmdsPtr + count * sizeElt, secondaryCmds[i].Handle.Handle);
				count++;
			}
			if (count > 0)
				vkCmdExecuteCommands (handle, (uint)count, cmdsPtr);

			Marshal.FreeHGlobal (cmdsPtr);

		}

	}
	/// <summary>
	/// Command buffers are objects used to record commands which can be subsequently submitted to a device queue for execution.
	/// There are two levels of command buffers 
	/// - primary command buffers, which can execute secondary command buffers, and which are submitted to queues
	/// - secondary command buffers, which can be executed by primary command buffers, and which are not directly submitted to queues.
	/// Command buffer are not derived from activable, because their state is retained by the pool which create them.
	/// </summary>
	public abstract class CommandBuffer {
		public enum States { Init, Record, Executable, Pending, Invalid };

        protected CommandPool pool;
        protected VkCommandBuffer handle;

        public VkCommandBuffer Handle => handle;
		public Device Device => pool?.Dev;//this help
		//public States State { get; internal set; }

        internal CommandBuffer (VkDevice _dev, CommandPool _pool, VkCommandBuffer _buff)
        {
            pool = _pool;
            handle = _buff;

			//State = States.Init;
        }

		/// <summary>
		/// Put the command buffer in the executable state if no errors are present in the recording.
		/// </summary>
		public void End () {
            CheckResult (vkEndCommandBuffer (handle));
        }
        /// <summary>
        /// Update dynamic viewport state
        /// </summary>
        public void SetViewport (float width, float height, float x = 0f, float y = 0f, float minDepth = 0.0f, float maxDepth = 1.0f) {
            VkViewport viewport = new VkViewport {
				x = x,
				y = y,
                height = height,
                width = width,
                minDepth = minDepth,
                maxDepth = maxDepth,
            };
            vkCmdSetViewport (handle, 0, 1, ref viewport);
        }
        /// <summary>
        /// Update dynamic scissor state
        /// </summary>
        public void SetScissor (uint width, uint height, int offsetX = 0, int offsetY = 0) {
            VkRect2D scissor = new VkRect2D (offsetX, offsetY, width, height);
            vkCmdSetScissor (handle, 0, 1, ref scissor);
        }
		//TODO:update generator to handle float array in this command
		public void SetBlendConstants (float r, float g, float b, float a)
		{
			throw new NotImplementedException();
			//vkCmdSetBlendConstants(handle, );
		}
		public void BindPipeline (Pipeline pipeline, VkPipelineBindPoint bindPoint) {
            vkCmdBindPipeline (handle, bindPoint, pipeline.Handle);
        }
		public void Dispatch (uint groupCountX, uint groupCountY = 1, uint groupCountZ = 1) {
			vkCmdDispatch (handle, groupCountX, groupCountY, groupCountZ);
		}
		public void BindPipeline (Pipeline pl) {
			pl.Bind (this);
		}
		/// <summary>
		/// bind pipeline and descriptor set with default pipeline layout
		/// </summary>
		/// <param name="pl">pipeline to bind</param>
		/// <param name="ds">descriptor set</param>
		/// <param name="firstset">first set to bind</param>
		public void BindPipeline (Pipeline pl, DescriptorSet ds, uint firstset = 0) {
			pl.Bind (this);
			pl.BindDescriptorSet (this, ds, firstset);
		}
		public void BindDescriptorSet (PipelineLayout pipelineLayout, DescriptorSet descriptorSet, uint firstSet = 0) {
            vkCmdBindDescriptorSets (handle, VkPipelineBindPoint.Graphics, pipelineLayout.handle, firstSet, 1, ref descriptorSet.handle, 0, IntPtr.Zero);
        }
		public void BindDescriptorSet (VkPipelineBindPoint bindPoint, PipelineLayout pipelineLayout, DescriptorSet descriptorSet, uint firstSet = 0) {
			vkCmdBindDescriptorSets (handle, bindPoint, pipelineLayout.handle, firstSet, 1, ref descriptorSet.handle, 0, IntPtr.Zero);
		}
		public void BindDescriptorSets (VkPipelineBindPoint bindPoint, PipelineLayout pipelineLayout, uint firstSet,params DescriptorSet[] descriptorSets) {
			vkCmdBindDescriptorSets (handle, bindPoint, pipelineLayout.handle, firstSet, (uint)descriptorSets.Length, descriptorSets.Pin (), 0, IntPtr.Zero);
			descriptorSets.Unpin ();
		}
		public void BindVertexBuffer (Buffer vertices, uint binding = 0, ulong offset = 0) {
            vkCmdBindVertexBuffers (handle, binding, 1, ref vertices.handle, ref offset);
        }
        public void BindIndexBuffer (Buffer indices, VkIndexType indexType = VkIndexType.Uint32, ulong offset = 0) {
            vkCmdBindIndexBuffer (handle, indices.handle, offset, indexType);
        }
        public void DrawIndexed (uint indexCount, uint instanceCount = 1, uint firstIndex = 0, int vertexOffset = 0, uint firstInstance = 0) {
            vkCmdDrawIndexed (Handle, indexCount, instanceCount, firstIndex, vertexOffset, firstInstance);
        }
        public void Draw (uint vertexCount, uint instanceCount = 1, uint firstVertex = 0, uint firstInstance = 0) {
            vkCmdDraw (Handle, vertexCount, instanceCount, firstVertex, firstInstance);
        }
		public void PushConstant (PipelineLayout pipelineLayout, VkShaderStageFlags stageFlags, Object data, uint offset = 0) {
			vkCmdPushConstants (handle, pipelineLayout.handle, stageFlags, offset, (uint)Marshal.SizeOf (data), data.Pin ());
			data.Unpin ();
		}
		public void PushConstant (Pipeline pipeline, object obj, int rangeIndex = 0, uint offset = 0) {
			PushConstant (pipeline.Layout, pipeline.Layout.PushConstantRanges[rangeIndex].stageFlags, obj, offset);
		}
		public void PushConstant (PipelineLayout pipelineLayout, VkShaderStageFlags stageFlags, uint size, Object data, uint offset = 0) {
			vkCmdPushConstants (handle, pipelineLayout.handle, stageFlags, offset, size, data.Pin ());
			data.Unpin ();
		}
		public void SetMemoryBarrier (VkPipelineStageFlags srcStageMask, VkPipelineStageFlags dstStageMask,
			VkAccessFlags srcAccessMask, VkAccessFlags dstAccessMask, VkDependencyFlags dependencyFlags = VkDependencyFlags.ByRegion) {
			VkMemoryBarrier memoryBarrier = default;
			memoryBarrier.srcAccessMask = srcAccessMask;
			memoryBarrier.dstAccessMask = dstAccessMask;
			Vk.vkCmdPipelineBarrier (Handle, srcStageMask, dstStageMask,
				dependencyFlags, 1, ref memoryBarrier, 0, IntPtr.Zero, 0, IntPtr.Zero);
		}
		public void BeginRegion (string name, float r = 1f, float g = 0.1f, float b=0.1f, float a = 1f) {
			if (!Device.debugUtilsEnabled)
				return;
			VkDebugMarkerMarkerInfoEXT info = default;
			info.pMarkerName = name.Pin ();
			info.color.X = r;
			info.color.Y = g;
			info.color.Z = b;
			info.color.W = a;
			vkCmdDebugMarkerBeginEXT (Handle, ref info);
			name.Unpin ();
		}
		public void InsertDebugMarker (string name, float r = 1f, float g = 0.1f, float b=0.1f, float a = 1f) {
			if (!Device.debugUtilsEnabled)
				return;
			VkDebugMarkerMarkerInfoEXT info = default;
			info.pMarkerName = name.Pin ();
			info.color.X = r;
			info.color.Y = g;
			info.color.Z = b;
			info.color.W = a;
			vkCmdDebugMarkerInsertEXT (Handle, ref info);
			name.Unpin ();
		}
		public void EndRegion () {
			if (Device.debugUtilsEnabled)
				vkCmdDebugMarkerEndEXT (Handle);
		}

		public void Free () {
            pool.FreeCommandBuffers (this);
        }
    }
}
