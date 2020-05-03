layout (constant_id = 0) const float 	VoxelRadianceDataSize = 0.01;				// voxel half-extent in world space units
layout (constant_id = 1) const float	VoxelRadianceDataSize_rcp = 100.0;			// 1.0 / voxel-half extent
layout (constant_id = 2) const uint		VoxelRadianceDataRes = 256;					// voxel grid resolution
layout (constant_id = 3) const float	VoxelRadianceDataRes_rcp = 1.0 / 256.0;		// 1.0 / voxel grid resolution
layout (constant_id = 4) const uint		VoxelRadianceDataMIPs = 1;					// voxel grid mipmap count
layout (constant_id = 5) const uint		VoxelRadianceNumCones = 1;					// number of diffuse cones to trace
layout (constant_id = 6) const float	VoxelRadianceNumCones_rcp = 1;				// 1.0 / number of diffuse cones to trace
layout (constant_id = 7) const float	VoxelRadianceRayStepSize = 1;				// raymarch step size in voxel space units
//layout (constant_id = 8) const vec3		VoxelRadianceDataCenter = vec3(0);			// center of the voxel grid in world space units
const vec3 VoxelRadianceDataCenter = vec3(0.0);