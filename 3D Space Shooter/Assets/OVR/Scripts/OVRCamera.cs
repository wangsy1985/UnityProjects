/************************************************************************************

Filename    :   OVRCamera.cs
Content     :   Interface to camera class
Created     :   January 8, 2013
Authors     :   Peter Giokaris

Copyright   :   Copyright 2013 Oculus VR, Inc. All Rights reserved.

Use of this software is subject to the terms of the Oculus LLC license
agreement provided at the time of installation or download, or which
otherwise accompanies this software in either electronic or hard copy form.

************************************************************************************/

using UnityEngine;
using System.Runtime.InteropServices;

[RequireComponent(typeof(Camera))]

/// <summary>
/// OVRCamera is used to render into a Unity Camera class. 
/// This component handles reading the Rift tracker and positioning the camera position
/// and rotation. It also is responsible for properly rendering the final output, which
/// also the final lens correction pass.
/// </summary>
public class OVRCamera : OVRComponent
{
    #region Member Variables
    // PRIVATE MEMBERS
#if (!UNITY_ANDROID || UNITY_EDITOR) // UnityJohn
	// If CameraTextureScale is not 1.0f, we will render to this texture 
	private RenderTexture	CameraTexture	  	= null;

	// Scaled size of final render buffer
	// A value of 1 will not create a render buffer but will render directly to final
	// backbuffer
#if (UNITY_EDITOR || !UNITY_ANDROID) 
 	private float			CameraTextureScale 	= 1.0f;
#else
	// Setting this to lower the 1.0 so that we can achieve a higher frame rate on Android
 	private float			CameraTextureScale 	= 1.0f;
#endif

	// DistortionMesh is faster then pixel shader distortion method
	private OVRDistortionMesh eyeMesh = new OVRDistortionMesh();
#endif
    // We will search for camera controller and set it here for access to its members
    private OVRCameraController CameraController = null;

    // PUBLIC MEMBERS
	// camera position,	from root of camera to neck (translation only)
    [HideInInspector]
    public Vector3 NeckPosition = new Vector3(0.0f, 0.0f, 0.0f);
    // From neck to eye (rotation and translation; x will be different for each eye)
    [HideInInspector]
    public Vector3 EyePosition = new Vector3(0.0f, 0.09f, 0.16f);

	// STATIC MEMBERS
#if (!UNITY_ANDROID || UNITY_EDITOR) // UnityJohn
	// We will grab the actual orientation that is used by the cameras in a shared location.
	// This will allow multiple OVRCameraControllers to eventually be used in a scene, and 
	// only one orientation will be used to syncronize all camera orientation
	static private Quaternion CameraOrientation = Quaternion.identity;

	// Blit material, used for drawing quads on-screen
	static private Material 		BlitMaterial   		= null;
	// Color only material, used for drawing quads on-screen
	static private Material 		ColorOnlyMaterial   = null;

	static private Color			QuadColor 			= Color.red;
#endif
    #endregion

    #region Monobehaviour Member Functions
#if (!UNITY_ANDROID || UNITY_EDITOR) // UnityJohn
	/// <summary>
	/// Awake
	/// </summary>
	new void Awake()
	{
		base.Awake ();
				
		// Material used to blit from one render texture to another
		if(BlitMaterial == null)
		{
			BlitMaterial = new Material (
				"Shader \"BlitCopy\" {\n" +
				"	SubShader { Pass {\n" +
				" 		ZTest Off Cull Off ZWrite Off Fog { Mode Off }\n" +
				"		SetTexture [_MainTex] { combine texture}"	+
				"	}}\n" +
				"Fallback Off }"
			);
		}
		
		// Material used for drawing color only polys into a render texture
		// Used by Latency tester
		if(ColorOnlyMaterial == null)
		{
			ColorOnlyMaterial = new Material (

			    "Shader \"Solid Color\" {\n" +
    			"Properties {\n" +
                "_Color (\"Color\", Color) = (1,1,1)\n" +
                "}\n" +
    			"SubShader {\n" +
    			"Color [_Color]\n" +
    			"Pass {}\n" +
    			"}\n" +
    			"}"		
			);
		}
	}
#endif

	/// <summary>
	/// Start
	/// </summary>
    new void Start()
    {
        base.Start();

        // Get the OVRCameraController
#if (!UNITY_ANDROID || UNITY_EDITOR) // UnityJohn
		CameraController = gameObject.transform.parent.GetComponent<OVRCameraController>();
#else
        CameraController = transform.parent.GetComponent<OVRCameraController>();
#endif
        if (CameraController == null)
            Debug.LogWarning("WARNING: OVRCameraController not found!");

#if (!UNITY_ANDROID || UNITY_EDITOR) // UnityJohn
		// NOTE: MSAA TEXTURES NOT AVAILABLE YET
		// Set CameraTextureScale (increases the size of the texture we are rendering into
		// for a better pixel match when post processing the image through lens distortion)
		// If CameraTextureScale is not 1.0f, create a new texture and assign to target texture
		// Otherwise, fall back to normal camera rendering
		if((CameraTexture == null) && (CameraTextureScale != 1.0f))
		{
			int w = (int)(Screen.width / 2.0f * CameraTextureScale);
			int h = (int)(Screen.height * CameraTextureScale);
			CameraTexture = new RenderTexture(  w, h, 16);		
			
#if (UNITY_EDITOR || !UNITY_ANDROID) 
 			CameraTexture.antiAliasing = QualitySettings.antiAliasing;
#else
			CameraTexture.antiAliasing = 1;
#endif
		}
		
		
#endif
    }

#if (!UNITY_ANDROID || UNITY_EDITOR) // UnityJohn
	// Update
	new void Update()
	{
		base.Update ();
	}

	/// <summary>
	/// Raises the pre cull event.
	/// </summary>
	void OnPreCull()
	{
		// NOTE: Setting the camera here increases latency, but ensures
		// that all Unity sub-systems that rely on camera location before
		// being set to render are satisfied. 
		if(CameraController.CallInPreRender == false)
			SetCameraOrientation();
	}
#endif

	/// <summary>
	/// Raises the pre render event.
	/// </summary>
    void OnPreRender()
    {
#if (!UNITY_ANDROID || UNITY_EDITOR) // UnityJohn
		// NOTE: Better latency performance here, but messes up water rendering and other
		// systems that rely on the camera to be set before PreCull takes place.
		if(CameraController.CallInPreRender == true)
			SetCameraOrientation();
#endif

        if (CameraController.WireMode == true)
            GL.wireframe = true;

#if (!UNITY_ANDROID || UNITY_EDITOR) // UnityJohn
		// Set new buffers and clear color and depth
		if(CameraTexture != null)
		{
			CameraTexture.DiscardContents();
			Graphics.SetRenderTarget(CameraTexture);
			GL.Clear (true, true, gameObject.camera.backgroundColor);
		}
#endif
    }

#if UNITY_ANDROID // UnityJohn
    [DllImport("OculusPlugin")]
    private static extern void OVR_CameraPostRender(int eyeNum);
#endif

	/// <summary>
	/// Raises the post render event.
	/// </summary>
    // UnityJohn:
    // The FBO will be current in OnPostRender, so the plugin can
    // query OpenGL to find what texture is being rendered to.
    //
    // The FBO is NOT current in OnRenderImage, so don't try to put this work there.
    void OnPostRender()
    {
        if (CameraController.WireMode == true)
            GL.wireframe = false;

#if (UNITY_ANDROID && !UNITY_EDITOR) // UnityJohn
//		Debug.Log( "OnPostRender" );
		// This needs to happen from the render thread.
		CameraController.EnsureOvrInitialized();

        // The depth == 1 and 2 cameras will draw the eye views
        //
        // This should be called while the renderTexture is bound, so
        // we can have native code query GL for the actual identifiers
        // to allow us to hijack them.
        //
        // We can also draw vignette's, calibration grids, etc into the render
        // texture now, and issue a flush to get everything rendering so we
        // can overlap with the next eye render.
        //
        // Unfortunately, if anything else draws after this OnPostRender,
        // it would cause a re-render 
		if ( true )
		{
			GL.IssuePluginEvent( (int)Camera.current.depth );
		}
		else
		{
			OVR_CameraPostRender((int)Camera.current.depth - 1);
		}
#endif
    }

#if (!UNITY_ANDROID || UNITY_EDITOR) // UnityJohn
	// OnRenderImage
	void  OnRenderImage (RenderTexture source, RenderTexture destination)
	{	
		// Use either source input or CameraTexutre, if it exists
		RenderTexture SourceTexture = source;
		
		if (CameraTexture != null)
			SourceTexture = CameraTexture;
				
		// Replace null material with lens correction material
		Material material = null;
		
		if(CameraController.LensCorrection == true)
		{
			if(CameraController.MeshDistortion)
			{
				material = GetComponent<OVRLensCorrection>().GetMaterial_MeshDistort(CameraController.PortraitMode);				
			}
			else
			{
				if(CameraController.Chromatic == true)
					material = GetComponent<OVRLensCorrection>().GetMaterial_CA(CameraController.PortraitMode);
				else
					material = GetComponent<OVRLensCorrection>().GetMaterial(CameraController.PortraitMode);
			}
		}
		
		if(CameraController.MeshDistortion)
		{
			// Make the destination texture the target for all rendering
			RenderTexture.active = destination;
			
			// Assign the source texture to a property from a shader
			material.mainTexture = SourceTexture;
			
			if(CameraController.ShowDistortionWire)
				GL.wireframe = true;
			
			// Set up the simple Matrix
			GL.PushMatrix ();
			GL.LoadOrtho ();
			for(int i = 0; i < material.passCount; i++)
			{
				material.SetPass(i);
				// Render with distortion
				eyeMesh.DrawMesh();
			}
			GL.PopMatrix ();
						
			if(CameraController.ShowDistortionWire)
				GL.wireframe = false;
		}
		else
		{
			if(material!= null)
			{
				// Render with distortion
				Graphics.Blit(SourceTexture, destination, material);
			}
			else
			{
				// Pass through
				Graphics.Blit(SourceTexture, destination);	
			}
		}
				
		// Run latency test by drawing out quads to the destination buffer
		LatencyTest(destination);
	}
#endif

    #endregion

#if (!UNITY_ANDROID || UNITY_EDITOR) // UnityJohn
	#region OVRCamera Functions

	/// <summary>
	/// Sets the camera orientation.
	/// </summary>
	void SetCameraOrientation()
	{
		Quaternion q   = Quaternion.identity;
		Vector3    dir = Vector3.forward;		
		
		// Main camera has a depth of 0, so it will be rendered first
		if(gameObject.camera.depth == 0.0f)
		{			
			// If desired, update parent transform y rotation here
			// This is useful if we want to track the current location of
			// of the head.
			// TODO: Future support for x and z, and possibly change to a quaternion
			// NOTE: This calculation is one frame behind 
			if(CameraController.TrackerRotatesY == true)
			{
				Vector3 a = gameObject.camera.transform.rotation.eulerAngles;
				a.x = 0; 
				a.z = 0;
				gameObject.transform.parent.transform.eulerAngles = a;
			}
			/*
			else
			{
				// We will still rotate the CameraController in the y axis
				// based on the fact that we have a Y rotation being passed 
				// in from above that still needs to take place (this functionality
				// may be better suited to be calculated one level up)
				Vector3 a = Vector3.zero;
				float y = 0.0f;
				CameraController.GetYRotation(ref y);
				a.y = y;
				gameObject.transform.parent.transform.eulerAngles = a;
			}
			*/	
			// Read shared data from CameraController	
			if(CameraController != null)
			{				
				// Read sensor here (prediction on or off)
				if(CameraController.PredictionOn == false)
					OVRDevice.GetOrientation(0, ref CameraOrientation);
				else
					OVRDevice.GetPredictedOrientation(0, ref CameraOrientation);				
			}
			
			// This needs to go as close to reading Rift orientation inputs
			OVRDevice.ProcessLatencyInputs();			
		}
		
		// Calculate the rotation Y offset that is getting updated externally
		// (i.e. like a controller rotation)
		float yRotation = 0.0f;
		CameraController.GetYRotation(ref yRotation);
		q = Quaternion.Euler(0.0f, yRotation, 0.0f);
		dir = q * Vector3.forward;
		q.SetLookRotation(dir, Vector3.up);
	
		// Multiply the camera controllers offset orientation (allow follow of orientation offset)
		Quaternion orientationOffset = Quaternion.identity;
		CameraController.GetOrientationOffset(ref orientationOffset);
		q = orientationOffset * q;
		
		// Multiply in the current HeadQuat (q is now the latest best rotation)
		if(CameraController != null)
			q = q * CameraOrientation;
		
		// * * *
		// Update camera rotation
		gameObject.camera.transform.rotation = q;
		
		// * * *
		// Update camera position (first add Offset to parent transform)
		gameObject.camera.transform.position = 
		gameObject.camera.transform.parent.transform.position + NeckPosition;
	
		// Adjust neck by taking eye position and transforming through q
		gameObject.camera.transform.position += q * EyePosition;		
	}

	// LatencyTest
	void LatencyTest(RenderTexture dest)
	{
		byte r = 0,g = 0, b = 0;
		
		// See if we get a string back to send to the debug out
		string s = Marshal.PtrToStringAnsi(OVRDevice.GetLatencyResultsString());
		if (s != null)
		{
			string result = 
			"\n\n---------------------\nLATENCY TEST RESULTS:\n---------------------\n";
			result += s;
			result += "\n\n\n";
			print(result);
		}
		
		if(OVRDevice.DisplayLatencyScreenColor(ref r, ref g, ref b) == false)
			return;
		
		RenderTexture.active = dest;  		
		Material material = ColorOnlyMaterial;
		QuadColor.r = (float)r / 255.0f;
		QuadColor.g = (float)g / 255.0f;
		QuadColor.b = (float)b / 255.0f;
		material.SetColor("_Color", QuadColor);
		GL.PushMatrix();
    	material.SetPass(0);
    	GL.LoadOrtho();
    	GL.Begin(GL.QUADS);
    	GL.Vertex3(0.3f,0.3f,0);
    	GL.Vertex3(0.3f,0.7f,0);
    	GL.Vertex3(0.7f,0.7f,0);
    	GL.Vertex3(0.7f,0.3f,0);
    	GL.End();
    	GL.PopMatrix();
		
	}
		
	// UpdateDistortionMeshParams
	public void UpdateDistortionMeshParams (OVRLensCorrection lc)
	{
		eyeMesh.UpdateParams(lc);
	}

	// Blit - Copies one render texture onto another through a material
	// flip will flip the render horizontally
	void Blit (RenderTexture source, RenderTexture dest, Material m, bool flip) 
	{
		Material material = m;
		
		// Default to blitting material if one doesn't get passed in
		if(material == null)
			material = BlitMaterial;
		
		// Make the destination texture the target for all rendering
		RenderTexture.active = dest;
		
		// Assign the source texture to a property from a shader
		source.SetGlobalShaderProperty ("_MainTex");	
		
		// Set up the simple Matrix
		GL.PushMatrix ();
		GL.LoadOrtho ();
		for(int i = 0; i < material.passCount; i++)
		{
			material.SetPass(i);
			DrawQuad(flip);
		}
		GL.PopMatrix ();
	}
	
	// DrawQuad
	void DrawQuad(bool flip)
	{
		GL.Begin (GL.QUADS);
	
		if(flip == true)
		{
			GL.TexCoord2( 0.0f, 1.0f ); GL.Vertex3( 0.0f, 0.0f, 0.1f );
			GL.TexCoord2( 1.0f, 1.0f ); GL.Vertex3( 1.0f, 0.0f, 0.1f );
			GL.TexCoord2( 1.0f, 0.0f ); GL.Vertex3( 1.0f, 1.0f, 0.1f );
			GL.TexCoord2( 0.0f, 0.0f ); GL.Vertex3( 0.0f, 1.0f, 0.1f );
		}
		else
		{
			GL.TexCoord2( 0.0f, 0.0f ); GL.Vertex3( 0.0f, 0.0f, 0.1f );
			GL.TexCoord2( 1.0f, 0.0f ); GL.Vertex3( 1.0f, 0.0f, 0.1f );
			GL.TexCoord2( 1.0f, 1.0f ); GL.Vertex3( 1.0f, 1.0f, 0.1f );
			GL.TexCoord2( 0.0f, 1.0f ); GL.Vertex3( 0.0f, 1.0f, 0.1f );
		}
		
		GL.End();
	}
	
	///////////////////////////////////////////////////////////
	// PUBLIC FUNCTIONS
	///////////////////////////////////////////////////////////
		
	// SetPerspectiveOffset
	public void SetPerspectiveOffset(ref Vector3 offset)
	{
		// NOTE: Unity skyboxes do not currently use the projection matrix, so
		// if one wants to use a skybox with the Rift it must be implemented 
		// manually		
		gameObject.camera.ResetProjectionMatrix();
		Matrix4x4 om = Matrix4x4.identity;
    	om.SetColumn (3, new Vector4 (offset.x, offset.y, 0.0f, 1));

		// Rotate -90.0 for portrait mode
		if(CameraController != null && CameraController.PortraitMode == true)
		{
			// Create a rotation matrix
			Vector3 t    = Vector3.zero;
			Quaternion r = Quaternion.Euler(0.0f, 0.0f, -90.0f);
			Vector3 s    = Vector3.one;
    		Matrix4x4 pm = Matrix4x4.TRS(t, r, s);
			
			gameObject.camera.projectionMatrix = pm * om * gameObject.camera.projectionMatrix;
		}
		else
		{
			gameObject.camera.projectionMatrix = om * gameObject.camera.projectionMatrix;
		}
		
	}
	
	// DrawGrid
	static public void DrawGrid(RenderTexture dest, int x, int y, Color color)
	{
		RenderTexture.active = dest;  		
		Material material = ColorOnlyMaterial;
		material.SetColor("_Color", color);
		GL.PushMatrix();
    	material.SetPass(0);
    	GL.LoadOrtho();
    	GL.Begin(GL.LINES);
  		// X
		float xStart = 0.0f;
		float xd = 1.0f / (float)x;
		for (int i = 0; i <= x; i++)
		{
			GL.Vertex3(xStart,0,0);
			GL.Vertex3(xStart,1,0);
			xStart+=xd;
		}
		 // Y
		float yStart = 0.0f;
		float yd = 1.0f / (float)y;
		for (int i = 0; i <= y; i++)
		{
			GL.Vertex3(0,yStart,0);
			GL.Vertex3(1,yStart,0);
			yStart+=yd;
		}
    	GL.End();
    	GL.PopMatrix();
	}
	#endregion
#endif

}

