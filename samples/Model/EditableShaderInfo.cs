// Copyright (c) 2013-2020  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.IO;
using Crow;
using vke;
using Vulkan;

namespace Model {
	public class EditableShaderInfo : ShaderInfo, IValueChange {
		#region IValueChange implementation
		public event EventHandler<ValueChangeEventArgs> ValueChanged;
		public virtual void NotifyValueChanged (string MemberName, object _value) {
			ValueChanged?.Invoke (this, new ValueChangeEventArgs (MemberName, _value));
		}
		#endregion

		public event EventHandler ModuleChanged;

		string path;
		string origSource;
		string source;

		public string Source {
			get => source;
			set {
				if (source == value)
					return;
				source = value;

				NotifyValueChanged (nameof (IsDirty), IsDirty);
				NotifyValueChanged (nameof(Source), source);

				Compile ();
			}
		}

		string error;

		public bool IsDirty => source != origSource;
		public bool HasError => !string.IsNullOrEmpty (error);

		public string Error {
			get => error;
			set {
				if (error == value)
					return;
				error = value;
				NotifyValueChanged (nameof (Error), error);
				NotifyValueChanged ("HasError", HasError);
			}
		}

		public EditableShaderInfo (Device dev, string shaderPath, VkShaderStageFlags stageFlags, SpecializationInfo specializationInfo = null, string entryPoint = "main") :
			base (stageFlags, default, specializationInfo, entryPoint) {
			this.dev = dev;
			path = shaderPath;
			reloadFromDisk ();
		}

		void reloadFromDisk () {
			using (StreamReader sr = new StreamReader (path)) {
				origSource = sr.ReadToEnd ();
			}
			Source = origSource;
		}

		public bool Compile () {
			using (shaderc.Compiler comp = new shaderc.Compiler ()) {
				using (shaderc.Result res = comp.Compile (source, path, Utils.ShaderStageToShaderKind (Stage))) {
					if (res.Status == shaderc.Status.Success) {
						dev.DestroyShaderModule (info.module);
						info.module = dev.CreateShaderModule (res.CodePointer, (UIntPtr)res.CodeLength);
						Error = null;
						ModuleChanged.Raise (this, null);
						return true;
					}
					Error = res.ErrorMessage;
				}
			}
			return false;
		}
	}
}
