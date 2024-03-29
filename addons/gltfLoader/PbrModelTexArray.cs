﻿// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Vulkan;

namespace vke.glTF {
	/// <summary>
	/// Indexed pbr model whith one descriptorSet per material with separate textures attachments
	/// </summary>
	public class PbrModelTexArray : PbrModel {
		public static uint TEXTURE_DIM = 512;
		public new struct Vertex {
			[VertexAttribute (VertexAttributeType.Position, VkFormat.R32g32b32Sfloat)]
			public Vector3 pos;
			[VertexAttribute (VertexAttributeType.Normal, VkFormat.R32g32b32Sfloat)]
			public Vector3 normal;
			[VertexAttribute (VertexAttributeType.UVs, VkFormat.R32g32Sfloat)]
			public Vector2 uv0;
			[VertexAttribute (VertexAttributeType.UVs, VkFormat.R32g32Sfloat)]
			public Vector2 uv1;
			public override string ToString () {
				return pos.ToString () + ";" + normal.ToString () + ";" + uv0.ToString () + ";" + uv1.ToString ();
			}
		};

		/// <summary>
		/// Material structure for ubo containing texture indices in tex array
		/// </summary>
		public struct Material {
			public Vector4 baseColorFactor;
			public Vector4 emissiveFactor;
			public Vector4 diffuseFactor;
			public Vector4 specularFactor;

			public float workflow;
			public AttachmentType TexCoord0;
			public AttachmentType TexCoord1;
			public int baseColorTextureSet;

			public int phyDescTex;
			public int normalTex;
			public int aoTex;
			public int emissiveTex;

			public float metallicFactor;
			public float roughnessFactor;
			public float alphaMask;
			public float alphaMaskCutoff;

		}

		public Image texArray;
		public Material[] materials;

		public PbrModelTexArray (Queue transferQ, string path) {
			dev = transferQ.Dev;
			using (CommandPool cmdPool = new CommandPool (dev, transferQ.index)) {
				using (glTFLoader ctx = new glTFLoader (path, transferQ, cmdPool)) {
					loadSolids<Vertex> (ctx);

					if (ctx.ImageCount > 0) {
						texArray = new Image (dev, Image.DefaultTextureFormat, VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferDst | VkImageUsageFlags.TransferSrc,
							VkMemoryPropertyFlags.DeviceLocal, TEXTURE_DIM, TEXTURE_DIM, VkImageType.Image2D,
							VkSampleCountFlags.SampleCount1, VkImageTiling.Optimal, Image.ComputeMipLevels (TEXTURE_DIM), ctx.ImageCount);

						ctx.BuildTexArray (ref texArray, 0);
					} else {
						texArray = new Image (dev, Image.DefaultTextureFormat, VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferDst | VkImageUsageFlags.TransferSrc,
							VkMemoryPropertyFlags.DeviceLocal, TEXTURE_DIM, TEXTURE_DIM, VkImageType.Image2D,
							VkSampleCountFlags.SampleCount1, VkImageTiling.Optimal, Image.ComputeMipLevels (TEXTURE_DIM), 1);
						PrimaryCommandBuffer cmd = cmdPool.AllocateAndStart (VkCommandBufferUsageFlags.OneTimeSubmit);
						texArray.SetLayout (cmd, VkImageAspectFlags.Color, VkImageLayout.ShaderReadOnlyOptimal);
						transferQ.EndSubmitAndWait (cmd, true);
					}

					texArray.CreateView (VkImageViewType.ImageView2DArray, VkImageAspectFlags.Color);
					texArray.CreateSampler ();
					texArray.Descriptor.imageLayout = VkImageLayout.ShaderReadOnlyOptimal;
					texArray.SetName ("model texArray");


					loadMaterials (ctx);
					materialUBO = new HostBuffer<Material> (dev, VkBufferUsageFlags.UniformBuffer, materials);
				}
			}
		}

		void loadMaterials (glTFLoader ctx) {
			glTFLoader.Material[] mats = ctx.LoadMaterial ();
			materials = new Material[mats.Length];

			for (int i = 0; i < mats.Length; i++) {
				materials[i] = new Material {
					workflow = (float)mats[i].workflow,
					baseColorFactor = mats[i].baseColorFactor,
					emissiveFactor = mats[i].emissiveFactor,
					metallicFactor = mats[i].metallicFactor,
					roughnessFactor = mats[i].roughnessFactor,

					baseColorTextureSet = mats[i].baseColorTexture,
					phyDescTex = mats[i].metallicRoughnessTexture,
					normalTex = mats[i].normalTexture,
					aoTex = mats[i].occlusionTexture,
					emissiveTex = mats[i].emissiveTexture,

					TexCoord0 = mats[i].availableAttachments,
					TexCoord1 = mats[i].availableAttachments1,

					alphaMask = 0f,
					alphaMaskCutoff = 0.0f,
					diffuseFactor = new Vector4 (0),
					specularFactor = new Vector4 (0)
				};
			}
		}

		public override void RenderNode (CommandBuffer cmd, PipelineLayout pipelineLayout, Node node, Matrix4x4 currentTransform, bool shadowPass = false) {
			Matrix4x4 localMat = node.localMatrix * currentTransform;

			cmd.PushConstant (pipelineLayout, VkShaderStageFlags.Vertex, localMat);

			if (node.Mesh != null) {
				foreach (Primitive p in node.Mesh.Primitives) {
					if (!shadowPass)
						cmd.PushConstant (pipelineLayout, VkShaderStageFlags.Fragment, (int)p.material, (uint)Marshal.SizeOf<Matrix4x4> ());
					cmd.DrawIndexed (p.indexCount, 1, p.indexBase, p.vertexBase, 0);
				}
			}
			if (node.Children == null)
				return;
			foreach (Node child in node.Children)
				RenderNode (cmd, pipelineLayout, child, localMat, shadowPass);
		}

		protected override void Dispose (bool disposing) {
			if (!isDisposed) {
				if (disposing) {
					texArray?.Dispose ();
				} else
					Debug.WriteLine ("model was not disposed");
			}
			base.Dispose (disposing);
		}
	}
}
