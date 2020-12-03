using System.IO;
using System.Linq;

namespace vke.samples {
	public static class Utils {
		static string dataDir = "../../../datas/".Replace ('/', Path.DirectorySeparatorChar);
		public static string DataDirectory => dataDir;
		static string[] gltfExtensions = new[] { ".gltf", ".glb"};

		public static string[] GltfFiles =>
			Directory
				.GetFiles (Path.Combine (DataDirectory, "models"), "*", SearchOption.AllDirectories)
				.Where (file => gltfExtensions.Any (file.ToLower ().EndsWith))
				.ToArray ();

		public static string[] CubeMaps = {
			GetDataFile ("textures/papermill.ktx"),
			GetDataFile ("textures/cubemap_yokohama_bc3_unorm.ktx"),
			GetDataFile ("textures/gcanyon_cube.ktx"),
			GetDataFile ("textures/pisa_cube.ktx"),
			GetDataFile ("textures/uffizi_cube.ktx"),
		};

		public static string GetDataFile (string relativePath) =>
			Path.Combine (DataDirectory, relativePath);
	}
}
