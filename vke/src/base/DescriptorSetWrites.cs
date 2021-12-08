//
// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Vulkan;
using static Vulkan.Vk;
using System.Linq;

namespace vke {
	/// <summary>
	/// Descriptor set writes is defined once, then update affect descriptors to write array
	/// </summary>
	public class DescriptorSetWrites {
		VkDescriptorSet? dstSetOverride = null;//when set, override target descriptors to update in each write
		public List<VkWriteDescriptorSet> WriteDescriptorSets = new List<VkWriteDescriptorSet> ();

		#region CTORS
		public DescriptorSetWrites () { }
		public DescriptorSetWrites (VkDescriptorSetLayoutBinding binding) {
			AddWriteInfo (binding);
		}
		public DescriptorSetWrites (DescriptorSet destSet, VkDescriptorSetLayoutBinding binding) {
			AddWriteInfo (destSet, binding);
		}
		public DescriptorSetWrites (params VkDescriptorSetLayoutBinding[] bindings) {
			AddWriteInfo (bindings);
		}
		public DescriptorSetWrites (DescriptorSet destSet, params VkDescriptorSetLayoutBinding[] bindings) {
			AddWriteInfo (destSet, bindings);
		}
		/// <summary>
		/// Configure the Write to update the full layout at once
		/// </summary>
		public DescriptorSetWrites (DescriptorSet destSet, DescriptorSetLayout layout) {
			foreach (VkDescriptorSetLayoutBinding binding in layout.Bindings) {
				AddWriteInfo (destSet, binding);
			}
		}
		/// <summary>
		/// Configure the Write to update the full layout at once
		/// </summary>
		public DescriptorSetWrites (DescriptorSetLayout layout) {
			foreach (VkDescriptorSetLayoutBinding binding in layout.Bindings) {
				AddWriteInfo (binding);
			}
		}
		#endregion

		/// <summary>
		/// Adds write info with a destination descriptor set, it could be overriden by calling Write
		/// with another descriptorSet in parametters
		/// </summary>
	 	public void AddWriteInfo (DescriptorSet destSet, params VkDescriptorSetLayoutBinding[] bindings) {
			foreach (VkDescriptorSetLayoutBinding binding in bindings)
				AddWriteInfo (destSet, binding);
        }
		/// <summary>
		/// Adds write info with a destination descriptor set, it could be overriden by calling Write
		/// with another descriptorSet in parametters
		/// </summary>
	 	public void AddWriteInfo (DescriptorSet destSet, VkDescriptorSetLayoutBinding binding) {
			VkWriteDescriptorSet wds = default;
			wds.descriptorType = binding.descriptorType;
			wds.descriptorCount = binding.descriptorCount;
			wds.dstBinding = binding.binding;
			wds.dstSet = destSet.handle;
			WriteDescriptorSets.Add (wds);
        }
		/// <summary>
		/// Adds write info without specifying a destination descriptor set, this imply that on calling Write, you MUST
		/// provide a desDescriptor!
		/// </summary>
		public void AddWriteInfo (VkDescriptorSetLayoutBinding[] bindings) {
			foreach (VkDescriptorSetLayoutBinding binding in bindings)
				AddWriteInfo (binding);
		}
		/// <summary>
		/// Adds write info without specifying a destination descriptor set, this imply that on calling Write, you MUST
		/// provide a desDescriptor!
		/// </summary>
		public void AddWriteInfo (VkDescriptorSetLayoutBinding binding) {
            VkWriteDescriptorSet wds = default;
            wds.descriptorType = binding.descriptorType;
            wds.descriptorCount = binding.descriptorCount;
            wds.dstBinding = binding.binding;
            WriteDescriptorSets.Add (wds);
		}

		/// <summary>
		/// execute the descriptors writes providing a target descriptorSet
		/// </summary>
		public void Write (Device dev, DescriptorSet set, params object[] descriptors) {
			dstSetOverride = set.handle;
			Write (dev, descriptors);
		}

		/// <summary>
		/// execute the descriptors writes targeting descriptorSets setted on AddWriteInfo call
		/// </summary>
		public void Write (Device dev, params object[] descriptors) {
			using (PinnedObjects pinCtx = new PinnedObjects ()) {
				int i = 0;
				int wdsPtr = 0;
				while (i < descriptors.Length) {
					int firstDescriptor = i;
					VkWriteDescriptorSet wds = WriteDescriptorSets[wdsPtr];
					wds.sType = VkStructureType.WriteDescriptorSet;
					if (dstSetOverride != null)
						wds.dstSet = dstSetOverride.Value.Handle;

					if (descriptors[i] is VkDescriptorBufferInfo)
						wds.pBufferInfo = descriptors.SubArray (i,(int)wds.descriptorCount).Cast<VkDescriptorBufferInfo>().ToArray();
					else if (descriptors[firstDescriptor] is VkDescriptorImageInfo)
						wds.pImageInfo = descriptors.SubArray (i,(int)wds.descriptorCount).Cast<VkDescriptorImageInfo>().ToArray();
					i+=(int)wds.descriptorCount;
					WriteDescriptorSets[wdsPtr] = wds;
					wdsPtr++;
				}
				vkUpdateDescriptorSets (dev.Handle, (uint)WriteDescriptorSets.Count, WriteDescriptorSets.Pin (pinCtx), 0, IntPtr.Zero);
			}
			foreach (VkWriteDescriptorSet wds in WriteDescriptorSets)
				wds.Dispose();
		}
		/// <summary>
		/// execute the descriptors writes targeting descriptorSets setted on AddWriteInfo call
		/// </summary>
		public void Push (PrimaryCommandBuffer cmd, PipelineLayout plLayout, params object[] descriptors) {
			using (PinnedObjects pinCtx = new PinnedObjects ()) {
				int i = 0;
				int wdsPtr = 0;
				while (i < descriptors.Length) {
					int firstDescriptor = i;
					VkWriteDescriptorSet wds = WriteDescriptorSets[wdsPtr];
					wds.dstSet = 0;
					IntPtr pDescriptors = IntPtr.Zero;

					if (wds.descriptorCount > 1) {
						List<IntPtr> descPtrArray = new List<IntPtr> ();
						for (int d = 0; d < wds.descriptorCount; d++) {
							descPtrArray.Add (descriptors[i].Pin (pinCtx));
							i++;
						}
						pDescriptors = descPtrArray.Pin (pinCtx);
					} else {
						pDescriptors = descriptors[i].Pin (pinCtx);
						i++;
					}
					/*
					if (descriptors[firstDescriptor] is VkDescriptorBufferInfo)
						wds.pBufferInfo = pDescriptors;
					else if (descriptors[firstDescriptor] is VkDescriptorImageInfo)
						wds.pImageInfo = pDescriptors;*/

					WriteDescriptorSets[wdsPtr] = wds;
					wdsPtr++;
				}
				vkCmdPushDescriptorSetKHR (cmd.Handle, VkPipelineBindPoint.Graphics, plLayout.handle, 0,
					 (uint)WriteDescriptorSets.Count, WriteDescriptorSets.Pin (pinCtx));
			}
		}
	}

	/// <summary>
	/// Descriptor set writes include descriptor in write addition with IDisposable model
	/// </summary>
	[Obsolete]
    public class DescriptorSetWrites2 : IDisposable {
        Device dev;
        List<VkWriteDescriptorSet> WriteDescriptorSets = new List<VkWriteDescriptorSet> ();
		List<object> descriptors = new List<object> ();

		public DescriptorSetWrites2 (Device device) {
            dev = device;
        }
        public void AddWriteInfo (DescriptorSet destSet, VkDescriptorSetLayoutBinding binding, VkDescriptorBufferInfo descriptor) {
			if (!descriptors.Contains (descriptor))
				descriptors.Add (descriptor);
            VkWriteDescriptorSet wds = default;
            wds.descriptorType = binding.descriptorType;
            wds.descriptorCount = binding.descriptorCount;
            wds.dstBinding = binding.binding;
            wds.dstSet = destSet.handle;
            wds.pBufferInfo = descriptor;

			WriteDescriptorSets.Add (wds);
        }
        public void AddWriteInfo (DescriptorSet destSet, VkDescriptorSetLayoutBinding binding, VkDescriptorImageInfo descriptor) {
			if (!descriptors.Contains (descriptor))
				descriptors.Add (descriptor);
            VkWriteDescriptorSet wds = default;
            wds.descriptorType = binding.descriptorType;
            wds.descriptorCount = binding.descriptorCount;
            wds.dstBinding = binding.binding;
            wds.dstSet = destSet.handle;
            wds.pImageInfo = descriptor;

            WriteDescriptorSets.Add (wds);
        }

        public void Update () {
            vkUpdateDescriptorSets (dev.Handle, (uint)WriteDescriptorSets.Count, WriteDescriptorSets.Pin (), 0, IntPtr.Zero);
			WriteDescriptorSets.Unpin ();
        }

        #region IDisposable Support
        private bool disposedValue = false; // Pour détecter les appels redondants

        protected virtual void Dispose (bool disposing) {
            if (!disposedValue) {
                foreach (object descriptor in descriptors)
                    descriptor.Unpin ();
                disposedValue = true;
            }
        }
        ~DescriptorSetWrites2() {
            Dispose(false);
        }
        // Ce code est ajouté pour implémenter correctement le modèle supprimable.
        public void Dispose () {
            Dispose (true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
