// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;

namespace SpirVTasks {
	/// <summary>
	/// Record include position and path in produced glsl file before compilation
	/// </summary>
	internal class IncludePosition {
		public int line;		//include position in final glsl file
		public string path;		//path to the included file
	}

	public class IncludeFileNotFound : FileNotFoundException {
		public string SourceFile;
		public int SourceLine;

		public IncludeFileNotFound(string srcFileName, int srcLine, string includeFileName) :
			base ("include file not found", includeFileName) {

			SourceFile = srcFileName;
			SourceLine = srcLine;
		}
	}

	public class CompileGLSLTask : Microsoft.Build.Utilities.Task {

		[Required]
		public ITaskItem SourceFile {
			get;
			set;
		}
		[Required]
		public ITaskItem TempDirectory {
			get;
			set;
		}
		[Required]
		[Output]
		public ITaskItem DestinationFile {
			get;
			set;
		}

		public ITaskItem AdditionalIncludeDirectories {
			get;
			set;
		}
		/// <summary>
		/// Optional, set macros to be passed to the compiler, project 'DefineConstants' item is automatically added
		/// </summary>
		public ITaskItem[] DefineConstants {
			get;
			set;
		}
		public ITaskItem Optimisation {
			get;
			set;
		}
		/// <summary>
		/// Optional, Specify the comple glslc executable path.
		/// </summary>
		public ITaskItem SpirVCompilerPath {
			get;
			set;
		}

		volatile bool success;
		//due to includes mechanic, file inclusion position has to be recorded
		int currentCompiledLine = -1;//current line produced of the unified glsl file with all includes
		List<IncludePosition> includesPositions = new List<IncludePosition>();	//each included file has en entry in this list
																				//to emit correct error location for logger.

		bool tryFindInclude (string include, out string incFile) {
			if (!string.IsNullOrEmpty (AdditionalIncludeDirectories?.ItemSpec)) {
				foreach (string incDir in AdditionalIncludeDirectories.ItemSpec.Split (';', ',', '|')) {
					incFile = Path.Combine (incDir, include);
					if (File.Exists (incFile))
						return true;
				}
			}
			incFile = "";
			return false;
		}
		/// <summary>
		/// produce a single glsl file with main glsl and all its includes in the temp directory
		/// </summary>
		void concatenate_sources (string src, StreamWriter temp) {
			using (StreamReader sr = new StreamReader (File.OpenRead (src))) {
				int srcLine = 0;
				while (!sr.EndOfStream) {
					string line = sr.ReadLine ();
					if (line.Trim ().StartsWith ("#include", StringComparison.Ordinal)) {
						string include = line.Split ('"', '<', '>')[1];
						string incFile = Path.Combine (Path.GetDirectoryName (src), include);
						if (!File.Exists (incFile)) {
							if (!tryFindInclude(include, out incFile))
								throw new IncludeFileNotFound (src, srcLine, include);
						}
						//store position when entering an included file
						includesPositions.Add(new IncludePosition {
							line = currentCompiledLine,
							path = incFile
						});

						concatenate_sources (incFile, temp);

						//store current position when include parsing is finished
						includesPositions.Add(new IncludePosition {
							line = currentCompiledLine,
							path = src
						});
					} else
						temp.WriteLine (line);
					currentCompiledLine++;
					srcLine++;
				}
			}
		}

		/// <summary>
		/// Use the SpirVCompilerPath element if present. if not search 'VULKAN_SDK' environment, then PATH env variable.
		/// </summary>
		bool tryFindGlslcExecutable (out string glslcPath) {
			if (!string.IsNullOrEmpty (SpirVCompilerPath?.ItemSpec)) {
				glslcPath = SpirVCompilerPath.ItemSpec;
				if (!File.Exists (glslcPath))
					return false;
			}

			string glslcExec = "glslc";
			if (Environment.OSVersion.Platform.ToString ().StartsWith ("Win", StringComparison.Ordinal))
				glslcExec = glslcExec + "exe";

			glslcPath = Path.Combine (Environment.GetEnvironmentVariable ("VULKAN_SDK"), "bin");
			glslcPath = Path.Combine (glslcPath, glslcExec);
			if (File.Exists (glslcPath))
				return true;

			string envStrPathes = Environment.GetEnvironmentVariable ("PATH");
			if (!string.IsNullOrEmpty (envStrPathes)) {
				foreach (string path in envStrPathes.Split (';')) {
					glslcPath = Path.Combine (path, glslcExec);
					if (File.Exists (glslcPath))
						return true;
				}
			}
			return false;		
		}


		public override bool Execute () {

			success = true;

			includesPositions.Clear();
			currentCompiledLine = 0;

			if (!tryFindGlslcExecutable(out string glslcPath)) {
				BuildErrorEventArgs err = new BuildErrorEventArgs ("execute", "VK001", BuildEngine.ProjectFileOfTaskNode, 0, 0, 0, 0, $"glslc command not found: {glslcPath}", "Set 'VULKAN_SDK' environment variable", "SpirVTasks");
				BuildEngine.LogErrorEvent (err);
				return false;
			}

			string tempFile = Path.Combine (TempDirectory.ItemSpec, SourceFile.ItemSpec);
			if (File.Exists (tempFile))
				File.Delete (tempFile);
			try {
				Directory.CreateDirectory (Path.GetDirectoryName (tempFile));
				using (StreamWriter sw = new StreamWriter (File.OpenWrite(tempFile))) {
					string src = SourceFile.ItemSpec;
					concatenate_sources (SourceFile.ItemSpec, sw);
				}
			} catch (IncludeFileNotFound ex) {
				BuildErrorEventArgs err = new BuildErrorEventArgs ("include", "VK002", ex.SourceFile, ex.SourceLine, 0, 0, 0, $"include file not found: {ex.FileName}", "", "SpirVTasks");
				BuildEngine.LogErrorEvent (err);
				return false;
			}catch (Exception ex) {
				BuildErrorEventArgs err = new BuildErrorEventArgs ("include", "VK000", SourceFile.ItemSpec, 0, 0, 0, 0, ex.ToString(), "", "SpirVTasks");
				BuildEngine.LogErrorEvent (err);
				return false;
			}

			Directory.CreateDirectory (Path.GetDirectoryName (DestinationFile.ItemSpec));

			//build macros parameter
			StringBuilder macros = new StringBuilder ();
			if (DefineConstants != null) {
				for (int i = 0; i < DefineConstants.Length; i++) {
					if (!string.IsNullOrEmpty (DefineConstants[i]?.ItemSpec)) {
						foreach (string macro in DefineConstants [i].ItemSpec.Split (new char[]{ ';'}, StringSplitOptions.RemoveEmptyEntries))
							macros.Append ($"-D{macro} ");
					}
				}
			}
			string optimisationStr = "";
			if (!string.IsNullOrEmpty (Optimisation?.ItemSpec)) {
				if (string.Equals (Optimisation.ItemSpec, "perf", StringComparison.OrdinalIgnoreCase))
					optimisationStr = "-O";
				else if (string.Equals (Optimisation.ItemSpec, "size", StringComparison.OrdinalIgnoreCase))
					optimisationStr = "-Os";
				else if (string.Equals (Optimisation.ItemSpec, "none", StringComparison.OrdinalIgnoreCase))
					optimisationStr = "-O0";
			}else
				optimisationStr = "-O";

			Process glslc = new Process();
			//glslc.StartInfo.StandardOutputEncoding = System.Text.Encoding.ASCII;
			//glslc.StartInfo.StandardErrorEncoding = System.Text.Encoding.ASCII;
			glslc.StartInfo.UseShellExecute = false;
			glslc.StartInfo.RedirectStandardOutput = true;
			glslc.StartInfo.RedirectStandardError = true;
			glslc.StartInfo.FileName = glslcPath;
			glslc.StartInfo.Arguments = $"{tempFile} -o {DestinationFile.ItemSpec} {macros.ToString()} {optimisationStr}";
			glslc.StartInfo.CreateNoWindow = true;

			glslc.EnableRaisingEvents = true;
			glslc.OutputDataReceived += Glslc_OutputDataReceived;
			glslc.ErrorDataReceived += Glslc_ErrorDataReceived;

			Log.LogMessage (MessageImportance.High, $"-> glslc {glslc.StartInfo.Arguments}");

			glslc.Start ();

			glslc.BeginErrorReadLine ();
			glslc.BeginOutputReadLine ();

			DestinationFile.SetMetadata ("LogicalName", $"FromCS.{SourceFile.ItemSpec.Replace (Path.DirectorySeparatorChar, '.')}");

			glslc.WaitForExit ();

			return success;
		}

		void Glslc_ErrorDataReceived (object sender, DataReceivedEventArgs e) {
			if (e.Data == null)
				return;

			if (string.Equals (e.Data, "(0)", StringComparison.Ordinal))
				return;

			string[] tmp = e.Data.Split (':');

			Log.LogMessage (MessageImportance.High, $"glslc: {e.Data}");

			if (tmp.Length == 5) {
				string srcFile = SourceFile.ItemSpec;
				int line = Math.Max (0, int.Parse (tmp[1]));

				IncludePosition ip = includesPositions.LastOrDefault(p => p.line < line);
				if (ip != null) {
					line -= ip.line;
					srcFile = ip.path;
				}

				BuildErrorEventArgs err = new BuildErrorEventArgs ("compile", tmp[2], srcFile, line, 0, 0, 0, $"{tmp[3]} {tmp[4]}", "no help", "SpirVTasks");
				BuildEngine.LogErrorEvent (err);
				success = false;
			} else {
				Log.LogMessage (MessageImportance.High, $"{e.Data}");
			}
		}


		void Glslc_OutputDataReceived (object sender, DataReceivedEventArgs e) {
			if (e.Data == null)
				return;
			if (string.Equals (e.Data, "(0)", StringComparison.Ordinal))
				return;

			Log.LogMessage (MessageImportance.High, $"data:{e.Data}");

			BuildWarningEventArgs taskEvent = new BuildWarningEventArgs ("glslc", "0", BuildEngine.ProjectFileOfTaskNode, 0, 0, 0, 0, $"{e.Data}", "no help", "SpirVTasks");
			BuildEngine.LogWarningEvent (taskEvent);
		}

	}
}
