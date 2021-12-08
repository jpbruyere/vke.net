// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using Vulkan;
using static Vulkan.Vk;
using static Vulkan.Utils;

namespace vke {
	public class TimestampQueryPool : QueryPool {
		public readonly float Period;

		#region CTORS
		public TimestampQueryPool (Device device, uint count = 2)
		: base (device, VkQueryType.Timestamp, 0, count)
		{
			Period = Dev.phy.Limits.timestampPeriod;

			resultLength = 1;

			Activate ();
		}
		#endregion

		public void Write (PrimaryCommandBuffer cmd, uint query, VkPipelineStageFlags stageFlags = VkPipelineStageFlags.BottomOfPipe) {
			vkCmdWriteTimestamp (cmd.Handle, stageFlags, handle, query);
		}
		public void Start (PrimaryCommandBuffer cmd, VkPipelineStageFlags stageFlags = VkPipelineStageFlags.BottomOfPipe) {
			vkCmdWriteTimestamp (cmd.Handle, stageFlags, handle, 0);
		}
		public void End (PrimaryCommandBuffer cmd, VkPipelineStageFlags stageFlags = VkPipelineStageFlags.BottomOfPipe) {
			vkCmdWriteTimestamp (cmd.Handle, stageFlags, handle, 1);
		}
		public float ElapsedMiliseconds {
			get {
				ulong[] res = GetResults ();
				return (res[1] - res[0]) * Period / 1000000f;
			}
		}
	}
	public class PipelineStatisticsQueryPool : QueryPool {

		public readonly VkQueryPipelineStatisticFlags[] RequestedStats;

		#region CTORS
		public PipelineStatisticsQueryPool (Device device, VkQueryPipelineStatisticFlags statisticFlags, uint count = 1)
		: base (device, VkQueryType.PipelineStatistics, statisticFlags, count)
		{
			List<VkQueryPipelineStatisticFlags> requests = new List<VkQueryPipelineStatisticFlags> ();

			if (statisticFlags.HasFlag (VkQueryPipelineStatisticFlags.InputAssemblyVertices))
				requests.Add (VkQueryPipelineStatisticFlags.InputAssemblyVertices);
			if (statisticFlags.HasFlag (VkQueryPipelineStatisticFlags.InputAssemblyPrimitives))
				requests.Add (VkQueryPipelineStatisticFlags.InputAssemblyPrimitives);
			if (statisticFlags.HasFlag (VkQueryPipelineStatisticFlags.VertexShaderInvocations))
				requests.Add (VkQueryPipelineStatisticFlags.VertexShaderInvocations);
			if (statisticFlags.HasFlag (VkQueryPipelineStatisticFlags.GeometryShaderInvocations))
				requests.Add (VkQueryPipelineStatisticFlags.GeometryShaderInvocations);
			if (statisticFlags.HasFlag (VkQueryPipelineStatisticFlags.GeometryShaderPrimitives))
				requests.Add (VkQueryPipelineStatisticFlags.GeometryShaderPrimitives);
			if (statisticFlags.HasFlag (VkQueryPipelineStatisticFlags.ClippingInvocations))
				requests.Add (VkQueryPipelineStatisticFlags.ClippingInvocations);
			if (statisticFlags.HasFlag (VkQueryPipelineStatisticFlags.ClippingPrimitives))
				requests.Add (VkQueryPipelineStatisticFlags.ClippingPrimitives);
			if (statisticFlags.HasFlag (VkQueryPipelineStatisticFlags.FragmentShaderInvocations))
				requests.Add (VkQueryPipelineStatisticFlags.FragmentShaderInvocations);
			if (statisticFlags.HasFlag (VkQueryPipelineStatisticFlags.TessellationControlShaderPatches))
				requests.Add (VkQueryPipelineStatisticFlags.TessellationControlShaderPatches);
			if (statisticFlags.HasFlag (VkQueryPipelineStatisticFlags.TessellationEvaluationShaderInvocations))
				requests.Add (VkQueryPipelineStatisticFlags.TessellationEvaluationShaderInvocations);
			if (statisticFlags.HasFlag (VkQueryPipelineStatisticFlags.ComputeShaderInvocations))
				requests.Add (VkQueryPipelineStatisticFlags.ComputeShaderInvocations);

			RequestedStats = requests.ToArray ();

			resultLength = (uint)requests.Count;

			Activate ();
		}
		#endregion

		public void Begin (PrimaryCommandBuffer cmd, uint query = 0) {
			vkCmdBeginQuery (cmd.Handle, handle, query, VkQueryControlFlags.Precise);
		}
		public void End (PrimaryCommandBuffer cmd, uint query = 0) {
			vkCmdEndQuery (cmd.Handle, handle, query);
		}
	}

	public abstract class QueryPool : Activable {
        protected VkQueryPool handle;
		protected readonly VkQueryPoolCreateInfo createInfos;
		public readonly VkQueryType QueryType;
		protected uint resultLength;

		#region CTORS
		protected QueryPool (Device device, VkQueryType queryType, VkQueryPipelineStatisticFlags statisticFlags, uint count = 1)
		: base(device)
        {
			createInfos = VkQueryPoolCreateInfo.CreateNew (queryType, statisticFlags, count);

			//Activate ();
        }

		#endregion

		protected override VkDebugUtilsObjectNameInfoEXT DebugUtilsInfo
					=> new VkDebugUtilsObjectNameInfoEXT (VkObjectType.QueryPool, handle.Handle);

		public override void Activate () {
			if (state != ActivableState.Activated) {
				VkQueryPoolCreateInfo infos = createInfos;
	            CheckResult (vkCreateQueryPool (Dev.Handle, ref infos, IntPtr.Zero, out handle));
			}
			base.Activate ();
		}

		public ulong[] GetResults () {
			ulong[] results = new ulong[resultLength * createInfos.queryCount];
			IntPtr ptr = results.Pin ();
			vkGetQueryPoolResults (Dev.Handle, handle, 0, createInfos.queryCount, (UIntPtr)(resultLength * createInfos.queryCount* sizeof (ulong)), ptr, sizeof (ulong), VkQueryResultFlags.QueryResult64);
			results.Unpin ();
			return results;
		}


		public override string ToString () {
			return string.Format ($"{base.ToString ()}[0x{handle.Handle.ToString("x")}]");
		}

		#region IDisposable Support
		protected override void Dispose (bool disposing) {
			if (!disposing)
				System.Diagnostics.Debug.WriteLine ("VKE QueryPool disposed by finalizer");
			if (state == ActivableState.Activated)
				vkDestroyQueryPool (Dev.Handle, handle, IntPtr.Zero);
			base.Dispose (disposing);
		}
		#endregion

	}
}
