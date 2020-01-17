// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using Vulkan;
using static Vulkan.Vk;

namespace vke {
	/// <summary>
	/// Graphic pipeline config is a helper class used to construct configurations to create pipelines.
	/// This class has some facilities for chaining multiple pipelines creations that have small differencies
	/// in their configurations.
	/// </summary>
	public class GraphicPipelineConfig {
		public uint SubpassIndex;
		/// <summary>
		/// Pipeline layout. Note that layout will not be activated (handle creation) until
		/// the creation of a new pipeline with this 'GraphicPipelineConfig'.
		/// It is valid to use an already activated layout for a new config.
		/// </summary>
		public PipelineLayout Layout;
		/// <summary>
		/// See note for the 'Layout' field.
		/// </summary>
		public RenderPass RenderPass;
		/// <summary>
		/// VkPipelineCache to use for the pipeline creation.
		/// </summary>
		public PipelineCache Cache;
		/// <summary>
		/// VkPipelineBindPoint.Graphics is set by default,
		/// </summary>
		public VkPipelineBindPoint bindPoint = VkPipelineBindPoint.Graphics;
		public VkPipelineInputAssemblyStateCreateInfo inputAssemblyState = VkPipelineInputAssemblyStateCreateInfo.New ();
		public VkPipelineRasterizationStateCreateInfo rasterizationState = VkPipelineRasterizationStateCreateInfo.New ();
		public List<VkViewport> Viewports = new List<VkViewport> ();
		public List<VkRect2D> Scissors = new List<VkRect2D> ();
		public VkPipelineDepthStencilStateCreateInfo depthStencilState = VkPipelineDepthStencilStateCreateInfo.New ();
		public VkPipelineMultisampleStateCreateInfo multisampleState = VkPipelineMultisampleStateCreateInfo.New ();
		public List<VkPipelineColorBlendAttachmentState> blendAttachments = new List<VkPipelineColorBlendAttachmentState> ();
		public List<VkDynamicState> dynamicStates = new List<VkDynamicState> ();
		public List<VkVertexInputBindingDescription> vertexBindings = new List<VkVertexInputBindingDescription> ();
		public List<VkVertexInputAttributeDescription> vertexAttributes = new List<VkVertexInputAttributeDescription> ();
		public List<ShaderInfo> shaders = new List<ShaderInfo> ();
		public VkBool32 ColorBlendLogicOpEnable = false;
		public VkLogicOp ColorBlendLogicOp;
		public Vector4 ColorBlendConstants;

		public VkSampleCountFlags Samples {
			get { return multisampleState.rasterizationSamples; }
		}
		/// <summary>
		/// Default constructor. Prefer the static 'CreateDefault' method to start with
		/// a classic default configuration for rendering.
		/// </summary>
		public GraphicPipelineConfig () {

		}

		/// <summary>
		/// Create a default pipeline configuration with viewport and scissor as dynamic states. One blend attachment is
		/// added automatically with blending disabled. (cfg.blendAttachments[0])
		/// If width and height parameter are omitted viewport and scissor dynamic states are automatically added, else
		/// a viewport and a vkrect2d are added to the viewport and scissor lists.
		/// </summary>
		public static GraphicPipelineConfig CreateDefault (VkPrimitiveTopology topology = VkPrimitiveTopology.TriangleList,
			VkSampleCountFlags samples = VkSampleCountFlags.SampleCount1, bool depthTestEnabled = true, int width = -1, int height = -1) {
			GraphicPipelineConfig cfg = new GraphicPipelineConfig ();

			cfg.inputAssemblyState.topology = topology;
			cfg.multisampleState.rasterizationSamples = samples;

			cfg.rasterizationState.polygonMode = VkPolygonMode.Fill;
			cfg.rasterizationState.cullMode = (uint)VkCullModeFlags.None;
			cfg.rasterizationState.frontFace = VkFrontFace.CounterClockwise;
			cfg.rasterizationState.depthClampEnable = False;
			cfg.rasterizationState.rasterizerDiscardEnable = False;
			cfg.rasterizationState.depthBiasEnable = False;
			cfg.rasterizationState.lineWidth = 1.0f;

			cfg.blendAttachments.Add (new VkPipelineColorBlendAttachmentState (false));

			if (width < 0) {
				cfg.dynamicStates.Add (VkDynamicState.Viewport);
				cfg.dynamicStates.Add (VkDynamicState.Scissor);
			} else {
				cfg.Viewports.Add (new VkViewport { height = height, width = width, minDepth = 0f, maxDepth = 1f });
				cfg.Scissors.Add (new VkRect2D ((uint)width, (uint)height));
			}

			if (depthTestEnabled) {
				cfg.depthStencilState.depthTestEnable = True;
				cfg.depthStencilState.depthWriteEnable = True;
				cfg.depthStencilState.depthCompareOp = VkCompareOp.LessOrEqual;
				cfg.depthStencilState.depthBoundsTestEnable = False;
				cfg.depthStencilState.back.failOp = VkStencilOp.Keep;
				cfg.depthStencilState.back.passOp = VkStencilOp.Keep;
				cfg.depthStencilState.back.compareOp = VkCompareOp.Always;
				cfg.depthStencilState.stencilTestEnable = False;
				cfg.depthStencilState.front = cfg.depthStencilState.back;
			}

			return cfg;
		}

		uint currentAttributeIndex = 0;
		public void AddVertexAttributes (uint binding, params VkFormat[] attribsDesc) {
			uint currentAttributeoffset = 0;
			for (uint i = 0; i < attribsDesc.Length; i++) {
				vertexAttributes.Add (new VkVertexInputAttributeDescription (binding, i + currentAttributeIndex, attribsDesc[i], currentAttributeoffset));
				VkFormatSize fs;
				Utils.vkGetFormatSize (attribsDesc[i], out fs);
				currentAttributeoffset += fs.blockSizeInBits / 8;
			}
			currentAttributeIndex += (uint)attribsDesc.Length;
		}
		public void AddVertexBinding (uint binding, uint stride, VkVertexInputRate inputRate = VkVertexInputRate.Vertex) {
			vertexBindings.Add (new VkVertexInputBindingDescription (binding, stride, inputRate));
		}
		public void AddVertexBinding<T> (uint binding = 0, VkVertexInputRate inputRate = VkVertexInputRate.Vertex) {
			vertexBindings.Add (new VkVertexInputBindingDescription (binding, (uint)Marshal.SizeOf<T> (), inputRate));
		}
		/// <summary>
		/// Automatically configure Attribute for that binding.
		/// </summary>
		public void AddVertex<T> (uint binding = 0, VkVertexInputRate inputRate = VkVertexInputRate.Vertex) {
			vertexBindings.Add (new VkVertexInputBindingDescription (binding, (uint)Marshal.SizeOf<T> (), inputRate));
			FieldInfo[] fields = typeof (T).GetFields ();
			VkFormat[] attribs = new VkFormat[fields.Length];
			for (int i = 0; i < fields.Length; i++)
				attribs[i] = fields[i].GetCustomAttribute<VertexAttributeAttribute> ().Format;
			AddVertexAttributes (binding, attribs);
		}
		public void AddShader (VkShaderStageFlags _stageFlags, string _spirvPath, SpecializationInfo specializationInfo = null, string _entryPoint = "main") {
			shaders.Add (new ShaderInfo (_stageFlags, _spirvPath, specializationInfo, _entryPoint));
		}
		/// <summary>
		/// Resets shaders and vertices in current configuration to ease reuse of
		/// current 'GraphicPipelineConfig' for creating another pipeline.
		/// </summary>
		public void ResetShadersAndVerticesInfos () {
			currentAttributeIndex = 0;
			vertexBindings.Clear ();
			vertexAttributes.Clear ();
			ResetShaders ();
		}
		/// <summary>
		/// Resets shaders in current config to ease reause of current 'GraphicPipelineConfig
		/// for creating another similar pipeline with different shaders.
		/// </summary>
		public void ResetShaders () {
			foreach (ShaderInfo shader in shaders)
				shader.Dispose ();
			shaders.Clear ();
		}
	}
}
