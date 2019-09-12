﻿//
// PipelineConfig.cs
//
// Author:
//       Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// Copyright (c) 2019 jp
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using Vulkan;
using static Vulkan.Vk;

namespace vke {
    public class GraphicPipelineConfig {
		public uint SubpassIndex;
        public PipelineLayout Layout;
		public RenderPass RenderPass;
		public PipelineCache Cache;
		public VkPipelineBindPoint bindPoint = VkPipelineBindPoint.Graphics;
        public VkPipelineInputAssemblyStateCreateInfo inputAssemblyState = VkPipelineInputAssemblyStateCreateInfo.New();
        public VkPipelineRasterizationStateCreateInfo rasterizationState = VkPipelineRasterizationStateCreateInfo.New();
        public VkPipelineViewportStateCreateInfo viewportState = VkPipelineViewportStateCreateInfo.New();
        public VkPipelineDepthStencilStateCreateInfo depthStencilState = VkPipelineDepthStencilStateCreateInfo.New();
        public VkPipelineMultisampleStateCreateInfo multisampleState = VkPipelineMultisampleStateCreateInfo.New();
        public List<VkPipelineColorBlendAttachmentState> blendAttachments = new List<VkPipelineColorBlendAttachmentState>();
        public List<VkDynamicState> dynamicStates = new List<VkDynamicState> ();
        public List<VkVertexInputBindingDescription> vertexBindings = new List<VkVertexInputBindingDescription> ();
        public List<VkVertexInputAttributeDescription> vertexAttributes = new List<VkVertexInputAttributeDescription> ();
        public readonly List<ShaderInfo> shaders = new List<ShaderInfo>();
		public VkBool32 ColorBlendLogicOpEnable = false;
		public VkLogicOp ColorBlendLogicOp;
		public Vector4 ColorBlendConstants;

		public VkSampleCountFlags Samples {
			get { return multisampleState.rasterizationSamples; }
		}

		public GraphicPipelineConfig () {

		}

		/// <summary>
		/// Create a default pipeline configuration with viewport and scissor as dynamic states. One blend attachment is
		/// added automatically with blending disabled. (cfg.blendAttachments[0])
		/// </summary>
		public static GraphicPipelineConfig CreateDefault (VkPrimitiveTopology topology = VkPrimitiveTopology.TriangleList,
			VkSampleCountFlags samples = VkSampleCountFlags.SampleCount1, bool depthTestEnabled = true)
		{
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

            cfg.viewportState.viewportCount = 1;
            cfg.viewportState.scissorCount = 1;

            cfg.blendAttachments.Add (new VkPipelineColorBlendAttachmentState (false));

            cfg.dynamicStates.Add (VkDynamicState.Viewport);
            cfg.dynamicStates.Add (VkDynamicState.Scissor);

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
				currentAttributeoffset += fs.blockSizeInBits/8;
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

		public void ResetShadersAndVerticesInfos () {
			foreach (ShaderInfo shader in shaders) 
				shader.Dispose ();
            currentAttributeIndex = 0;
            vertexBindings.Clear ();
			vertexAttributes.Clear ();
			shaders.Clear ();
		}
	}
}
