// Copyright (c) 2019-2021  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
//
// This code is licensed under the MIT license (MIT) (http://opensource.org/licenses/MIT)
using System.Numerics;

namespace vke {
	public class Camera {
		/// <summary>Corection matrix for vulkan projection</summary>
		public static readonly Matrix4x4 VKProjectionCorrection =
			new Matrix4x4 (
				1,  0,    0,    0,
				0, -1,    0,    0,
				0,  0, 1f/2, 	0,
				0,  0, 1f/2,    1
			);

		public enum CamType {LookAt, FirstPerson};

		float fov, aspectRatio, zNear = 0.1f, zFar = 128f, zoom = 1.0f;
		float moveSpeed = 0.1f, rotSpeed = 0.01f, zoomSpeed = 0.01f;

		Vector3 rotation = Vector3.Zero;
		Vector3 position = Vector3.Zero;
		Matrix4x4 model = Matrix4x4.Identity;

		public Vector3 Position => position;
		public Vector3 Rotation => rotation;
		public float NearPlane => zNear;
		public float FarPlane => zFar;

		public CamType Type;

		public float AspectRatio {
			get => aspectRatio;
			set {
				aspectRatio = value;
				update ();
			}
		}
		public float FieldOfView {
			get => fov;
			set {
				fov = value;
				update ();
			}
		}

		public Camera (float fieldOfView, float aspectRatio, float nearPlane = 0.1f, float farPlane = 16f) {
			fov = fieldOfView;
			this.aspectRatio = aspectRatio;
			zNear = nearPlane;
			zFar = farPlane;
			Model = Matrix4x4.Identity;
			update ();
		}
		/// <summary>
		/// Rotate the camera by an angle given in radian for each axes.
		/// </summary>
		/// <param name="x">rotation around the x axis</param>
		/// <param name="y">rotation around the y axis</param>
		/// <param name="z">rotation around the z axis</param>
		public void Rotate (float x, float y, float z = 0) {
			rotation.X += rotSpeed * x;
			rotation.Y += rotSpeed * y;
			rotation.Z += rotSpeed * z;
			update ();
		}
		public void Rotate (Vector3 angle) {
			rotation += rotSpeed * angle;
			update ();
		}
		public float Zoom {
			get => zoom;
			set {
				zoom = value;
				update ();
			}
		}
		/// <summary>
		/// Set the current rotation angle in radian around each axes
		/// </summary>
		/// <param name="x">current rotation around the x axis</param>
		/// <param name="y">current rotation around the y axis</param>
		/// <param name="z">current rotation around the z axis</param>
		public void SetRotation (float x, float y, float z = 0) {
			rotation.X = x;
			rotation.Y = y;
			rotation.Z = z;
			update ();
		}
		public void SetRotation (Vector3 newRotationVector) {
			rotation = newRotationVector;
			update ();
		}
		/// <summary>
		/// Set the current position of the camera.
		/// </summary>
		/// <param name="x">position on the x axis</param>
		/// <param name="y">position on the y axis</param>
		/// <param name="z">position on the z axis</param>
		public void SetPosition (float x, float y, float z = 0) {
			position.X = x;
			position.Y = y;
			position.Z = z;
			update ();
		}
		public void SetPosition (Vector3 newPosition) {
			position = newPosition;
			update ();
		}
		/// <summary>
		/// Move the camera by an amount given for each axis.
		/// </summary>
		/// <param name="x">displacement on the x axis</param>
		/// <param name="y">displacement on the y axis</param>
		/// <param name="z">displacement on the z axis</param>
		public void Move (float x, float y, float z = 0) {
			position.X += moveSpeed * x;
			position.Y += moveSpeed * y;
			position.Z += moveSpeed * z;
			update ();
		}
		public void Move (Vector3 displacementVector) {
			position += moveSpeed * displacementVector;
			update ();
		}
		public void SetZoom (float factor) {
			zoom += zoomSpeed * factor;
			update ();
		}
		/// <summary>
		/// The resulting projection matrix of the camera.
		/// Manual update may be triggered by calling the 'Update' method.
		/// </summary>
		public Matrix4x4 Projection { get; private set;}
		/// <summary>
		/// The resulting view matrix of the camera.
		/// Manual update may be triggered by calling the 'Update' method.
		/// </summary>
		public Matrix4x4 View { get; private set;}
		/// <summary>
		/// The model matrix. By default set to identity. It does not influence the
		/// view and proj matrices, it's only a convenient store place for the model matrix
		/// associated with this camera.
		/// </summary>
		public Matrix4x4 Model {
			get => model;
			set {
				model = value;
				update ();
			}
		}
		/// <summary>
		/// compute the skybox view matrix for this camera using it's rotation angles.
		/// </summary>
		public Matrix4x4 SkyboxView =>
					Matrix4x4.CreateFromAxisAngle (Vector3.UnitZ, rotation.Z) *
					Matrix4x4.CreateFromAxisAngle (Vector3.UnitY, rotation.Y) *
					Matrix4x4.CreateFromAxisAngle (Vector3.UnitX, rotation.X);
		/// <summary>
		/// Recompute the projection and the view matrices from the current parameters
		/// of this camera (fov, near, far, position, rotation, aspectRatio).
		/// After the call to this method, the projection and the view matrices will be
		/// in sync with the parameters. It's automatically called after rotation, move, etc...
		/// </summary>
		void update () {
			Projection =  Helpers.CreatePerspectiveFieldOfView (fov, aspectRatio, zNear, zFar);

			Matrix4x4 translation = Matrix4x4.CreateTranslation (position * zoom);
			if (Type == CamType.LookAt) {
				View =
						Matrix4x4.CreateFromAxisAngle (Vector3.UnitZ, rotation.Z) *
						Matrix4x4.CreateFromAxisAngle (Vector3.UnitY, rotation.Y) *
						Matrix4x4.CreateFromAxisAngle (Vector3.UnitX, rotation.X) *
						translation ;
			} else {
				View =	translation *
						Matrix4x4.CreateFromAxisAngle (Vector3.UnitX, rotation.X) *
						Matrix4x4.CreateFromAxisAngle (Vector3.UnitY, rotation.Y) *
						Matrix4x4.CreateFromAxisAngle (Vector3.UnitZ, rotation.Z);
			}
		}
	}
}
