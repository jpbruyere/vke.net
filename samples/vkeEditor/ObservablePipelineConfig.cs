// Copyright (c) 2019  Bruyère Jean-Philippe jp_bruyere@hotmail.com
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using Crow;
using vke;
using Vulkan;

namespace vkeEditor {
	public class ObservablePipelineConfig : GraphicPipelineConfig, IValueChange {
		#region IValueChange implementation
		public event EventHandler<ValueChangeEventArgs> ValueChanged;
		public virtual void NotifyValueChanged (string MemberName, object _value)
		{
			ValueChanged?.Invoke (this, new Crow.ValueChangeEventArgs (MemberName, _value));
		}
		#endregion


		public ObservablePipelineConfig ()
		{
			Viewports = new ObservableList<VkViewport> ();
			Scissors = new ObservableList<VkRect2D> ();
			blendAttachments = new ObservableList<VkPipelineColorBlendAttachmentState> ();
			dynamicStates = new ObservableList<VkDynamicState> ();
			vertexBindings = new ObservableList<VkVertexInputBindingDescription> ();
			vertexAttributes = new ObservableList<VkVertexInputAttributeDescription> ();
			Shaders = new ObservableList<ShaderInfo> ();
		}

		bool isExpanded;
		public bool IsExpanded {
			get => isExpanded;
			set {
				if (isExpanded == value)
					return;
				isExpanded = value;
				NotifyValueChanged ("IsExpanded", isExpanded);
			}
		}

		public string FrontGroupBoxCaption =>
			FrontAndBackDifferent ? "Front Stencil State" : "Font and Back Stencil States";

		bool frontAndBackDifferent;
		//front and back stencil state idem
		public bool FrontAndBackDifferent {
			get => frontAndBackDifferent;
			set {
				if (frontAndBackDifferent == value)
					return;
			 	frontAndBackDifferent = value;
				NotifyValueChanged ("FrontAndBackDifferent", frontAndBackDifferent);
				NotifyValueChanged ("FrontGroupBoxCaption", FrontGroupBoxCaption);
			}
		}
		public VkStencilOpState FrontStencil {
			get => depthStencilState.front;
			set {
				depthStencilState.front = value;
				if (!frontAndBackDifferent)
					BackStencil = value;
				NotifyValueChanged ("FrontStencil", depthStencilState.front);
			}
		}
		public VkStencilOpState BackStencil {
			get => depthStencilState.back;
			set {
				depthStencilState.back = value;
				NotifyValueChanged ("BackStencil", depthStencilState.back);
			}
		}

		#region BlendAttachments
		public ObservableList<VkViewport> ObsViewports
			=>Viewports as ObservableList<VkViewport>;
		public ObservableList<VkRect2D> ObsScissors
			=> Scissors as ObservableList<VkRect2D>;
		public ObservableList<VkPipelineColorBlendAttachmentState> ObsBlendAttachments
			=> blendAttachments as ObservableList<VkPipelineColorBlendAttachmentState>;
		public ObservableList<VkDynamicState> ObsDynamicStates
			=> dynamicStates as ObservableList<VkDynamicState>;
		public ObservableList<VkVertexInputBindingDescription> ObsVertexBindings
			=> vertexBindings as ObservableList<VkVertexInputBindingDescription>;
		public ObservableList<VkVertexInputAttributeDescription> ObsVertexAttributes
			=> vertexAttributes as ObservableList<VkVertexInputAttributeDescription>;
		public ObservableList<ShaderInfo> ObsShaders
			=> Shaders as ObservableList<ShaderInfo>;

		public void onEditBlendAttachmentClick (object sender, MouseEventArgs e)
		{
			/*Widget w = VkWindow.CurWin.loadWindow ("ui/editBlendAttachment.crow", this);
			w.FindByName ("OkButton").MouseClick += (s, ev) => ObsBlendAttachments.RaiseEdit (); */
		}
		public void onEditVertexAttributesClick (object sender, MouseEventArgs e)
		{
			/*Widget w = VkCrowWindow.CurWin.loadWindow ("ui/editVertexAttributes.crow", this);
			w.FindByName ("OkButton").MouseClick += (s, ev) => ObsVertexAttributes.RaiseEdit ();*/
		}

		#endregion


	}
}
