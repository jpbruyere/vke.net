//
//  Mesh.cs
//
//  Author:
//       Jean-Philippe Bruyère <jp.bruyere@hotmail.com>
//
//  Copyright (c) 2015 jp
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <http://www.gnu.org/licenses/>.
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading;

namespace vke
{
	public class ObjMesh {
		public Model.Vertex[] vertices;
		public ushort[] indices;

		public ObjMesh(string path) {
			OBJMeshLoader<ushort> loader = new OBJMeshLoader<ushort> (path);
			vertices = new Model.Vertex[loader.VertexCount];
			for (int i = 0; i < loader.VertexCount; i++) {
				vertices[i] = new Model.Vertex {
					pos = loader.lPositions[i],
					normal = loader.lNormals[i],
					uv = loader.lTexCoords[i]
				};
			}
			indices = loader.lIndices.ToArray ();
		}

		#region .OBJ Loading
		class OBJMeshLoader<TIdx> {
			public int VertexCount => objPositions.Count;

			public List<Vector3> objPositions = new List<Vector3> ();
			public List<Vector3> objNormals = new List<Vector3> ();
			public List<Vector2> objTexCoords = new List<Vector2> ();
			public List<Vector3> lPositions = new List<Vector3> ();
			public List<Vector3> lNormals = new List<Vector3> ();
			public List<Vector2> lTexCoords = new List<Vector2> ();
			public List<TIdx> lIndices = new List<TIdx> ();

			public OBJMeshLoader (string fileName) {
				string name = "unamed";
				using (Stream stream = new FileStream (fileName, FileMode.Open, FileAccess.Read)) {
					using (StreamReader Reader = new StreamReader (stream)) {
						System.Globalization.CultureInfo savedCulture = Thread.CurrentThread.CurrentCulture;
						Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

						string line;
						while ((line = Reader.ReadLine ()) != null) {
							line = line.Trim (' ');
							line = line.Replace ("  ", " ");

							string[] parameters = line.Split (' ');

							switch (parameters[0]) {
							case "o":
								name = parameters[1];
								break;
							case "p": // Point
								break;
							case "v": // Vertex
								float x = float.Parse (parameters[1]);
								float y = float.Parse (parameters[2]);
								float z = float.Parse (parameters[3]);

								objPositions.Add (new Vector3 (x, y, z));
								break;
							case "vt": // TexCoord
								float u = float.Parse (parameters[1]);
								float v = float.Parse (parameters[2]);
								objTexCoords.Add (new Vector2 (u, v));
								break;

							case "vn": // Normal
								float nx = float.Parse (parameters[1]);
								float ny = float.Parse (parameters[2]);
								float nz = float.Parse (parameters[3]);
								objNormals.Add (new Vector3 (nx, ny, nz));
								break;

							case "f":
								switch (parameters.Length) {
								case 4:

									lIndices.Add (ParseFaceParameter (parameters[1]));
									lIndices.Add (ParseFaceParameter (parameters[2]));
									lIndices.Add (ParseFaceParameter (parameters[3]));
									break;

								case 5:
									lIndices.Add (ParseFaceParameter (parameters[1]));
									lIndices.Add (ParseFaceParameter (parameters[2]));
									lIndices.Add (ParseFaceParameter (parameters[3]));
									lIndices.Add (ParseFaceParameter (parameters[4]));
									break;
								}
								break;
							}
						}
						Thread.CurrentThread.CurrentCulture = savedCulture;
					}
				}
			}

			TIdx ParseFaceParameter (string faceParameter) {
				Vector3 vertex = new Vector3 ();
				Vector2 texCoord = new Vector2 ();
				Vector3 normal = new Vector3 ();

				string[] parameters = faceParameter.Split ('/');

				int vertexIndex = int.Parse (parameters[0]);
				if (vertexIndex < 0) vertexIndex = objPositions.Count + vertexIndex;
				else vertexIndex = vertexIndex - 1;
				vertex = objPositions[vertexIndex];

				if (parameters.Length > 1) {
					int texCoordIndex;
					if (int.TryParse (parameters[1], out texCoordIndex)) {
						if (texCoordIndex < 0) texCoordIndex = objTexCoords.Count + texCoordIndex;
						else texCoordIndex = texCoordIndex - 1;
						texCoord = objTexCoords[texCoordIndex];
					}
				}

				if (parameters.Length > 2) {
					int normalIndex;
					if (int.TryParse (parameters[2], out normalIndex)) {
						if (normalIndex < 0) normalIndex = objNormals.Count + normalIndex;
						else normalIndex = normalIndex - 1;
						normal = objNormals[normalIndex];
					}
				}

				lPositions.Add (vertex);
				lTexCoords.Add (texCoord);
				lNormals.Add (normal);

				TIdx index = (TIdx)(object)(lPositions.Count - 1);
				return index;
			}
		}
		#endregion
	}
}

