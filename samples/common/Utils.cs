using System.IO;
using System.Linq;

namespace vke.samples {
	public static class Utils {
		public static string DataDirectory => "../../../datas/";
		static string[] gltfExtensions = new[] { ".gltf", ".glb"};

		public static string[] GltfFiles =>
			Directory
				.GetFiles (Path.Combine (DataDirectory, "models"), "*", SearchOption.AllDirectories)
				.Where (file => gltfExtensions.Any (file.ToLower ().EndsWith))
				.ToArray ();

	public static string[] CubeMaps = {
			DataDirectory + "textures/papermill.ktx",
			DataDirectory + "textures/cubemap_yokohama_bc3_unorm.ktx",
			DataDirectory + "textures/gcanyon_cube.ktx",
			DataDirectory + "textures/pisa_cube.ktx",
			DataDirectory + "textures/uffizi_cube.ktx",
		};
	}
}
