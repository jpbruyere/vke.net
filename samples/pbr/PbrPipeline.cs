// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)

using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Vulkan;

namespace vke {
	class PBRPipeline : GraphicPipeline {

		public struct Matrices {
			public Matrix4x4 projection;
			public Matrix4x4 model;
			public Matrix4x4 view;
			public Vector4 camPos;
			public Vector4 lightDir;
			public float exposure;
			public float gamma;
			public float prefilteredCubeMipLevels;
			public float scaleIBLAmbient;
			public float debugViewInputs;
			public float debugViewEquation;
		}

		public Matrices matrices = new Matrices {
			lightDir = Vector4.Normalize (new Vector4 (0.7f, 0.6f, 0.2f, 0.0f)),
			gamma = 1.2f,
			exposure = 2.5f,
			scaleIBLAmbient = 0.9f,
			debugViewInputs = 0,
			debugViewEquation = 0
		};

		public HostBuffer uboMats;

		DescriptorPool descriptorPool;

		DescriptorSetLayout descLayoutMain;
		DescriptorSetLayout descLayoutTextures;
		public DescriptorSet dsMain;

		public PbrModel2 model;
		public vke.Environment.EnvironmentCube envCube;
		string[] cubemapPathes = {
			Utils.DataDirectory + "textures/papermill.ktx",
			Utils.DataDirectory + "textures/cubemap_yokohama_bc3_unorm.ktx",
			Utils.DataDirectory + "textures/gcanyon_cube.ktx",
			Utils.DataDirectory + "textures/pisa_cube.ktx",
			Utils.DataDirectory + "textures/uffizi_cube.ktx",
		};
		public PBRPipeline (Queue staggingQ, RenderPass renderPass, PipelineCache pipelineCache = null) :
			base (renderPass, pipelineCache, "pbr pipeline") {

			descriptorPool = new DescriptorPool (Dev, 2,
				new VkDescriptorPoolSize (VkDescriptorType.UniformBuffer, 2),
				new VkDescriptorPoolSize (VkDescriptorType.CombinedImageSampler, 8)
			);

			descLayoutMain = new DescriptorSetLayout (Dev,
				new VkDescriptorSetLayoutBinding (0, VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment, VkDescriptorType.UniformBuffer),
				new VkDescriptorSetLayoutBinding (1, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler),
				new VkDescriptorSetLayoutBinding (2, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler),
				new VkDescriptorSetLayoutBinding (3, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler),
				new VkDescriptorSetLayoutBinding (4, VkShaderStageFlags.Fragment, VkDescriptorType.UniformBuffer));				

			descLayoutTextures = new DescriptorSetLayout (Dev,
				new VkDescriptorSetLayoutBinding (0, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler),
				new VkDescriptorSetLayoutBinding (1, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler),
				new VkDescriptorSetLayoutBinding (2, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler),
				new VkDescriptorSetLayoutBinding (3, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler),
				new VkDescriptorSetLayoutBinding (4, VkShaderStageFlags.Fragment, VkDescriptorType.CombinedImageSampler)
			);
				
			GraphicPipelineConfig cfg = GraphicPipelineConfig.CreateDefault (VkPrimitiveTopology.TriangleList, renderPass.Samples);
			cfg.Layout = new PipelineLayout (Dev, descLayoutMain, descLayoutTextures);
			cfg.Layout.AddPushConstants (
				new VkPushConstantRange (VkShaderStageFlags.Vertex, (uint)Marshal.SizeOf<Matrix4x4> ()),
				new VkPushConstantRange (VkShaderStageFlags.Fragment, sizeof(int), 64)
			);
			cfg.RenderPass = renderPass;
			cfg.AddVertexBinding<PbrModel2.Vertex> (0);
			cfg.AddVertexAttributes (0, VkFormat.R32g32b32Sfloat, VkFormat.R32g32b32Sfloat, VkFormat.R32g32Sfloat, VkFormat.R32g32Sfloat);

			cfg.AddShader (Dev, VkShaderStageFlags.Vertex, "#shaders.pbr.vert.spv");
			cfg.AddShader (Dev, VkShaderStageFlags.Fragment, "#shaders.pbr_khr.frag.spv");

			layout = cfg.Layout;

			init (cfg);

			cfg.DisposeShaders ();

			dsMain = descriptorPool.Allocate (descLayoutMain);

			envCube = new Environment.EnvironmentCube (cubemapPathes[0], layout, staggingQ, RenderPass);

			matrices.prefilteredCubeMipLevels = envCube.prefilterCube.CreateInfo.mipLevels;
			uboMats = new HostBuffer (Dev, VkBufferUsageFlags.UniformBuffer, matrices, true);

			DescriptorSetWrites uboUpdate = new DescriptorSetWrites (descLayoutMain.Bindings.GetRange(0,4).ToArray());
			uboUpdate.Write (Dev, dsMain,
				uboMats.Descriptor,
				envCube.irradianceCube.Descriptor,
				envCube.prefilterCube.Descriptor,
				envCube.lutBrdf.Descriptor);
		}

		public void LoadModel (Queue staggingQ, string path) {
			model?.Dispose ();

			model = new PbrModel2 (staggingQ, path, descLayoutTextures,
				AttachmentType.Color,
				AttachmentType.PhysicalProps,
				AttachmentType.Normal,
				AttachmentType.AmbientOcclusion,
				AttachmentType.Emissive);

			DescriptorSetWrites uboUpdate = new DescriptorSetWrites (descLayoutMain.Bindings[4]);
			uboUpdate.Write (Dev, dsMain, model.materialUBO.Descriptor);
		}

		public void RecordDraw (PrimaryCommandBuffer cmd) {
			cmd.BindDescriptorSet (Layout, dsMain);
			envCube.RecordDraw (cmd);
			drawModel (cmd);
		}
		void drawModel (PrimaryCommandBuffer cmd) {
			Bind (cmd);
			model.Bind (cmd);
			model.DrawAll (cmd, Layout);

		}

		protected override void Dispose (bool disposing) {
			model.Dispose ();
			envCube.Dispose ();

			descLayoutMain.Dispose ();
			descLayoutTextures.Dispose ();
			descriptorPool.Dispose ();

			uboMats.Dispose ();

			base.Dispose (disposing);
		}
	}

}
