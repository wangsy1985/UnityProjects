/************************************************************************************

Filename    :   OVRCameraController.cs
Content     :   Camera controller interface. 
				This script is used to interface the OVR cameras.
Created     :   January 8, 2013
Authors     :   Peter Giokaris

Copyright   :   Copyright 2013 Oculus VR, Inc. All Rights reserved.

Use of this software is subject to the terms of the Oculus LLC license
agreement provided at the time of installation or download, or which
otherwise accompanies this software in either electronic or hard copy form.

************************************************************************************/
using UnityEngine;
using System.Collections.Generic;
using System.Runtime.InteropServices;		// JDC UnityJohn
using System;		// JDC UnityJohn

//-------------------------------------------------------------------------------------
// ***** OVRCameraController
/// <summary>
/// OVR camera controller.
/// OVRCameraController is a component that allows for easy handling of the lower level cameras.
/// It is the main interface between Unity and the cameras. 
/// This is attached to a prefab that makes it easy to add a Rift into a scene.
///
/// All camera control should be done through this component.
///
/// </summary>
public class OVRCameraController : OVRComponent
{
    // PRIVATE MEMBERS
	private bool   UpdateCamerasDirtyFlag = false;	
    private Camera CameraLeft, CameraRight = null;	
    private float IPD = 0.064f; 							// in millimeters
	private float  LensOffsetLeft, LensOffsetRight = 0.0f;  // normalized screen space
    private float VerticalFOV = 90.0f;	 					// in degrees
    private float AspectRatio = 1.0f;
    private float DistK0, DistK1, DistK2, DistK3 = 0.0f; 	// lens distortion parameters

#if UNITY_ANDROID // UnityJohn
    private Camera CameraView = null;	// JDC
	public bool	Monoscopic = false;		// if true, only render the left eye camera
#endif

	// Initial orientation of the camera, can be used to always set the 
	// zero orientation of the cameras to follow a set forward facing orientation.
    private Quaternion OrientationOffset = Quaternion.identity;
    // Set Y rotation here; this will offset the y rotation of the cameras. 
    private float YRotation = 0.0f;

    // Camera positioning:
    // CameraRootPosition will be used to calculate NeckPosition and Eye Height
    public Vector3 CameraRootPosition = new Vector3(0.0f, 1.0f, 0.0f);
    // From CameraRootPosition to neck
    public Vector3 NeckPosition = new Vector3(0.0f, 0.7f, 0.0f);
    // From neck to eye (rotation and translation; x will be different for each eye, based on IPD)
    public Vector3 EyeCenterPosition = new Vector3(0.0f, 0.15f, 0.09f);

#if UNITY_ANDROID // UnityJohn
    // This is returned by the time warp rendering, which samples the head tracker
    // right at vsync time, so moving objects will move consistently while head motion
    // is occuring.
    public Quaternion CameraOrientation = Quaternion.identity;
#endif

    // Use player eye height as set in the Rift config tool
    public bool UsePlayerEyeHeight = false;
    private bool PrevUsePlayerEyeHeight = false;
    // Set this transform with an object that the camera orientation should follow.
    // NOTE: Best not to set this with the OVRCameraController IF TrackerRotatesY is
    // on, since this will lead to uncertain output
    public Transform FollowOrientation = null;
    // Set to true if we want the rotation of the camera controller to be influenced by tracker
    public bool TrackerRotatesY = false;

    public bool PortraitMode = false; // We currently default to landscape mode for render
    private bool PrevPortraitMode = false;

    // Use this to turn on/off Prediction
    public bool PredictionOn = true;
    // Use this to decide where tracker sampling should take place
    // Setting to true allows for better latency, but some systems
    // (such as Pro water) will break
    public bool CallInPreRender = false;
    // Use this to turn on wire-mode
    public bool WireMode = false;
    // Turn lens distortion on/off; use Chromatic Aberration in lens distortion calculation
    public bool LensCorrection = true;
    public bool Chromatic = true;

#if (!UNITY_ANDROID || UNITY_EDITOR) // UnityJohn
	// Uses a pre-distorted mesh for distortion as opposed to a pixel shader based one
	public bool         MeshDistortion  = true;
	
	public bool			ShowDistortionWire = false;
	
	public Vector4		InvWarp = new Vector4(1.0f, 0.22f, 0.24f, 0.0f);
#endif

    // anmold add : rotatable camera
    public bool useTracker = true;

    // UNITY CAMERA FIELDS
    // Set the background color for both cameras
    public Color BackgroundColor = new Color(0.192f, 0.302f, 0.475f, 1.0f);
    // Set the near and far clip plane for both cameras
    public float NearClipPlane = 0.15f;
    public float FarClipPlane = 1000.0f;

    // * * * * * * * * * * * * *

#if (!UNITY_ANDROID || UNITY_EDITOR) // UnityJohn
	/// <summary>
	/// Awake this instance.
	/// </summary>
	new void Awake()
	{
		base.Awake();
		
		// Get the cameras
		Camera[] cameras = gameObject.GetComponentsInChildren<Camera>();
		
		for (int i = 0; i < cameras.Length; i++)
		{
			if(cameras[i].name == "CameraLeft")
				CameraLeft = cameras[i];
			
			if(cameras[i].name == "CameraRight")
				CameraRight = cameras[i];
		}
		
		if((CameraLeft == null) || (CameraRight == null))
			Debug.LogWarning("WARNING: Unity Cameras in OVRCameraController not found!");
	}

	/// <summary>
	/// Start this instance.
	/// </summary>
	new void Start()
	{
		base.Start();
		
		// Get the required Rift information needed to set cameras
		InitCameraControllerVariables();
		
		// Initialize the cameras
		UpdateCamerasDirtyFlag = true;
		UpdateCameras();
		
		SetMaximumVisualQuality();
		
	}

	/// <summary>
	/// Update this instance.
	/// </summary>
	new void Update()
	{
		base.Update();		
		UpdateCameras();
	}
#endif

	/// <summary>
	/// Inits the camera controller variables.
	/// Made public so that it can be called by classes that require information about the
	/// camera to be present when initing variables in 'Start'
	/// </summary>
    public void InitCameraControllerVariables()
    {
#if (!UNITY_ANDROID || UNITY_EDITOR) // UnityJohn
		// Get the IPD value (distance between eyes in meters)
		OVRDevice.GetIPD(ref IPD);

		// Get the values for both IPD and lens distortion correction shift. We don't normally
		// need to set the PhysicalLensOffset once it's been set here.
		OVRDevice.CalculatePhysicalLensOffsets(ref LensOffsetLeft, ref LensOffsetRight);
		
		// Using the calculated FOV, based on distortion parameters, yeilds the best results.
		// However, public functions will allow to override the FOV if desired
		VerticalFOV = OVRDevice.VerticalFOV();
		
		// Store aspect ratio as well
		AspectRatio = OVRDevice.CalculateAspectRatio();
		
		OVRDevice.GetDistortionCorrectionCoefficients(ref DistK0, ref DistK1, ref DistK2, ref DistK3);
		
		// Check to see if we should render in portrait mode
		if(PortraitMode != true)
			PortraitMode = OVRDevice.RenderPortraitMode();
		
		PrevPortraitMode = false;
		
		// Get our initial world orientation of the cameras from the scene (we can grab it from 
		// the set FollowOrientation object or this OVRCameraController gameObject)
		if(FollowOrientation != null)
			OrientationOffset = FollowOrientation.rotation;
		else
			OrientationOffset = transform.rotation;
#endif
    }

#if (!UNITY_ANDROID || UNITY_EDITOR) // UnityJohn
	/// <summary>
	/// Updates the cameras.
	/// </summary>
	void UpdateCameras()
	{
		// Values that influence the stereo camera orientation up and above the tracker
		if(FollowOrientation != null)
			OrientationOffset = FollowOrientation.rotation;
				
		// Handle Portrait Mode changes to cameras
		SetPortraitMode();
		
		// Handle positioning of eye height and other things here
		UpdatePlayerEyeHeight();
		
		// Handle all other camera updates here
		if(UpdateCamerasDirtyFlag == false)
			return;
		
		float distOffset = 0.5f + (LensOffsetRight * 0.5f);
		float perspOffset = LensOffsetRight;
		float eyePositionOffset = IPD * 0.5f;
		ConfigureCamera(ref CameraRight, distOffset, perspOffset, eyePositionOffset);
		
		distOffset = 0.5f + (LensOffsetLeft * 0.5f);
		perspOffset = LensOffsetLeft;
		eyePositionOffset = -IPD * 0.5f;
		ConfigureCamera(ref CameraLeft, distOffset, perspOffset, eyePositionOffset);
		
		
		UpdateCamerasDirtyFlag = false;
	}
#endif

#if (!UNITY_ANDROID || UNITY_EDITOR) // UnityJohn
	/// <summary>
	/// Configures the camera.
	/// </summary>
	/// <returns><c>true</c>, if camera was configured, <c>false</c> otherwise.</returns>
	/// <param name="camera">Camera.</param>
	/// <param name="eyePositionOffset">Eye position offset.</param>
	bool ConfigureCamera(ref Camera camera, float distOffset, float perspOffset, float eyePositionOffset)
	{
		Vector3 PerspOffset = Vector3.zero;
		Vector3 EyePosition = EyeCenterPosition;
				
		// Always set  camera fov and aspect ration
		camera.fieldOfView = VerticalFOV;
		camera.aspect = AspectRatio;
			
		// Centre of lens correction
		camera.GetComponent<OVRLensCorrection>()._Center.x = distOffset;
		ConfigureCameraLensCorrection(ref camera);

		// Perspective offset for image
		PerspOffset.x = perspOffset;
		camera.GetComponent<OVRCamera>().SetPerspectiveOffset(ref PerspOffset);
			
		// Set camera variables that pertain to the neck and eye position
		// NOTE: We will want to add a scale vlue here in the event that the player 
		// grows or shrinks in the world. This keeps head modelling behaviour
		// accurate
		camera.GetComponent<OVRCamera>().NeckPosition = NeckPosition;
		EyePosition.x = eyePositionOffset; 
		camera.GetComponent<OVRCamera>().EyePosition = EyePosition;		
					
		// Background color
		camera.backgroundColor = BackgroundColor;
		
		// Clip Planes
		camera.nearClipPlane = NearClipPlane;
		camera.farClipPlane = FarClipPlane;
			
		return true;
	}
#else
	/// <summary>
	/// Configures the camera.
	/// </summary>
	/// <returns><c>true</c>, if camera was configured, <c>false</c> otherwise.</returns>
	/// <param name="camera">Camera.</param>
    bool ConfigureCamera(ref Camera camera)
    {
        // If we don't clear the color buffer with a glClear, tiling GPUs
		// will be forced to do an "unresolve" and read back the color buffer information.
		// The clear is free on PowerVR, and possibly Mali, but it is a performance cost
		// on Adreno, and we would be better off if we had the ability to discard/invalidate
		// the color buffer instead of clearing.
//        camera.clearFlags = CameraClearFlags.Depth;
        camera.clearFlags = CameraClearFlags.SolidColor;

        // Clip Planes
        camera.nearClipPlane = NearClipPlane;
        camera.farClipPlane = FarClipPlane;

        // JDC: Render to a square target texture
//		camera.targetTexture = new RenderTexture(1024, 1024, 16);
		camera.targetTexture = new RenderTexture(768, 768, 16);
		camera.targetTexture.antiAliasing = 2;
        camera.fieldOfView = 90.0f;	// FIXME: query from native device
        camera.aspect = 1.0f;
        camera.rect = new Rect(0, 0, 1, 1);	// Does this matter when targetTexture is set?

        return true;
    }
#endif

#if (!UNITY_ANDROID || UNITY_EDITOR) // UnityJohn
	// SetCameraLensCorrection
	void ConfigureCameraLensCorrection(ref Camera camera)
	{
		// Get the distortion scale and aspect ratio to use when calculating distortion shader
		float distortionScale = 1.0f / OVRDevice.DistortionScale();
		float aspectRatio     = OVRDevice.CalculateAspectRatio();
		
		// These values are different in the SDK World Demo; Unity renders each camera to a buffer
		// that is normalized, so we will respect this rule when calculating the distortion inputs
		float NormalizedWidth  = 1.0f;
		float NormalizedHeight = 1.0f;
		
		OVRLensCorrection lc = camera.GetComponent<OVRLensCorrection>();
		
		lc._Scale.x     = (NormalizedWidth  / 2.0f) * distortionScale;
		lc._Scale.y     = (NormalizedHeight / 2.0f) * distortionScale * aspectRatio;
		lc._ScaleIn.x   = (2.0f / NormalizedWidth);
		lc._ScaleIn.y   = (2.0f / NormalizedHeight) / aspectRatio;
		lc._HmdWarpParam.x = DistK0;		
		lc._HmdWarpParam.y = DistK1;
		lc._HmdWarpParam.z = DistK2;
		lc._HmdWarpParam.w = DistK3;
		
		// Push params also into the mesh distortion instance (if there is one)
		camera.GetComponent<OVRCamera>().UpdateDistortionMeshParams(lc);
	}

	// SetPortraitMode
	void SetPortraitMode()
	{
		if(PortraitMode != PrevPortraitMode)
		{
			Rect r = new Rect(0,0,0,0);
			
			if(PortraitMode == true)
			{
				r.x 		= 0.0f;
				r.y 		= 0.5f;
				r.width 	= 1.0f;
				r.height 	= 0.5f;
				CameraLeft.rect = r;
				
				r.x 		= 0.0f;
				r.y 		= 0.0f;
				r.width 	= 1.0f;
				r.height 	= 0.499999f;
				CameraRight.rect = r;
			}
			else
			{
				r.x 		= 0.0f;
				r.y 		= 0.0f;
				r.width 	= 0.5f;
				r.height 	= 1.0f;
				CameraLeft.rect = r;
				
				r.x 		= 0.5f;
				r.y 		= 0.0f;
				r.width 	= 0.499999f;
				r.height 	= 1.0f;
				CameraRight.rect = r;
			}
		}
		
		PrevPortraitMode = PortraitMode;
	}
#endif

#if (UNITY_ANDROID && !UNITY_EDITOR) // UnityJohn
    [DllImport("OculusPlugin")]
    // We need to get our OS thread ID to set it to sched_fifo
    // the orientation to use to generate next frame.
    private static extern int OVR_GetTid();

	[DllImport("OculusPlugin")]
	private static extern int OVR_SetInitVariables(IntPtr activity, IntPtr vrActivityClass );
	

	[DllImport("OculusPlugin")]
	private static extern int OVR_Init(IntPtr activity, IntPtr vrActivityClass );

 	void runOnUiThread() {
		// The Vsync class uses Choreographer to make Vsync event callbacks, and must
		// be run on a thread with a looper already running.
		AndroidJavaClass javaVsyncClass = new AndroidJavaClass("com.oculusvr.vrlib.Vsync");
		javaVsyncClass.CallStatic( "start" );
    }	

	// Get this from Unity on startup so we can call Activity java functions
	AndroidJavaObject activity;
	AndroidJavaClass javaVrActivityClass;
#endif

#if (UNITY_ANDROID && !UNITY_EDITOR) // UnityJohn
	// With multithreaded rendering, this needs to be called from the render thread.
	bool	ovrInitialized = false;
	public void	EnsureOvrInitialized()
	{
		if ( ovrInitialized )
		{
			return;
		}
		ovrInitialized = true;
        // JDC: start up the java vsync callbacks and set SCHED_FIFO
#if UNITY_ANDROID && !UNITY_EDITOR		
    	AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
		javaVrActivityClass = new AndroidJavaClass("com.oculusvr.vrlib.VrActivity");
		activity.Call("runOnUiThread", new AndroidJavaRunnable(runOnUiThread));
		//OVR_Init ( activity.GetRawObject(), javaVrActivityClass.GetRawClass() );

		// Sets the script thread to SCHED_FIFO and prepares for the render thread init
		OVR_SetInitVariables ( activity.GetRawObject(), javaVrActivityClass.GetRawClass() );

		// This will trigger the init on the render thread
		GL.IssuePluginEvent( 0 );

// doing it in plugin now		int tid = OVR_GetTid();
//		javaVrActivityClass.CallStatic( "setSchedFifoStatic", activity, tid, 1 );
#endif
	}

    // Start
    new void Start()
    {
        base.Start();
		
        // Disable screen dimming
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        // JDC: add a third camera that distorts both eye textures to the screen
        gameObject.AddComponent("Camera");

        // Get the cameras
        Camera[] cameras = gameObject.GetComponentsInChildren<Camera>();

        for (int i = 0; i < cameras.Length; i++)
        {
            if (cameras[i].name == "CameraLeft")
                CameraLeft = cameras[i];
            else if (cameras[i].name == "CameraRight")
                CameraRight = cameras[i];
            else
                CameraView = cameras[i];	// JDC
        }

        if ((CameraLeft == null) || (CameraRight == null))
            Debug.LogWarning("WARNING: Unity Cameras in OVRCameraController not found!");

        CameraView.cullingMask = 0;	// cull everything, we will draw the warp in the plugin
        CameraView.clearFlags = CameraClearFlags.Nothing;
		
		// We will get best overlap of GPU drawing and CPU work if the view distortion
		// operation happens first, using the renders from the previous frame, allowing the
		// longer eye renders to overlap with the script and engine work for the following frame.
		//
		// Depth doesn't seem to apply between rendertarget and non-rendertarget cameras.
        CameraView.depth = 0;
        CameraView.targetTexture = new RenderTexture(1, 1, 0);
		
        CameraLeft.depth = 1;
        CameraRight.depth = 2;
		
        // Get the required Rift information needed to set cameras
        // Get the IPD value (distance between eyes in meters)
        OVRDevice.GetIPD(ref IPD);

        // Get our initial world orientation of the cameras from the scene (we can grab it from 
        // the set FollowOrientation object or this OVRCameraController gameObject)
        if (FollowOrientation != null)
            OrientationOffset = FollowOrientation.rotation;
        else
            OrientationOffset = transform.rotation;

        // Initialize the cameras
        ConfigureCamera(ref CameraRight);
        ConfigureCamera(ref CameraLeft);
		
		// If rendering monoscopic, disable the right camera -- we will use the left
		// camera for both eyes.
		if ( Monoscopic )
			CameraRight.enabled = false;
		
        // SetMaximumVisualQuality
        QualitySettings.softVegetation = true;
        QualitySettings.maxQueuedFrames = 1;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        QualitySettings.vSyncCount = 0;	// JDC: we sync in the time warp, so we don't want unity syncing elsewhere
    }

    // Update 
    new void Update()
    {
		// Samsung MWC demo
#if UNITY_ANDROID		
		if ( Input.GetKey( KeyCode.Escape ) || Input.GetKey ( KeyCode.Alpha1 ) )
		{
			Debug.Log("Escape key");
			javaVrActivityClass.CallStatic( "returnToLauncher", activity );
			Application.Quit();
		}
#endif

        //		Debug.Log("CameraController Update");

		base.Update();
        //!@#		UpdateCameras();
        // Values that influence the stereo camera orientation up and above the tracker
        //		if(FollowOrientation != null)
        //			OrientationOffset = FollowOrientation.rotation;

        // Handle positioning of eye height and other things here
        //		UpdatePlayerEyeHeight();		


        Quaternion q = Quaternion.identity;
        Vector3 dir = Vector3.forward;

        // Calculate the rotation Y offset that is getting updated externally
        // (i.e. like a controller rotation)
        float yRotation = 0.0f;
        GetYRotation(ref yRotation);
        q = Quaternion.Euler(0.0f, yRotation, 0.0f);
        dir = q * Vector3.forward;
        q.SetLookRotation(dir, Vector3.up);

        // Multiply the camera controllers offset orientation (allow follow of orientation offset)
        Quaternion orientationOffset = Quaternion.identity;
        GetOrientationOffset(ref orientationOffset);
        q = orientationOffset * q;

        // Multiply in the current HeadQuat (q is now the latest best rotation)
        q = q * CameraOrientation;
//        q = CameraOrientation;

        // If desired, update parent transform y rotation here
        // This is useful if we want to track the current location of
        // of the head.
        // TODO: Future support for x and z, and possibly change to a quaternion
        // NOTE: This calculation is one frame behind 
        //		if(TrackerRotatesY == true)
        //		{	
        //			Vector3 a = transform.rotation.eulerAngles;
        //			a.x = 0; 
        //			a.z = 0;
        //			transform.parent.transform.eulerAngles = a;
        //		}

        // * * *
        // Update camera rotation
		transform.rotation = q;
        CameraLeft.transform.rotation = q;
        CameraRight.transform.rotation = q;

        // * * *
        // Update camera position (first add Offset to parent transform)
        // Adjust neck by taking eye position and transforming through q
        CameraLeft.transform.position = 
			transform.position + NeckPosition
                + q * ( EyeCenterPosition + new Vector3( IPD * -0.5f, 0, 0 ) );

        CameraRight.transform.position = 
            transform.position + NeckPosition
                + q * ( EyeCenterPosition + new Vector3( IPD * 0.5f, 0, 0 ) );

    }

    [DllImport("OculusPlugin")]
    // Time warp renders the most recent camera views and returns
    // the orientation to use to generate next frame.
    private static extern bool OVR_TimeWarp( bool monoscopic,
                                                 ref float w,
                                                 ref float x,
                                                 ref float y,
                                                 ref float z);

    void OnRenderObject()
    {
        // JDC
        // The view camera will draw the actual screen by
        // warping the output from cameras 0 and 1 to the screen.
        if (Camera.current != CameraView)
        {
            return;
        }
		// When the render thread gets to this point, the time warp
		// code will draw the screen.
		GL.IssuePluginEvent( 3 );

		// Fetch the orientation returned by the last time warp
        float w = 0, x = 0, y = 0, z = 0;
#if UNITY_ANDROID && !UNITY_EDITOR
        OVR_TimeWarp( Monoscopic, ref w, ref x, ref y, ref z);
#endif
        // Change the co-ordinate system from right-handed to Unity left-handed
        CameraOrientation.w = w;
        CameraOrientation.x = -x;
        CameraOrientation.y = -y;
        CameraOrientation.z = z;
    }

	void	CreateTargetTextures( int resolution, int depth )
	{
	}
#endif

	/// <summary>
	/// Updates the height of the player eye.
	/// </summary>
    void UpdatePlayerEyeHeight()
    {
        if ((UsePlayerEyeHeight == true) && (PrevUsePlayerEyeHeight == false))
        {
            // Calculate neck position to use based on Player configuration
            float peh = 0.0f;

            if (OVRDevice.GetPlayerEyeHeight(ref peh) != false)
            {
                NeckPosition.y = peh - CameraRootPosition.y - EyeCenterPosition.y;
            }
        }

        PrevUsePlayerEyeHeight = UsePlayerEyeHeight;
    }

    ///////////////////////////////////////////////////////////
    // PUBLIC FUNCTIONS
    ///////////////////////////////////////////////////////////

	/// <summary>
	/// Sets the cameras - Should we want to re-target the cameras
	/// </summary>
	/// <param name="cameraLeft">Camera left.</param>
	/// <param name="cameraRight">Camera right.</param>
    public void SetCameras(ref Camera cameraLeft, ref Camera cameraRight)
    {
        CameraLeft = cameraLeft;
        CameraRight = cameraRight;
		UpdateCamerasDirtyFlag = true;
    }

	/// <summary>
	/// Gets the IPD.
	/// </summary>
	/// <param name="ipd">Ipd.</param>
    public void GetIPD(ref float ipd)
    {
        ipd = IPD;
    }
	/// <summary>
	/// Sets the IPD.
	/// </summary>
	/// <param name="ipd">Ipd.</param>
    public void SetIPD(float ipd)
    {
        IPD = ipd;
		UpdateCamerasDirtyFlag = true;
    }

	/// <summary>
	/// Gets the vertical FOV.
	/// </summary>
	/// <param name="verticalFOV">Vertical FO.</param>
    public void GetVerticalFOV(ref float verticalFOV)
    {
        verticalFOV = VerticalFOV;
    }
	/// <summary>
	/// Sets the vertical FOV.
	/// </summary>
	/// <param name="verticalFOV">Vertical FO.</param>
    public void SetVerticalFOV(float verticalFOV)
    {
        VerticalFOV = verticalFOV;
		UpdateCamerasDirtyFlag = true;
    }

	/// <summary>
	/// Gets the aspect ratio.
	/// </summary>
	/// <param name="aspecRatio">Aspec ratio.</param>
    public void GetAspectRatio(ref float aspecRatio)
    {
        aspecRatio = AspectRatio;
    }
	/// <summary>
	/// Sets the aspect ratio.
	/// </summary>
	/// <param name="aspectRatio">Aspect ratio.</param>
    public void SetAspectRatio(float aspectRatio)
    {
        AspectRatio = aspectRatio;
		UpdateCamerasDirtyFlag = true;
    }

    // Get/SetDistortionCoefs
    public void GetDistortionCoefs(ref float distK0,
                                   ref float distK1,
                                   ref float distK2,
                                   ref float distK3)
    {
        distK0 = DistK0;
        distK1 = DistK1;
        distK2 = DistK2;
        distK3 = DistK3;
    }
    public void SetDistortionCoefs(float distK0,
                                   float distK1,
                                   float distK2,
                                   float distK3)
    {
        DistK0 = distK0;
        DistK1 = distK1;
        DistK2 = distK2;
        DistK3 = distK3;
		UpdateCamerasDirtyFlag = true;
    }

	/// <summary>
	/// Gets the camera root position.
	/// </summary>
	/// <param name="cameraRootPosition">Camera root position.</param>
    public void GetCameraRootPosition(ref Vector3 cameraRootPosition)
    {
        cameraRootPosition = CameraRootPosition;
    }
	/// <summary>
	/// Sets the camera root position.
	/// </summary>
	/// <param name="cameraRootPosition">Camera root position.</param>
    public void SetCameraRootPosition(ref Vector3 cameraRootPosition)
    {
        CameraRootPosition = cameraRootPosition;
		UpdateCamerasDirtyFlag = true;
    }

	/// <summary>
	/// Gets the neck position.
	/// </summary>
	/// <param name="neckPosition">Neck position.</param>
    public void GetNeckPosition(ref Vector3 neckPosition)
    {
        neckPosition = NeckPosition;
    }
	/// <summary>
	/// Sets the neck position.
	/// </summary>
	/// <param name="neckPosition">Neck position.</param>
    public void SetNeckPosition(Vector3 neckPosition)
    {
        // This is locked to the NeckPosition that is set by the
        // Player profile.
        if (UsePlayerEyeHeight != true)
        {
            NeckPosition = neckPosition;
			UpdateCamerasDirtyFlag = true;
        }
    }

	/// <summary>
	/// Gets the eye center position.
	/// </summary>
	/// <param name="eyeCenterPosition">Eye center position.</param>
    public void GetEyeCenterPosition(ref Vector3 eyeCenterPosition)
    {
        eyeCenterPosition = EyeCenterPosition;
    }
	/// <summary>
	/// Sets the eye center position.
	/// </summary>
	/// <param name="eyeCenterPosition">Eye center position.</param>
    public void SetEyeCenterPosition(Vector3 eyeCenterPosition)
    {
        EyeCenterPosition = eyeCenterPosition;
		UpdateCamerasDirtyFlag = true;
    }

	/// <summary>
	/// Gets the orientation offset.
	/// </summary>
	/// <param name="orientationOffset">Orientation offset.</param>
    public void GetOrientationOffset(ref Quaternion orientationOffset)
    {
        orientationOffset = OrientationOffset;
    }
	/// <summary>
	/// Sets the orientation offset.
	/// </summary>
	/// <param name="orientationOffset">Orientation offset.</param>
    public void SetOrientationOffset(Quaternion orientationOffset)
    {
        OrientationOffset = orientationOffset;
    }

	/// <summary>
	/// Gets the Y rotation.
	/// </summary>
	/// <param name="yRotation">Y rotation.</param>
    public void GetYRotation(ref float yRotation)
    {
        yRotation = YRotation;
    }
	/// <summary>
	/// Sets the Y rotation.
	/// </summary>
	/// <param name="yRotation">Y rotation.</param>
    public void SetYRotation(float yRotation)
    {
        YRotation = yRotation;
    }

	/// <summary>
	/// Gets the tracker rotates y flag.
	/// </summary>
	/// <param name="trackerRotatesY">Tracker rotates y.</param>
    public void GetTrackerRotatesY(ref bool trackerRotatesY)
    {
        trackerRotatesY = TrackerRotatesY;
    }
	/// <summary>
	/// Sets the tracker rotates y flag.
	/// </summary>
	/// <param name="trackerRotatesY">If set to <c>true</c> tracker rotates y.</param>
    public void SetTrackerRotatesY(bool trackerRotatesY)
    {
        TrackerRotatesY = trackerRotatesY;
    }

    // GetCameraOrientationEulerAngles
	/// <summary>
	/// Gets the camera orientation euler angles.
	/// </summary>
	/// <returns><c>true</c>, if camera orientation euler angles was gotten, <c>false</c> otherwise.</returns>
	/// <param name="angles">Angles.</param>
    public bool GetCameraOrientationEulerAngles(ref Vector3 angles)
    {
        if (CameraRight == null)
            return false;

        angles = CameraRight.transform.rotation.eulerAngles;
        return true;
    }

	/// <summary>
	/// Gets the camera orientation.
	/// </summary>
	/// <returns><c>true</c>, if camera orientation was gotten, <c>false</c> otherwise.</returns>
	/// <param name="quaternion">Quaternion.</param>
    public bool GetCameraOrientation(ref Quaternion quaternion)
    {
        if (CameraRight == null)
            return false;

        quaternion = CameraRight.transform.rotation;
        return true;
    }

	/// <summary>
	/// Gets the camera position.
	/// </summary>
	/// <returns><c>true</c>, if camera position was gotten, <c>false</c> otherwise.</returns>
	/// <param name="position">Position.</param>
    public bool GetCameraPosition(ref Vector3 position)
    {
        if (CameraRight == null)
            return false;

        position = CameraRight.transform.position;

        return true;
    }

    // Get/Set NearClipPlane
    public void GetNearClipLane(ref float nearClipPlane)
    {
        nearClipPlane = NearClipPlane;
    }
    public void setNearClipLane(float nearClipPlane)
    {
        NearClipPlane = nearClipPlane;
		UpdateCamerasDirtyFlag = true;
    }

	/// <summary>
	/// Gets the camera.
	/// </summary>
	/// <param name="camera">Camera.</param>
    public void GetCamera(ref Camera camera)
    {
        camera = CameraRight;
    }

	/// <summary>
	/// Attachs a game object to the right (main) camera.
	/// </summary>
	/// <returns><c>true</c>, if game object to camera was attached, <c>false</c> otherwise.</returns>
	/// <param name="gameObject">Game object.</param>
    public bool AttachGameObjectToCamera(ref GameObject gameObject)
    {
        if (CameraRight == null)
            return false;

        gameObject.transform.parent = CameraRight.transform;

        return true;
    }

	/// <summary>
	/// Detachs the game object from the right (main) camera.
	/// </summary>
	/// <returns><c>true</c>, if game object from camera was detached, <c>false</c> otherwise.</returns>
	/// <param name="gameObject">Game object.</param>
    public bool DetachGameObjectFromCamera(ref GameObject gameObject)
    {
        if ((CameraRight != null) && (CameraRight.transform == gameObject.transform.parent))
        {
            gameObject.transform.parent = null;
            return true;
        }

        return false;
    }

    // Get Misc. values from CameraController

	/// <summary>
	/// Gets the height of the player eye.
	/// </summary>
	/// <returns><c>true</c>, if player eye height was gotten, <c>false</c> otherwise.</returns>
	/// <param name="eyeHeight">Eye height.</param>
    public bool GetPlayerEyeHeight(ref float eyeHeight)
    {
        eyeHeight = CameraRootPosition.y + NeckPosition.y + EyeCenterPosition.y;

        return true;
    }

#if (!UNITY_ANDROID || UNITY_EDITOR) // UnityJohn
	/// <summary>
	/// Sets the maximum visual quality.
	/// </summary>
	public void SetMaximumVisualQuality()
	{
		QualitySettings.softVegetation  = 		true;
		QualitySettings.maxQueuedFrames = 		1;
		QualitySettings.anisotropicFiltering = 	AnisotropicFiltering.ForceEnable;
		QualitySettings.vSyncCount = 			1;
	}
	
	// Throw any A/B testing here
	void TestChanges()	
	{
		if(Input.GetKeyDown(KeyCode.T))
		{
			// Toggle between pixel and mesh distortion
			if(MeshDistortion == true)
			{
				OVRDebugStreamer.message = "PIXEL DISTORTION\n";
				MeshDistortion = false;
			}
			else
			{
				OVRDebugStreamer.message = "MESH DISTORTION\n";
				MeshDistortion = true;				
			}
		}
	}
#endif
}

