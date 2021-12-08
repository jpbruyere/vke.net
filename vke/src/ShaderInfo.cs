// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using Vulkan;

namespace vke {
	/// <summary>
	/// This class is a helper class for VkPipelineShaderStageCreateInfo creation.
	/// </summary>
	public class ShaderInfo : IDisposable {
		protected VkPipelineShaderStageCreateInfo info;
		protected Device dev;

		public VkShaderStageFlags Stage => info.stage;
		public VkPipelineShaderStageCreateInfo Info => info;

		public void RecreateModule(uint[] code, UIntPtr codeSize) {
			if (dev == null)
				throw new Exception ("[ShaderInfo]Trying to recreate unowned shader module.");
			dev.DestroyShaderModule (info.module);
			info.module = dev.CreateShaderModule (code, codeSize);
		}

		/// <summary>
		/// Create a new 'ShaderInfo' object by providing a handle to an native memory holding the compiled SpirV code
		/// and its size in byte.
		/// </summary>
		/// <param name="dev">Dev.</param>
		/// <param name="stageFlags">Stage flags.</param>
		/// <param name="code">a native pointer on the SpirV Code, typically a 'shaderc.Result.CodePointer</param>
		/// <param name="codeSize">Code size in byte</param>
		/// <param name="specializationInfo">Specialization info.</param>
		/// <param name="entryPoint">shader entry point</param>
		public ShaderInfo (Device dev, VkShaderStageFlags stageFlags, uint[] code, UIntPtr codeSize, SpecializationInfo specializationInfo = null, string entryPoint = "main"):
			this(stageFlags, dev.CreateShaderModule (code, codeSize), specializationInfo, entryPoint) {
			this.dev = dev;//keep dev for destroying module created in this CTOR
		}
		/// <summary>
		/// Create a new ShaderInfo object by providing the path to a compiled SpirV shader.
		/// </summary>
		/// <param name="dev">vke Device</param>
		/// <param name="_stageFlags">Stage flags.</param>
		/// <param name="_spirvPath">
		/// Path to a compiled SpirV Shader on disk or as embedded ressource. See <see cref="Helpers.GetStreamFromPath"/> for more information.
		/// </param>
		/// <param name="specializationInfo">Specialization info</param>
		/// <param name="entryPoint">shader entry point, 'main' by default.</param>
		public ShaderInfo (Device dev, VkShaderStageFlags _stageFlags, string _spirvPath, SpecializationInfo specializationInfo = null, string entryPoint = "main"):
			this(_stageFlags, dev.CreateShaderModule (_spirvPath), specializationInfo, entryPoint) {
			this.dev = dev;//keep dev for destroying module created in this CTOR
		}
		/// <summary>
		/// Create a new ShaderInfo object by providing directly a VkShaderModule. Note
		/// that this module will not be own by this ShaderInfo, and so will not be
		/// destroyed on Dispose.
		/// </summary>
		public ShaderInfo (VkShaderStageFlags stageFlags, VkShaderModule module, SpecializationInfo specializationInfo = null, string entryPoint = "main") {
			info.stage = stageFlags;
			info.pName = entryPoint;
			info.module = module;
			if (specializationInfo != null)
				info.pSpecializationInfo =  specializationInfo.infos;
		}

		#region IDisposable Support
		private bool disposedValue = false; // Pour détecter les appels redondants

		protected virtual void Dispose (bool disposing) {
			if (!disposedValue) {
				if (!disposing)
					System.Diagnostics.Debug.WriteLine ("VKE ShaderInfo disposed by finalizer");

				dev?.DestroyShaderModule (info.module);
				info.Dispose();

				disposedValue = true;
			}
		}
		public void Dispose () {
			Dispose (true);
		}
		#endregion
	}
}
