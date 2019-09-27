// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System.Collections.Generic;
using Vulkan;

namespace vke {
	public class SubPass {
		public uint Index { get; internal set; }
		List<VkAttachmentReference> colorRefs = new List<VkAttachmentReference>();
        List<VkAttachmentReference> inputRefs = new List<VkAttachmentReference>();
        MarshaledObject<VkAttachmentReference> depthRef;
        List<VkAttachmentReference> resolveRefs = new List<VkAttachmentReference>();
        List<uint> preservedRefs = new List<uint>();

        public SubPass () {
        }
		public SubPass (params VkImageLayout[] layouts) {
			for (uint i = 0; i < layouts.Length; i++) 
				AddColorReference (i, layouts[i]);
		}

		public void AddColorReference (uint attachment, VkImageLayout layout = VkImageLayout.DepthStencilAttachmentOptimal) {
            AddColorReference (new VkAttachmentReference { attachment = attachment, layout = layout });
        }
        public void AddColorReference (params VkAttachmentReference[] refs) {
            if (colorRefs == null)
                colorRefs = new List<VkAttachmentReference> ();
            for (int i = 0; i < refs.Length; i++)
                colorRefs.Add (refs[i]);
        }
        public void AddInputReference (params VkAttachmentReference[] refs) {
        	inputRefs.AddRange (refs);
        }
        public void AddPreservedReference (params uint[] refs) {
            preservedRefs.AddRange (refs);
        }
        public void SetDepthReference (uint attachment, VkImageLayout layout = VkImageLayout.DepthStencilAttachmentOptimal) {
            DepthReference = new VkAttachmentReference { attachment = attachment, layout = layout };
        }
		public void AddResolveReference (params VkAttachmentReference[] refs) {
			resolveRefs.AddRange (refs);
		}
		public void AddResolveReference (uint attachment, VkImageLayout layout = VkImageLayout.ColorAttachmentOptimal) {
			AddResolveReference (new VkAttachmentReference { attachment = attachment, layout = layout });
		}
		public VkAttachmentReference DepthReference {
            set {
                if (depthRef != null)
                    depthRef.Dispose ();
                depthRef = new MarshaledObject<VkAttachmentReference> (value);
            }
        }

		/// <summary>
		/// after having fetched the vkSubpassDescription structure, the lists of attachment are pinned,
		/// so it is mandatory to call the UnpinLists methods after.
		/// </summary>
		public VkSubpassDescription SubpassDescription {
            get {
                VkSubpassDescription subpassDescription = new VkSubpassDescription ();
                subpassDescription.pipelineBindPoint = VkPipelineBindPoint.Graphics;
                if (colorRefs.Count > 0) {
                    subpassDescription.colorAttachmentCount = (uint)colorRefs.Count;
                    subpassDescription.pColorAttachments = colorRefs.Pin(); ; 
                }
                if (inputRefs.Count > 0) {
                    subpassDescription.inputAttachmentCount = (uint)inputRefs.Count;
                    subpassDescription.pInputAttachments = inputRefs.Pin (); ;
                }
                if (preservedRefs.Count > 0) {
                    subpassDescription.preserveAttachmentCount = (uint)preservedRefs.Count;
                    subpassDescription.pPreserveAttachments = preservedRefs.Pin (); ;
                }
				if (resolveRefs.Count > 0)
					subpassDescription.pResolveAttachments = resolveRefs.Pin ();

				if (depthRef != null)
                    subpassDescription.pDepthStencilAttachment = depthRef.Pointer;

                return subpassDescription;
            }        
        }

		public void UnpinLists () {
			if (colorRefs.Count > 0) 
				colorRefs.Unpin (); 
			if (inputRefs.Count > 0) 			
				inputRefs.Unpin ();
			if (preservedRefs.Count > 0) 
				preservedRefs.Unpin ();
			if (resolveRefs.Count > 0)
				resolveRefs.Unpin ();
		}

		public static implicit operator uint(SubPass sp) => sp.Index;
	}
}
