/************************************************************************************

Filename    :   OVRDevice.cs
Content     :   Interface for the Oculus Rift Device
Created     :   February 14, 2013
Authors     :   Peter Giokaris

Copyright   :   Copyright 2013 Oculus VR, Inc. All Rights reserved.

Use of this software is subject to the terms of the Oculus LLC license
agreement provided at the time of installation or download, or which
otherwise accompanies this software in either electronic or hard copy form.

************************************************************************************/
using UnityEngine;
using System.Collections;

/// <summary>
/// OVR distortion mesh.
/// </summary>
public class OVRDistortionMesh
{		
	// Pow2 for the Morton order to work!
	// 4 is too low - it is easy to see the "wobbles" in the HMD.
	// 5 is realllly close on DK1, but you can see pixel differences when even/odd frames, so maybe not risk it.
	// 6 is indistinguishable on a monitor on even/odd frames. SOLD!
	const int MPP_GridSizeLog2 = 6;
	const int MPP_GridSize = 1 << MPP_GridSizeLog2;
	const int MPP_NumVertsPerEye = (MPP_GridSize+1)*(MPP_GridSize+1);
	const int MPP_NumTrisPerEye = (MPP_GridSize)*(MPP_GridSize)*2;
	
	private Mesh mesh;
	private Vector3[] positions;
	private Vector2[] uvs;
	private int[] triIndices;
			
	Vector2 _Center;
	Vector2 _ScaleIn;
    Vector2 _Scale;
    Vector4 _HmdWarpParam = new Vector4(1.0f, 0.05f, 0.115f, 0.0f);

	/// <summary>
	/// Updates the parameters.
	/// </summary>
	/// <param name="lc">Lc.</param>
	/// <param name="rightEye">If set to <c>true</c> right eye.</param>
	/// <param name="flipY">If set to <c>true</c> flip y.</param>
	public void UpdateParams (OVRLensCorrection lc)
	{
		_Center = lc._Center;
		_ScaleIn = lc._ScaleIn;
		_Scale = lc._Scale;
		_HmdWarpParam = lc._HmdWarpParam;
				
		GenerateMesh();
	}
	
    // Scales input texture coordinates for distortion.
    // ScaleIn maps texture coordinates to Scales to ([-1, 1] * scaleFactor),
    // where scaleFactor compensates input for K1 and K2, to allow full screen size to be used.
    // Scale factor that fits into screen size can be determined by solving this
    // equation for Scale: 1 = Scale * (K0 + K1 * Scale^2 + K2 * Scale^4).    
    Vector2 HmdWarp(ref Vector2 in01)
    {		
      	Vector2 vecFromCenter = (in01 - _Center).EntrywiseMultiply(_ScaleIn); // Scales to [-1, 1] 
      	float rSq = vecFromCenter.x * vecFromCenter.x + vecFromCenter.y * vecFromCenter.y;
      	Vector2 vecResult = vecFromCenter * (_HmdWarpParam.x + 
										     _HmdWarpParam.y * rSq + 
			  								 _HmdWarpParam.z * rSq * rSq +
											 _HmdWarpParam.w * rSq * rSq * rSq);
      	return _Center + _Scale.EntrywiseMultiply(vecResult);
    }
		
	public OVRDistortionMesh()
	{
	}

	/// <summary>
	/// Generates the mesh.
	/// This should only be done when fov changes.
	/// </summary>
	/// <param name="lc">Lc.</param>
	/// <param name="rightEye">If set to <c>true</c> right eye.</param>
	/// <param name="flipY">If set to <c>true</c> flip y.</param>
	public void GenerateMesh()
	{
		positions = new Vector3[MPP_NumVertsPerEye];
		uvs = new Vector2[MPP_NumVertsPerEye];
		triIndices = new int[MPP_NumTrisPerEye * 3];
		
		{
			int i = 0;
            for ( int y = 0; y <= MPP_GridSize; y++ )
            {
                for ( int x = 0; x <= MPP_GridSize; x++ )
                {
					Vector2 sourceCoordNDC;
                    sourceCoordNDC.x = ( (float)x / (float)MPP_GridSize );
                    sourceCoordNDC.y = ( (float)y / (float)MPP_GridSize );
					uvs[i] = HmdWarp(ref sourceCoordNDC);
					uvs[i].x = Mathf.Clamp(uvs[i].x, 0.0f, 1.0f);
					uvs[i].y = Mathf.Clamp(uvs[i].y, 0.0f, 1.0f);
					
					Vector2 newPos = sourceCoordNDC;
					newPos.x = newPos.x * 2.0f - 1.0f;
					// Flip y if we are using MSAA
					if(QualitySettings.antiAliasing < 2)
						newPos.y = newPos.y * 2.0f - 1.0f;
					else
						newPos.y = (1-newPos.y) * 2.0f - 1.0f;
					
					positions[i].x = newPos.x;
					positions[i].y = newPos.y;

					// 'positions z' will be set to modulate color to make edges black
					float vx = Mathf.Clamp(uvs[i].x, 0, 0.998f);
					float vy = Mathf.Clamp(uvs[i].y, 0, 0.998f);
					float fadeColorX = 1.0f;
					float fadeColorY = 1.0f;

					if ((vx >= 0.0f) && (vx < 0.1f))	
						fadeColorX = vx / 0.1f;
					if (vx > 0.9f) 	
						fadeColorX = 1.0f - ((vx - 0.9f)/0.1f);
					
					if ((vy >= 0.0f) && (vy < 0.1f))	
						fadeColorY = vy / 0.1f;
					if (vy > 0.9f) 	
						fadeColorY = 1.0f - ((vy - 0.9f)/0.1f);

					positions[i].z = (fadeColorX < fadeColorY) ? fadeColorX : fadeColorY;

                    i++;
                }
            }
			
			int triIndex = 0;
            for ( int triNum = 0; triNum < MPP_GridSize * MPP_GridSize; triNum++ )
            {
                // Use a Morton order to help locality of FB, texture and vertex cache.
                // (0.325ms raster order -> 0.257ms Morton order)
                //OVR_ASSERT ( MPP_GridSize <= 256 );
                int x = ( ( triNum & 0x0001 ) >> 0 ) |
                        ( ( triNum & 0x0004 ) >> 1 ) |
                        ( ( triNum & 0x0010 ) >> 2 ) |
                        ( ( triNum & 0x0040 ) >> 3 ) |
                        ( ( triNum & 0x0100 ) >> 4 ) |
                        ( ( triNum & 0x0400 ) >> 5 ) |
                        ( ( triNum & 0x1000 ) >> 6 ) |
                        ( ( triNum & 0x4000 ) >> 7 );
                int y = ( ( triNum & 0x0002 ) >> 1 ) |
                        ( ( triNum & 0x0008 ) >> 2 ) |
                        ( ( triNum & 0x0020 ) >> 3 ) |
                        ( ( triNum & 0x0080 ) >> 4 ) |
                        ( ( triNum & 0x0200 ) >> 5 ) |
                        ( ( triNum & 0x0800 ) >> 6 ) |
                        ( ( triNum & 0x2000 ) >> 7 ) |
                        ( ( triNum & 0x8000 ) >> 8 );
                int FirstVertex = x * (MPP_GridSize+1) + y;
                // Another twist - we want the top-left and bottom-right quadrants to
                // have the triangles split one way, the other two split the other.
                // +---+---+---+---+
                // |  /|  /|\  |\  |
                // | / | / | \ | \ |
                // |/  |/  |  \|  \|
                // +---+---+---+---+
                // |  /|  /|\  |\  |
                // | / | / | \ | \ |
                // |/  |/  |  \|  \|
                // +---+---+---+---+
                // |\  |\  |  /|  /|
                // | \ | \ | / | / |
                // |  \|  \|/  |/  |
                // +---+---+---+---+
                // |\  |\  |  /|  /|
                // | \ | \ | / | / |
                // |  \|  \|/  |/  |
                // +---+---+---+---+
                // This way triangle edges don't span long distances over the distortion function,
                // so linear interpolation works better & we can use fewer tris.
                if ( ( x < MPP_GridSize/2 ) != ( y < MPP_GridSize/2 ) )       // != is logical XOR
                {					
                    triIndices[triIndex++] = FirstVertex;
                    triIndices[triIndex++] = FirstVertex+1;
                    triIndices[triIndex++] = FirstVertex+(MPP_GridSize+1)+1;

                    triIndices[triIndex++] = FirstVertex+(MPP_GridSize+1)+1;
                    triIndices[triIndex++] = FirstVertex+(MPP_GridSize+1);
                    triIndices[triIndex++] = FirstVertex;
                }
                else
                {
                    triIndices[triIndex++] = FirstVertex;
                    triIndices[triIndex++] = FirstVertex+1;
                    triIndices[triIndex++] = FirstVertex+(MPP_GridSize+1);

                    triIndices[triIndex++] = FirstVertex+1;
                    triIndices[triIndex++] = FirstVertex+(MPP_GridSize+1)+1;
                    triIndices[triIndex++] = FirstVertex+(MPP_GridSize+1);
                }
            }
			
			mesh = new Mesh();
			mesh.vertices = positions;
			mesh.uv = uvs;
			mesh.triangles = triIndices;
        }
	}
	
	/// <summary>
	/// Draws the mesh.
	/// </summary>
	public void DrawMesh()
	{
		if(mesh != null)
		{
			Graphics.DrawMeshNow(mesh, Matrix4x4.identity);		
		}
	}
}

public static class VectorExtension
{
	// Entrywise product of two vectors
    public static Vector2 EntrywiseMultiply(this Vector2 a, Vector2 b)
	{
		return new Vector2(a.x * b.x, a.y * b.y);
	}
}