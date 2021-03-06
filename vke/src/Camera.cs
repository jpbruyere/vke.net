﻿// Copyright (c) 2019  Jean-Philippe Bruyère <jp_bruyere@hotmail.com>
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
			get { return aspectRatio; }
			set {
				aspectRatio = value;
				Update ();
			}
		}
		public float FieldOfView {
			get { return fov; }
			set {
				fov = value;
				Update ();
			}
		}
		public Matrix4x4 Perspective {
			get { return Matrix4x4.CreatePerspectiveFieldOfView (fov, aspectRatio, zNear, zFar); }
		}

		public Camera (float fieldOfView, float aspectRatio, float nearPlane = 0.1f, float farPlane = 16f) {
			fov = fieldOfView;
			this.aspectRatio = aspectRatio;
			zNear = nearPlane;
			zFar = farPlane;
			Update ();
		}

		public void Rotate (float x, float y, float z = 0) {
			rotation.X += rotSpeed * x;
			rotation.Y += rotSpeed * y;
			rotation.Z += rotSpeed * z;
			Update ();
		}
		public float Zoom {
			get { return zoom; }
			set {
				zoom = value;
				Update ();
			}
		}
		public void SetRotation (float x, float y, float z = 0) {
			rotation.X = x;
			rotation.Y = y;
			rotation.Z = z;
			Update ();
		}
		public void SetPosition (float x, float y, float z = 0) {
			position.X = x;
			position.Y = y;
			position.Z = z;
			Update ();
		}
		public void Move (float x, float y, float z = 0) {
			position.X += moveSpeed * x;
			position.Y += moveSpeed * y;
			position.Z += moveSpeed * z;
			Update ();
		}
		public void SetZoom (float factor) {
			zoom += zoomSpeed * factor;
			Update ();
		}

		public Matrix4x4 Projection { get; private set;}
		public Matrix4x4 View { get; private set;}
		public Matrix4x4 Model {
			get { return model; }
			set {
				model = value;
				Update ();
			} 
		}

		public Matrix4x4 SkyboxView {
			get { 
				return
					Matrix4x4.CreateFromAxisAngle (Vector3.UnitZ, rotation.Z) *
					Matrix4x4.CreateFromAxisAngle (Vector3.UnitY, rotation.Y) *
					Matrix4x4.CreateFromAxisAngle (Vector3.UnitX, rotation.X);
			}
		}

		public void Update () {
			Projection =  Vulkan.Utils.CreatePerspectiveFieldOfView (fov, aspectRatio, zNear, zFar);

			Matrix4x4 translation = Matrix4x4.CreateTranslation (position * zoom);// * new Vector3(1,1,-1)) ;
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
