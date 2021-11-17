// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Xml.Serialization;

namespace Vulkan {
	public static partial class Utils {
		/// <summary>Throw an erro if VkResult != Success.</summary>
		public static void CheckResult (VkResult result, string errorString = "Call failed") {
            if (result != VkResult.Success)
                throw new InvalidOperationException (errorString + ": " + result.ToString ());
        }
		static void xmlMakeTypeFieldsAsAttributes (Type t, ref XmlAttributeOverrides overrides)
		{
			foreach (FieldInfo fi in t.GetFields (BindingFlags.Public | BindingFlags.Instance))
				overrides.Add (t, fi.Name, new XmlAttributes { XmlAttribute = new XmlAttributeAttribute () });
		}
		public static XmlAttributeOverrides GetXmlOverrides ()
		{
			XmlAttributeOverrides xmlAttributeOverrides = new XmlAttributeOverrides ();
			//Assembly avk = Assembly.GetAssembly (typeof (VkInstance));
			xmlMakeTypeFieldsAsAttributes (typeof (VkDescriptorPoolSize), ref xmlAttributeOverrides);
			return xmlAttributeOverrides;
		}
		static bool tryFindResource (Assembly a, string resId, out Stream stream) {
			stream = null;
			if (a == null)
				return false;
			stream = a.GetManifestResourceStream (resId);
			return stream != null;
		}
		/// <summary>
		/// Return a file or embedded resource stream.
		/// </summary>
		/// <returns>The stream from path.</returns>
		/// <param name="path">The file or stream path. Embedded resource path starts with '#'.</param>
		public static Stream GetStreamFromPath (string path) {
			if (path.StartsWith ("#", StringComparison.Ordinal)) {
				Stream stream = null;
				string resId = path.Substring (1);
				if (tryFindResource (Assembly.GetEntryAssembly (), resId, out stream))
					return stream;
				string[] assemblyNames = resId.Split ('.');
				Assembly assembly = AppDomain.CurrentDomain.GetAssemblies ().FirstOrDefault (aa => aa.GetName ().Name == assemblyNames[0]);
				if (assembly == null && assemblyNames.Length > 3)
					assembly = AppDomain.CurrentDomain.GetAssemblies ()
						.FirstOrDefault (aa => aa.GetName ().Name == $"{assemblyNames[0]}.{assemblyNames[1]}");
				if (assembly != null && tryFindResource (assembly, resId, out stream))
					return stream;

				if (assembly != null)
					new Exception("Embedded resource not found in assembly: " + path);

				throw new Exception ("Resource not found: " + path);
			}
			if (!File.Exists (path))
				throw new FileNotFoundException ("File not found: ", path);
			return new FileStream (path, FileMode.Open, FileAccess.Read);
		}
		/// <summary>Convert angle from degree to radian.</summary>
		public static float DegreesToRadians (float degrees) {
            return degrees * (float)Math.PI / 180f;
        }

		/// <summary>
		/// Populate a Vector3 with values from a float array
		/// </summary>
		public static void FromFloatArray (ref Vector3 v, float[] floats) {
			if (floats.Length > 0)
				v.X = floats[0];
			if (floats.Length > 1)
				v.Y = floats[1];
			if (floats.Length > 2)
				v.Z = floats[2];
		}
		/// <summary>
		/// Populate a Vector4 with values from a float array
		/// </summary>
		public static void FromFloatArray (ref Vector4 v, float[] floats) {
			if (floats.Length > 0)
				v.X = floats[0];
			if (floats.Length > 1)
				v.Y = floats[1];
			if (floats.Length > 2)
				v.Z = floats[2];
			if (floats.Length > 3)
				v.W = floats[3];
        }
		/// <summary>
		/// Populate a Quaternion with values from a float array
		/// </summary>
		public static void FromFloatArray (ref Quaternion v, float[] floats) {
			if (floats.Length > 0)
				v.X = floats[0];
			if (floats.Length > 1)
				v.Y = floats[1];
			if (floats.Length > 2)
				v.Z = floats[2];
			if (floats.Length > 3)
				v.W = floats[3];
		}
		/// <summary>
		/// Populate a Vector2 with values from a byte array starting at offset
		/// </summary>
		public static void FromByteArray (ref Vector2 v, byte[] byteArray, int offset) {
            v.X = BitConverter.ToSingle (byteArray, offset);
            v.Y = BitConverter.ToSingle (byteArray, offset + 4);
        }
		/// <summary>
		/// Populate a Vector3 with values from a byte array starting at offset
		/// </summary>
		public static void FromByteArray (ref Vector3 v, byte[] byteArray, int offset) {
            v.X = BitConverter.ToSingle (byteArray, offset);
            v.Y = BitConverter.ToSingle (byteArray, offset + 4);
            v.Z = BitConverter.ToSingle (byteArray, offset + 8);
        }
		/// <summary>
		/// Populate a Vector4 with values from a byte array starting at offset
		/// </summary>
		public static void FromByteArray (ref Vector4 v, byte[] byteArray, int offset) {
            v.X = BitConverter.ToSingle (byteArray, offset);
            v.Y = BitConverter.ToSingle (byteArray, offset + 4);
            v.Z = BitConverter.ToSingle (byteArray, offset + 8);
            v.W = BitConverter.ToSingle (byteArray, offset + 12);
        }
		/// <summary>
		/// Populate a Quaternion with values from a byte array starting at offset
		/// </summary>
		public static void FromByteArray (ref Quaternion v, byte[] byteArray, int offset) {
            v.X = BitConverter.ToSingle (byteArray, offset);
            v.Y = BitConverter.ToSingle (byteArray, offset + 4);
            v.Z = BitConverter.ToSingle (byteArray, offset + 8);
            v.W = BitConverter.ToSingle (byteArray, offset + 12);
        }

		#region Extensions methods
		public static void ImportFloatArray (this ref Vector3 v, float[] floats) {
			if (floats.Length > 0)
				v.X = floats[0];
			if (floats.Length > 1)
				v.Y = floats[1];
			if (floats.Length > 2)
				v.Z = floats[2];
		}
		public static Vector3 Transform (this Vector3 v, ref Matrix4x4 mat, bool translate = false) {
			Vector4 v4 = Vector4.Transform (new Vector4 (v, translate ? 1f : 0f), mat);
			return new Vector3 (v4.X, v4.Y, v4.Z);
		}
		public static Vector3 ToVector3 (this Vector4 v) {
			return new Vector3 (v.X, v.Y, v.Z);
		}
		#endregion

		// Fixed sub resource on first mip level and layer
		public static void setImageLayout (
            VkCommandBuffer cmdbuffer,
            VkImage image,
            VkImageAspectFlags aspectMask,
            VkImageLayout oldImageLayout,
            VkImageLayout newImageLayout,
            VkPipelineStageFlags srcStageMask = VkPipelineStageFlags.AllCommands,
            VkPipelineStageFlags dstStageMask = VkPipelineStageFlags.AllCommands) {
            VkImageSubresourceRange subresourceRange = new VkImageSubresourceRange {
                aspectMask = aspectMask,
                baseMipLevel = 0,
                levelCount = 1,
                layerCount = 1,
            };
            setImageLayout (cmdbuffer, image, aspectMask, oldImageLayout, newImageLayout, subresourceRange);
        }

        // Create an image memory barrier for changing the layout of
        // an image and put it into an active command buffer
        // See chapter 11.4 "Image Layout" for details

        public static void setImageLayout (
            VkCommandBuffer cmdbuffer,
            VkImage image,
            VkImageAspectFlags aspectMask,
            VkImageLayout oldImageLayout,
            VkImageLayout newImageLayout,
            VkImageSubresourceRange subresourceRange,
            VkPipelineStageFlags srcStageMask = VkPipelineStageFlags.AllCommands,
            VkPipelineStageFlags dstStageMask = VkPipelineStageFlags.AllCommands) {
            // Create an image barrier object
            VkImageMemoryBarrier imageMemoryBarrier = VkImageMemoryBarrier.New();
            imageMemoryBarrier.srcQueueFamilyIndex = Vk.QueueFamilyIgnored;
            imageMemoryBarrier.dstQueueFamilyIndex = Vk.QueueFamilyIgnored;
            imageMemoryBarrier.oldLayout = oldImageLayout;
            imageMemoryBarrier.newLayout = newImageLayout;
            imageMemoryBarrier.image = image;
            imageMemoryBarrier.subresourceRange = subresourceRange;

            // Source layouts (old)
            // Source access mask controls actions that have to be finished on the old layout
            // before it will be transitioned to the new layout
            switch (oldImageLayout) {
                case VkImageLayout.Undefined:
                    // Image layout is undefined (or does not matter)
                    // Only valid as initial layout
                    // No flags required, listed only for completeness
                    imageMemoryBarrier.srcAccessMask = 0;
                    break;

                case VkImageLayout.Preinitialized:
                    // Image is preinitialized
                    // Only valid as initial layout for linear images, preserves memory contents
                    // Make sure host writes have been finished
                    imageMemoryBarrier.srcAccessMask = VkAccessFlags.HostWrite;
                    break;

                case VkImageLayout.ColorAttachmentOptimal:
                    // Image is a color attachment
                    // Make sure any writes to the color buffer have been finished
                    imageMemoryBarrier.srcAccessMask = VkAccessFlags.ColorAttachmentWrite;
                    break;

                case VkImageLayout.DepthStencilAttachmentOptimal:
                    // Image is a depth/stencil attachment
                    // Make sure any writes to the depth/stencil buffer have been finished
                    imageMemoryBarrier.srcAccessMask = VkAccessFlags.DepthStencilAttachmentWrite;
                    break;

                case VkImageLayout.TransferSrcOptimal:
                    // Image is a transfer source
                    // Make sure any reads from the image have been finished
                    imageMemoryBarrier.srcAccessMask = VkAccessFlags.TransferRead;
                    break;

                case VkImageLayout.TransferDstOptimal:
                    // Image is a transfer destination
                    // Make sure any writes to the image have been finished
                    imageMemoryBarrier.srcAccessMask = VkAccessFlags.TransferWrite;
                    break;

                case VkImageLayout.ShaderReadOnlyOptimal:
                    // Image is read by a shader
                    // Make sure any shader reads from the image have been finished
                    imageMemoryBarrier.srcAccessMask = VkAccessFlags.ShaderRead;
                    break;
            }

            // Target layouts (new)
            // Destination access mask controls the dependency for the new image layout
            switch (newImageLayout) {
                case VkImageLayout.TransferDstOptimal:
                    // Image will be used as a transfer destination
                    // Make sure any writes to the image have been finished
                    imageMemoryBarrier.dstAccessMask = VkAccessFlags.TransferWrite;
                    break;

                case VkImageLayout.TransferSrcOptimal:
                    // Image will be used as a transfer source
                    // Make sure any reads from and writes to the image have been finished
                    imageMemoryBarrier.srcAccessMask = imageMemoryBarrier.srcAccessMask | VkAccessFlags.TransferRead;
                    imageMemoryBarrier.dstAccessMask = VkAccessFlags.TransferRead;
                    break;

                case VkImageLayout.ColorAttachmentOptimal:
                    // Image will be used as a color attachment
                    // Make sure any writes to the color buffer have been finished
                    imageMemoryBarrier.srcAccessMask = VkAccessFlags.TransferRead;
                    imageMemoryBarrier.dstAccessMask = VkAccessFlags.ColorAttachmentWrite;
                    break;

                case VkImageLayout.DepthStencilAttachmentOptimal:
                    // Image layout will be used as a depth/stencil attachment
                    // Make sure any writes to depth/stencil buffer have been finished
                    imageMemoryBarrier.dstAccessMask = imageMemoryBarrier.dstAccessMask | VkAccessFlags.DepthStencilAttachmentWrite;
                    break;

                case VkImageLayout.ShaderReadOnlyOptimal:
                    // Image will be read in a shader (sampler, input attachment)
                    // Make sure any writes to the image have been finished
                    if (imageMemoryBarrier.srcAccessMask == 0) {
                        imageMemoryBarrier.srcAccessMask = VkAccessFlags.HostWrite | VkAccessFlags.TransferWrite;
                    }
                    imageMemoryBarrier.dstAccessMask = VkAccessFlags.ShaderRead;
                    break;
            }

            // Put barrier inside setup command buffer
            Vk.vkCmdPipelineBarrier (
                cmdbuffer,
                srcStageMask,
                dstStageMask,
                0,
                0, IntPtr.Zero,
                0, IntPtr.Zero,
                1, ref imageMemoryBarrier);
        }
		/// <summary>
		/// Find usage flags and aspect flag from image layout
		/// </summary>
		public static void QueryLayoutRequirements (VkImageLayout layout, ref VkImageUsageFlags usage, ref VkImageAspectFlags aspectFlags) {
			switch (layout) {
				case VkImageLayout.ColorAttachmentOptimal:
				case VkImageLayout.PresentSrcKHR:
				case VkImageLayout.SharedPresentKHR:
					aspectFlags |= VkImageAspectFlags.Color;
					if (usage.HasFlag (VkImageUsageFlags.Sampled))
						usage |= VkImageUsageFlags.InputAttachment;
					usage |= VkImageUsageFlags.ColorAttachment;
					break;
				case VkImageLayout.DepthStencilAttachmentOptimal:
					aspectFlags |= VkImageAspectFlags.Depth | VkImageAspectFlags.Stencil;
					usage |= VkImageUsageFlags.DepthStencilAttachment;
					break;
				case VkImageLayout.DepthStencilReadOnlyOptimal:
					aspectFlags |= VkImageAspectFlags.Depth | VkImageAspectFlags.Stencil;
					if (usage.HasFlag (VkImageUsageFlags.ColorAttachment))
						usage |= VkImageUsageFlags.InputAttachment;
					else
						usage |= VkImageUsageFlags.Sampled;
					break;
				case VkImageLayout.ShaderReadOnlyOptimal:
					aspectFlags |= VkImageAspectFlags.Color;
					usage |= VkImageUsageFlags.Sampled;
					break;
				case VkImageLayout.TransferSrcOptimal:
					usage |= VkImageUsageFlags.TransferSrc;
					break;
				case VkImageLayout.TransferDstOptimal:
					usage |= VkImageUsageFlags.TransferDst;
					break;
				case VkImageLayout.DepthReadOnlyStencilAttachmentOptimalKHR:
				case VkImageLayout.DepthAttachmentStencilReadOnlyOptimalKHR:
					aspectFlags |= VkImageAspectFlags.Depth | VkImageAspectFlags.Stencil;
					usage |= VkImageUsageFlags.Sampled | VkImageUsageFlags.DepthStencilAttachment;
					break;
			}
		}
		/// <summary>
		/// Try to get the block width and height of a compressed format
		/// </summary>
		/// <returns><c>true</c>return true if format given as first argument is a compressed format.</returns>
		/// <param name="format">Vulkan format to test.</param>
		/// <param name="width">Compressed block width.</param>
		/// <param name="height">Compressed block height.</param>
		public static bool TryGetCompressedFormatBlockSize (this VkFormat format, out uint width, out uint height)
		{
			width = height = 1;
			if (format < VkFormat.Bc1RgbUnormBlock || format > (VkFormat)1000066013) //VK_FORMAT_ASTC_12x12_SFLOAT_BLOCK_EXT)
				return false;
			if (format < VkFormat.Astc5x4UnormBlock)
				width = height = 4;
			else {
				string str = format.ToString ();
				if (str.StartsWith ("Astc", StringComparison.OrdinalIgnoreCase)) {
					width = uint.Parse (str.Substring (4, 1));
					height = uint.Parse (str.Substring (6, 1));
				}
			}

			return true;
		}
		//TODO:quick done list, refine needed
		public static VkPipelineStageFlags GetDefaultStage (this VkImageLayout layout) {
			switch (layout) {
			case VkImageLayout.Preinitialized:
			case VkImageLayout.Undefined:
				return VkPipelineStageFlags.AllCommands;

			case VkImageLayout.General:
				return VkPipelineStageFlags.ComputeShader;

			case VkImageLayout.ColorAttachmentOptimal:
			case VkImageLayout.DepthStencilAttachmentOptimal:
				return VkPipelineStageFlags.ColorAttachmentOutput;

			case VkImageLayout.DepthStencilReadOnlyOptimal:
			case VkImageLayout.DepthReadOnlyStencilAttachmentOptimalKHR:
			case VkImageLayout.DepthAttachmentStencilReadOnlyOptimalKHR:
				return VkPipelineStageFlags.EarlyFragmentTests;

			case VkImageLayout.ShaderReadOnlyOptimal:
				return VkPipelineStageFlags.FragmentShader;

			case VkImageLayout.TransferSrcOptimal:
			case VkImageLayout.TransferDstOptimal:
				return VkPipelineStageFlags.Transfer;

			case VkImageLayout.PresentSrcKHR:
			case VkImageLayout.SharedPresentKHR:
				return VkPipelineStageFlags.ColorAttachmentOutput;

			//case VkImageLayout.ShadingRateOptimalNV:
			//case VkImageLayout.FragmentDensityMapOptimalEXT:
			default:
				return VkPipelineStageFlags.AllCommands;
			}
		}
		public static Matrix4x4 CreatePerspectiveFieldOfView (float fov, float aspectRatio, float zNear, float zFar) {
			float f = (float)(1.0 / System.Math.Tan (0.5 * fov));
			return new Matrix4x4 (
				f / aspectRatio, 0, 0, 0,
				0, -f, 0, 0,
				0, 0, zFar / (zNear - zFar), -1,
				0, 0, zNear * zFar / (zNear - zFar), 0
			);
		}

		public static VkShaderStageFlags ShaderKindToStageFlag (shaderc.ShaderKind shaderKind) {
			switch (shaderKind) {
			case shaderc.ShaderKind.VertexShader:
			case shaderc.ShaderKind.GlslDefaultVertexShader:
				return VkShaderStageFlags.Vertex;
			case shaderc.ShaderKind.FragmentShader:
			case shaderc.ShaderKind.GlslDefaultFragmentShader:
				return VkShaderStageFlags.Fragment;
			case shaderc.ShaderKind.ComputeShader:
			case shaderc.ShaderKind.GlslDefaultComputeShader:
				return VkShaderStageFlags.Compute;
			case shaderc.ShaderKind.GeometryShader:
			case shaderc.ShaderKind.GlslDefaultGeometryShader:
				return VkShaderStageFlags.Geometry;
			case shaderc.ShaderKind.TessControlShader:
			case shaderc.ShaderKind.GlslDefaultTessControlShader:
				return VkShaderStageFlags.TessellationControl;
			case shaderc.ShaderKind.TessEvaluationShader:
			case shaderc.ShaderKind.GlslDefaultTessEvaluationShader:
				return VkShaderStageFlags.TessellationEvaluation;
			case shaderc.ShaderKind.RaygenShader:
			case shaderc.ShaderKind.GlslDefaultRaygenShader:
				return VkShaderStageFlags.RaygenKHR;
			case shaderc.ShaderKind.AnyhitShader:
			case shaderc.ShaderKind.GlslDefaultAnyhitShader:
				return VkShaderStageFlags.AnyHitKHR;
			case shaderc.ShaderKind.ClosesthitShader:
			case shaderc.ShaderKind.GlslDefaultClosesthitShader:
				return VkShaderStageFlags.ClosestHitKHR;
			case shaderc.ShaderKind.MissShader:
			case shaderc.ShaderKind.GlslDefaultMissShader:
				return VkShaderStageFlags.MissKHR;
			case shaderc.ShaderKind.IntersectionShader:
			case shaderc.ShaderKind.GlslDefaultIntersectionShader:
				return VkShaderStageFlags.IntersectionKHR;
			case shaderc.ShaderKind.CallableShader:
			case shaderc.ShaderKind.GlslDefaultCallableShader:
				return VkShaderStageFlags.CallableKHR;
			case shaderc.ShaderKind.TaskShader:
			case shaderc.ShaderKind.GlslDefaultTaskShader:
				return VkShaderStageFlags.TaskNV;
			case shaderc.ShaderKind.MeshShader:
			case shaderc.ShaderKind.GlslDefaultMeshShader:
				return VkShaderStageFlags.MeshNV;
			default:
				throw new NotSupportedException ($"shaderc shaderKind {shaderKind} conversion to VK StageFlag  not handled");
			}
		}
		public static shaderc.ShaderKind ShaderStageToShaderKind (VkShaderStageFlags stageFlag) {
			switch (stageFlag) {
			case VkShaderStageFlags.Vertex:
				return shaderc.ShaderKind.VertexShader;
			case VkShaderStageFlags.TessellationControl:
				return shaderc.ShaderKind.TessControlShader;
			case VkShaderStageFlags.TessellationEvaluation:
				return shaderc.ShaderKind.TessEvaluationShader;
			case VkShaderStageFlags.Geometry:
				return shaderc.ShaderKind.GeometryShader;
			case VkShaderStageFlags.Fragment:
				return shaderc.ShaderKind.FragmentShader;
			case VkShaderStageFlags.Compute:
				return shaderc.ShaderKind.ComputeShader;
			case VkShaderStageFlags.RaygenKHR:
				return shaderc.ShaderKind.RaygenShader;
			case VkShaderStageFlags.AnyHitKHR:
				return shaderc.ShaderKind.AnyhitShader;
			case VkShaderStageFlags.ClosestHitKHR:
				return shaderc.ShaderKind.ClosesthitShader;
			case VkShaderStageFlags.MissKHR:
				return shaderc.ShaderKind.MissShader;
			case VkShaderStageFlags.IntersectionKHR:
				return shaderc.ShaderKind.IntersectionShader;
			case VkShaderStageFlags.CallableKHR:
				return shaderc.ShaderKind.CallableShader;
			case VkShaderStageFlags.TaskNV:
				return shaderc.ShaderKind.TaskShader;
			case VkShaderStageFlags.MeshNV:
				return shaderc.ShaderKind.MeshShader;
			default:
				throw new NotSupportedException ($"Error Shader Stage flag conversion to ShaderKind: {stageFlag}");
			}
		}
	}
}
