/************************************************************************************

Filename    :   OVRMainMenu.cs
Content     :   Main script to run various Unity scenes
Created     :   January 8, 2013
Authors     :   Peter Giokaris

Copyright   :   Copyright 2013 Oculus VR, Inc. All Rights reserved.

Use of this software is subject to the terms of the Oculus LLC license
agreement provided at the time of installation or download, or which
otherwise accompanies this software in either electronic or hard copy form.

************************************************************************************/
using UnityEngine;
using System.Collections;

//-------------------------------------------------------------------------------------
// ***** OVRMainMenu
//
/// <summary>
/// OVRMainMenu is used to control the loading of different scenes. It also renders out 
/// a menu that allows a user to modify various Rift settings, and allow for storing 
/// these settings for recall later.
/// 
/// A user of this component can add as many scenes that they would like to be able to 
/// have access to.
///
/// OVRMainMenu is currently attached to the OVRPlayerController prefab for convenience, 
/// but can safely removed from it and added to another GameObject that is used for general 
/// Unity logic.
///
/// </summary>
public class OVRMainMenu : MonoBehaviour
{
	private OVRPresetManager	PresetManager 	= new OVRPresetManager();
	
	public float 	FadeInTime    		= 2.0f;
	public Texture 	FadeInTexture 		= null;
	public Font 	FontReplace			= null;
	
	// Scenes to show onscreen
	public string [] SceneNames;
	public string [] Scenes;
	
	private bool ScenesVisible   	= false;
	
	// Spacing for scenes menu
	private int    	StartX			= 490;
	private int    	StartY			= 275;
	private int    	WidthX			= 300;
	private int    	WidthY			= 23;
	
	// Spacing for variables that users can change
	private int    	VRVarsSX		= 553;
	private int		VRVarsSY		= 265;
	private int    	VRVarsWidthX 	= 175;
	private int    	VRVarsWidthY 	= 23;

	private int    	StepY			= 23;
		
	// Handle to OVRCameraController
	private OVRCameraController CameraController = null;
	
	// Handle to OVRPlayerController
	private OVRPlayerController PlayerController = null;
	
	// Controller buttons
	private bool  PrevStartDown;
	private bool  PrevHatDown;
	private bool  PrevHatUp;
	
	private bool  ShowVRVars;
	
	private bool  OldSpaceHit;
	
	// FPS 
	private float  UpdateInterval 	= 0.5f;
	private float  Accum   			= 0; 	
	private int    Frames  			= 0; 	
	private float  TimeLeft			= 0; 				
	private string strFPS			= "FPS: 0";
	
	// IPD shift from physical IPD
	public float   IPDIncrement		= 0.0025f;
	private string strIPD 			= "IPD: 0.000";	
	
	// Prediction (in ms)
	public float   PredictionIncrement = 0.001f; // 1 ms
	private string strPrediction       = "Pred: OFF";	
	
	// FOV Variables
	public float   FOVIncrement		= 0.2f;
	private string strFOV     		= "FOV: 0.0f";
	
	// Eye Distance Variables (1/10mm)
	private float   EDIncrement		= 0.0001f;
	private string strED     		= "EyeDistance: 0.0f";
	
	// Distortion Variables
	public float   DistKIncrement   = 0.001f;
	private string strDistortion1 	= "Dist k0: 0.00f k1 0.00f";
	private string strDistortion2 	= "Dist k2: 0.00f k3 0.00f";
	
	// Height adjustment
	public float   HeightIncrement   = 0.01f;
	private string strHeight     	 = "Height: 0.0f";
	
	// Speed and rotation adjustment
	public float   SpeedRotationIncrement   	= 0.05f;
	private string strSpeedRotationMultipler    = "Spd. X: 0.0f Rot. X: 0.0f";
	
	private bool   LoadingLevel 	= false;	
	private float  AlphaFadeValue	= 1.0f;
	private int    CurrentLevel		= 0;
	
	// Rift detection
	private bool   HMDPresent           = false;
	private bool   SensorPresent        = false;
	private float  RiftPresentTimeout   = 0.0f;
	private string strRiftPresent		= "";
	
	// Device attach / detach
	public enum Device {HMDSensor, HMD, LatencyTester}
	private float  DeviceDetectionTimeout 	= 0.0f;
	private string strDeviceDetection 		= "";
	
	// Mag yaw-drift correction
	private OVRMagCalibration   MagCal     = new OVRMagCalibration();
	
	// Replace the GUI with our own texture and 3D plane that
	// is attached to the rendder camera for true 3D placement
	private OVRGUI  		GuiHelper 		 = new OVRGUI();
	private GameObject      GUIRenderObject  = null;
	private RenderTexture	GUIRenderTexture = null;
	private bool 			ShowGrid 		 = false;
	
	// Crosshair system, rendered onto 3D plane
	public Texture  CrosshairImage 			= null;
	private OVRCrosshair Crosshair        	= new OVRCrosshair();
	
	// Create a delegate for update functions
	private delegate void updateFunctions();
	private updateFunctions UpdateFunctions;
	
	
	// * * * * * * * * * * * * *

	/// <summary>
	/// Awake this instance.
	/// </summary>
	void Awake()
	{
		// Find camera controller
		OVRCameraController[] CameraControllers;
		CameraControllers = gameObject.GetComponentsInChildren<OVRCameraController>();
		
		if(CameraControllers.Length == 0)
			Debug.LogWarning("OVRMainMenu: No OVRCameraController attached.");
		else if (CameraControllers.Length > 1)
			Debug.LogWarning("OVRMainMenu: More then 1 OVRCameraController attached.");
		else
			CameraController = CameraControllers[0];
	
		// Find player controller
		OVRPlayerController[] PlayerControllers;
		PlayerControllers = gameObject.GetComponentsInChildren<OVRPlayerController>();
		
		if(PlayerControllers.Length == 0)
			Debug.LogWarning("OVRMainMenu: No OVRPlayerController attached.");
		else if (PlayerControllers.Length > 1)
			Debug.LogWarning("OVRMainMenu: More then 1 OVRPlayerController attached.");
		else
			PlayerController = PlayerControllers[0];
	
	}
	
	/// <summary>
	/// Start this instance.
	/// </summary>
	void Start()
	{
		AlphaFadeValue = 1.0f;	
		CurrentLevel   = 0;
		PrevStartDown  = false;
		PrevHatDown    = false;
		PrevHatUp      = false;
		ShowVRVars	   = false;
		OldSpaceHit    = false;
		strFPS         = "FPS: 0";
		LoadingLevel   = false;	
		ScenesVisible    = false;
		
		// Ensure that camera controller variables have been properly
		// initialized before we start reading them
		if(CameraController != null)
		{
			CameraController.InitCameraControllerVariables();
			GuiHelper.SetCameraController(ref CameraController);
		}
		
		// Set the GUI target 
		GUIRenderObject = GameObject.Instantiate(Resources.Load("OVRGUIObjectMain")) as GameObject;
		
		if(GUIRenderObject != null)
		{
			if(GUIRenderTexture == null)
			{
				int w = Screen.width;
				int h = Screen.height;

				if(CameraController.PortraitMode == true)
				{
					int t = h;
					h = w;
					w = t;
				}
				
				// We don't need a depth buffer on this texture
				GUIRenderTexture = new RenderTexture(w, h, 0);	
				GuiHelper.SetPixelResolution(w, h);
				// PGG: This is used for keeping non-DK1 displays from 
				// rendering off center (will be fixed when target rendering
				// is done via normalized co-ordinates)
				//GuiHelper.SetDisplayResolution(OVRDevice.HResolution, OVRDevice.VResolution);
				GuiHelper.SetDisplayResolution(1280.0f, 800.0f);
			}
		}
		
		// Attach GUI texture to GUI object and GUI object to Camera
		if(GUIRenderTexture != null && GUIRenderObject != null)
		{
			GUIRenderObject.renderer.material.mainTexture = GUIRenderTexture;
			
			if(CameraController != null)
			{
				// Grab transform of GUI object
				Transform t = GUIRenderObject.transform;
				// Attach the GUI object to the camera
				CameraController.AttachGameObjectToCamera(ref GUIRenderObject);
				// Reset the transform values (we will be maintaining state of the GUI object
				// in local state)
				OVRUtils.SetLocalTransform(ref GUIRenderObject, ref t);
				// Deactivate object until we have completed the fade-in
				// Also, we may want to deactive the render object if there is nothing being rendered
				// into the UI
				// we will move the position of everything over to the left, so get
				// IPD / 2 and position camera towards negative X
				Vector3 lp = GUIRenderObject.transform.localPosition;
				float ipd = 0.0f;
				CameraController.GetIPD(ref ipd);
				lp.x -= ipd * 0.5f;
				GUIRenderObject.transform.localPosition = lp;
				
				GUIRenderObject.SetActive(false);
			}
		}
		
		// Save default values initially
		StoreSnapshot("DEFAULT");
		
		// Make sure to hide cursor 
		if(Application.isEditor == false)
		{
			Screen.showCursor = false; 
			Screen.lockCursor = true;
		}
		
		// Add delegates to update; useful for ordering menu tasks, if required
		UpdateFunctions += UpdateFPS;
		
		// CameraController updates
		if(CameraController != null)
		{
			UpdateFunctions += UpdateIPD;
			UpdateFunctions += UpdatePrediction;
			UpdateFunctions += UpdateFOV;
			UpdateFunctions += UpdateEyeDistance;
			UpdateFunctions += UpdateDistortionCoefs;
			
			// PGG We will not change player height while tuning distortion			
			//UpdateFunctions += UpdateEyeHeightOffset;
		}
		
		// PlayerController updates
		if(PlayerController != null)
		{
			// PGG We will not change player movement scale while tuning distortion
			//UpdateFunctions += UpdateSpeedAndRotationScaleMultiplier;
			UpdateFunctions += UpdatePlayerControllerMovement;
		}
		
		// MainMenu updates
		UpdateFunctions += UpdateSelectCurrentLevel;
		UpdateFunctions += UpdateHandleSnapshots;
		
		// Device updates
		UpdateFunctions += UpdateDeviceDetection;
		UpdateFunctions += UpdateResetOrientation;
		OVRMessenger.AddListener<Device, bool>("Sensor_Attached", UpdateDeviceDetectionMsgCallback);
		
		// Mag Yaw-Drift correction
		UpdateFunctions += MagCal.UpdateMagYawDriftCorrection;
		MagCal.SetOVRCameraController(ref CameraController);
		
		// Crosshair functionality
		Crosshair.InitCrosshair();
		Crosshair.SetCrosshairTexture(ref CrosshairImage);
		Crosshair.SetOVRCameraController (ref CameraController);
		Crosshair.SetOVRPlayerController(ref PlayerController);
		UpdateFunctions += Crosshair.UpdateCrosshair;
		
		// Check for HMD and sensor
		CheckIfRiftPresent();
		
		// Init static members
		ScenesVisible = false;
	}
	
	/// <summary>
	/// Update this instance.
	/// </summary>
	void Update()
	{		
		if(LoadingLevel == true)
			return;
		
		// Update specific delegate variables that are not passed through
		// the delegate master function (may change UpdateFunctions to take
		// a data ptr or certain variables)
		// MagCal.MagAutoCalibrate = MagAutoCalibrate;
		
		// Main update
		UpdateFunctions();
		
		// We will add showing the grid here
		if (Input.GetKeyDown(KeyCode.G) == true)
			ShowGrid = (ShowGrid == true) ? false : true;
		
		// Toggle Fullscreen
		if(Input.GetKeyDown(KeyCode.F11))
			Screen.fullScreen = !Screen.fullScreen;
		
		// Escape Application
		if (Input.GetKeyDown(KeyCode.Escape))
			Application.Quit();
	}
	
	/// <summary>
	/// Updates the FPS.
	/// </summary>
	void UpdateFPS()
	{
    	TimeLeft -= Time.deltaTime;
   	 	Accum += Time.timeScale/Time.deltaTime;
    	++Frames;
 
    	// Interval ended - update GUI text and start new interval
    	if( TimeLeft <= 0.0 )
    	{
        	// display two fractional digits (f2 format)
			float fps = Accum / Frames;
			
			if(ShowVRVars == true)// limit gc
				strFPS = System.String.Format("FPS: {0:F2}",fps);

       		TimeLeft += UpdateInterval;
        	Accum = 0.0f;
        	Frames = 0;
    	}
	}
	
	/// <summary>
	/// Updates the IPD.
	/// </summary>
	void UpdateIPD()
	{
		if(Input.GetKeyDown (KeyCode.Equals))
		{
			float ipd = 0;
			CameraController.GetIPD(ref ipd);
			ipd += IPDIncrement;
			CameraController.SetIPD (ipd);
		}
		else if(Input.GetKeyDown (KeyCode.Minus))
		{
			float ipd = 0;
			CameraController.GetIPD(ref ipd);
			ipd -= IPDIncrement;
			CameraController.SetIPD (ipd);
		}
		
		if(ShowVRVars == true)// limit gc
		{	
			float ipd = 0;
			CameraController.GetIPD (ref ipd);
			strIPD = System.String.Format("IPD (mm): {0:F4}", ipd * 1000.0f);
		}
	}
	
	/// <summary>
	/// Updates the prediction.
	/// </summary>
	void UpdatePrediction()
	{
		// Turn prediction on/off
		if(Input.GetKeyDown (KeyCode.P))
		{		
			if( CameraController.PredictionOn == false) 
				CameraController.PredictionOn = true;
			else
				CameraController.PredictionOn = false;
		}
		
		// Update prediction value (only if prediction is on)
		if(CameraController.PredictionOn == true)
		{
			float pt = OVRDevice.GetPredictionTime(0); 
			if(Input.GetKeyDown (KeyCode.Comma))
				pt -= PredictionIncrement;
			else if(Input.GetKeyDown (KeyCode.Period))
				pt += PredictionIncrement;
			
			OVRDevice.SetPredictionTime(0, pt);
			
			// re-get the prediction time to make sure it took
			pt = OVRDevice.GetPredictionTime(0) * 1000.0f;
			
			if(ShowVRVars == true)// limit gc
				strPrediction = System.String.Format ("Pred (ms): {0:F3}", pt);								 
		}
		else
		{
			strPrediction = "Pred: OFF";
		}
	}
	
	/// <summary>
	/// Updates the FOV.
	/// </summary>
	void UpdateFOV()
	{
		if(Input.GetKeyDown (KeyCode.LeftBracket))
		{
			float cfov = 0;
			CameraController.GetVerticalFOV(ref cfov);
			cfov -= FOVIncrement;
			CameraController.SetVerticalFOV(cfov);
		}
		else if (Input.GetKeyDown (KeyCode.RightBracket))
		{
			float cfov = 0;
			CameraController.GetVerticalFOV(ref cfov);
			cfov += FOVIncrement;
			CameraController.SetVerticalFOV(cfov);
		}
		
		if(ShowVRVars == true)// limit gc
		{
			float cfov = 0;
			CameraController.GetVerticalFOV(ref cfov);
			strFOV = System.String.Format ("FOV (deg): {0:F3}", cfov);
		}
	}
	
	// UpdateEyeDistance
	void UpdateEyeDistance()
	{
		float oldED = OVRDevice.EyeToScreenDistance;
		float newED = oldED;
		
		if(Input.GetKeyDown (KeyCode.Alpha9))
		{
			newED += EDIncrement;
		}
		else if(Input.GetKeyDown (KeyCode.Alpha0))
		{
			newED -= EDIncrement;
		}
		
		if(oldED != newED)
		{
			OVRDevice.EyeToScreenDistance = newED;
			float fov = OVRDevice.VerticalFOV();
			CameraController.SetVerticalFOV(fov);
		}
		
		if(ShowVRVars == true)// limit gc
		{
			strED = System.String.Format ("Eye Distance: {0:F4}", OVRDevice.EyeToScreenDistance);			
			
			// Change FOV to reflect Eye Distance change
			float cfov = 0;
			CameraController.GetVerticalFOV(ref cfov);
			strFOV = System.String.Format ("FOV (deg): {0:F3}", cfov);
		}
	}
	
	// UpdateDistortionCoefs
	void UpdateDistortionCoefs()
	{
	 	float Dk0 = 0.0f;
		float Dk1 = 0.0f;
		float Dk2 = 0.0f;
		float Dk3 = 0.0f;
		
		// * * * * * * * * *
		// Get the distortion coefficients to apply to shader
		CameraController.GetDistortionCoefs(ref Dk0, ref Dk1, ref Dk2, ref Dk3);
		
		bool dirtyK = false;

		if(Input.GetKeyDown(KeyCode.Alpha1))
		{
			Dk0 -= DistKIncrement; dirtyK = true;
		}
		else if (Input.GetKeyDown(KeyCode.Alpha2))
		{
			Dk0 += DistKIncrement; dirtyK = true;
		}
			
		if(Input.GetKeyDown(KeyCode.Alpha3))
		{
			Dk1 -= DistKIncrement; dirtyK = true;
		}
		else if (Input.GetKeyDown(KeyCode.Alpha4))
		{
			Dk1 += DistKIncrement; dirtyK = true;
		}

		if(Input.GetKeyDown(KeyCode.Alpha5))
		{
			Dk2 -= DistKIncrement; dirtyK = true;
		}
		else if (Input.GetKeyDown(KeyCode.Alpha6))
		{
			Dk2 += DistKIncrement; dirtyK = true;
		}
			
		if(Input.GetKeyDown(KeyCode.Alpha7))
		{
			Dk3 -= DistKIncrement; dirtyK = true;
		}
		else if (Input.GetKeyDown(KeyCode.Alpha8))
		{
			Dk3 += DistKIncrement; dirtyK = true;
		}

		if(dirtyK == true)
			CameraController.SetDistortionCoefs(Dk0, Dk1, Dk2, Dk3);
		
		if(ShowVRVars == true) // limit gc
		{
			strDistortion1 = 
			System.String.Format ("DST k0: {0:F3} k1 {1:F3}", Dk0, Dk1);
			strDistortion2 = 
			System.String.Format ("DST k2: {0:F3} k3 {1:F3}", Dk2, Dk3);
		}
	}
	
	/// <summary>
	/// Updates the eye height offset.
	/// </summary>
	void UpdateEyeHeightOffset()
	{
		// We will update neck position, since camera root and eye center should
		// be set differently.
		if(Input.GetKeyDown(KeyCode.Alpha5))
		{	
			Vector3 neckPosition = Vector3.zero;
			CameraController.GetNeckPosition(ref neckPosition);
			neckPosition.y -= HeightIncrement;
			CameraController.SetNeckPosition(neckPosition);
		}
		else if (Input.GetKeyDown(KeyCode.Alpha6))
		{
			Vector3 neckPosition = Vector3.zero;;
			CameraController.GetNeckPosition(ref neckPosition);
			neckPosition.y += HeightIncrement;
			CameraController.SetNeckPosition(neckPosition);
		}
			
		if(ShowVRVars == true)// limit gc
		{
			float eyeHeight = 0.0f;
			CameraController.GetPlayerEyeHeight(ref eyeHeight);
			
			strHeight = System.String.Format ("Eye Height (m): {0:F3}", eyeHeight);
		}
	}
	
	/// <summary>
	/// Updates the speed and rotation scale multiplier.
	/// </summary>
	void UpdateSpeedAndRotationScaleMultiplier()
	{
		float moveScaleMultiplier = 0.0f;
		PlayerController.GetMoveScaleMultiplier(ref moveScaleMultiplier);
		if(Input.GetKeyDown(KeyCode.Alpha7))
			moveScaleMultiplier -= SpeedRotationIncrement;
		else if (Input.GetKeyDown(KeyCode.Alpha8))
			moveScaleMultiplier += SpeedRotationIncrement;		
		PlayerController.SetMoveScaleMultiplier(moveScaleMultiplier);
		
		float rotationScaleMultiplier = 0.0f;
		PlayerController.GetRotationScaleMultiplier(ref rotationScaleMultiplier);
		if(Input.GetKeyDown(KeyCode.Alpha9))
			rotationScaleMultiplier -= SpeedRotationIncrement;
		else if (Input.GetKeyDown(KeyCode.Alpha0))
			rotationScaleMultiplier += SpeedRotationIncrement;	
		PlayerController.SetRotationScaleMultiplier(rotationScaleMultiplier);
		
		if(ShowVRVars == true)// limit gc
			strSpeedRotationMultipler = System.String.Format ("Spd.X: {0:F2} Rot.X: {1:F2}", 
									moveScaleMultiplier, 
									rotationScaleMultiplier);
	}
	
	/// <summary>
	/// Updates the player controller movement.
	/// </summary>
	void UpdatePlayerControllerMovement()
	{
		if(PlayerController != null)
			PlayerController.SetHaltUpdateMovement(ScenesVisible);
	}
	
	/// <summary>
	/// Updates the select current level.
	/// </summary>
	void UpdateSelectCurrentLevel()
	{
		ShowLevels();
				
		if(ScenesVisible == false)
			return;
			
		CurrentLevel = GetCurrentLevel();
		
		if((Scenes.Length != 0) && 
		   ((OVRGamepadController.GPC_GetButton((int)OVRGamepadController.Button.A) == true) ||
			 Input.GetKeyDown(KeyCode.Return)))
		{
			LoadingLevel = true;
			Application.LoadLevelAsync(Scenes[CurrentLevel]);
		}
	}
	
	/// <summary>
	/// Shows the levels.
	/// </summary>
	/// <returns><c>true</c>, if levels was shown, <c>false</c> otherwise.</returns>
	bool ShowLevels()
	{
		if(Scenes.Length == 0)
		{
			ScenesVisible = false;
			return ScenesVisible;
		}
		
		bool curStartDown = false;
		if(OVRGamepadController.GPC_GetButton((int)OVRGamepadController.Button.Start) == true)
			curStartDown = true;
		
		if((PrevStartDown == false) && (curStartDown == true) ||
			Input.GetKeyDown(KeyCode.RightShift) )
		{
			if(ScenesVisible == true) 
				ScenesVisible = false;
			else 
				ScenesVisible = true;
		}
		
		PrevStartDown = curStartDown;
		
		return ScenesVisible;
	}
	
	/// <summary>
	/// Gets the current level.
	/// </summary>
	/// <returns>The current level.</returns>
	int GetCurrentLevel()
	{
		bool curHatDown = false;
		if(OVRGamepadController.GPC_GetButton((int)OVRGamepadController.Button.Down) == true)
			curHatDown = true;
		
		bool curHatUp = false;
		if(OVRGamepadController.GPC_GetButton((int)OVRGamepadController.Button.Down) == true)
			curHatUp = true;
		
		if((PrevHatDown == false) && (curHatDown == true) ||
			Input.GetKeyDown(KeyCode.DownArrow))
		{
			CurrentLevel = (CurrentLevel + 1) % SceneNames.Length;	
		}
		else if((PrevHatUp == false) && (curHatUp == true) ||
			Input.GetKeyDown(KeyCode.UpArrow))
		{
			CurrentLevel--;	
			if(CurrentLevel < 0)
				CurrentLevel = SceneNames.Length - 1;
		}
					
		PrevHatDown = curHatDown;
		PrevHatUp = curHatUp;
		
		return CurrentLevel;
	}
	
	// GUI
	
	/// <summary>
	/// Raises the GU event.
	/// </summary>
 	void OnGUI()
 	{		
		// Important to keep from skipping render events
		if (Event.current.type != EventType.Repaint)
			return;

		// Fade in screen
		if(AlphaFadeValue > 0.0f)
		{
  			AlphaFadeValue -= Mathf.Clamp01(Time.deltaTime / FadeInTime);
			if(AlphaFadeValue < 0.0f)
			{
				AlphaFadeValue = 0.0f;	
			}
			else
			{
				GUI.color = new Color(0, 0, 0, AlphaFadeValue);
  				GUI.DrawTexture( new Rect(0, 0, Screen.width, Screen.height ), FadeInTexture ); 
				return;
			}
		}
		
		// We can turn on the render object so we can render the on-screen menu
		if(GUIRenderObject != null)
		{
			if (ScenesVisible || ShowVRVars || ShowGrid || Crosshair.IsCrosshairVisible() || 
				RiftPresentTimeout > 0.0f || DeviceDetectionTimeout > 0.0f ||
				((MagCal.Disabled () == false) && (MagCal.Ready () == false))
				)
			{	
				GUIRenderObject.SetActive(true);
				GuiHelper.DoNotDraw = false;
			}
			else
			{
				GUIRenderObject.SetActive(false);
				GuiHelper.DoNotDraw = true;
			}
		}
		
		//***
		// Set the GUI matrix to deal with portrait mode
		Vector3 scale = Vector3.one;
		if(CameraController.PortraitMode == true)
		{
			float h = OVRDevice.HResolution;
			float v = OVRDevice.VResolution;
			scale.x = v / h; 					// calculate hor scale
    		scale.y = h / v; 					// calculate vert scale
		}
		Matrix4x4 svMat = GUI.matrix; // save current matrix
    	// substitute matrix - only scale is altered from standard
    	GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, scale);
		
		// Cache current active render texture
		RenderTexture previousActive = RenderTexture.active;
		
		// if set, we will render to this texture
		if((GUIRenderTexture != null) && (GuiHelper.DoNotDraw == false))
		{
			RenderTexture.active = GUIRenderTexture;
			GL.Clear (false, true, new Color (0.0f, 0.0f, 0.0f, 0.0f));
		}
		
		// Update OVRGUI functions (will be deprecated eventually when 2D rendering
		// is removed from GUI)
		GuiHelper.SetFontReplace(FontReplace);
		
		// If true, we are displaying information about the Rift not being detected
		// So do not show anything else
		if(GUIShowRiftDetected() != true)
		{	
			// Draw grid to be an RGB value to minimize chromatic distortion
#if UNITY_ANDROID  // UnityJohn
//JDC: no grid in new code			if(ShowGrid == true)
//				OVRCamera.DrawGrid(GUIRenderTexture, 60, 40, Color.green);
#else
			if(ShowGrid == true)
				OVRCamera.DrawGrid(GUIRenderTexture, 60, 40, Color.green);
#endif
			
			GUIShowLevels();
			GUIShowVRVariables();
		}
		
		// The cross-hair may need to go away at some point, unless someone finds it 
		// useful
		Crosshair.OnGUICrosshair();
					
		// Restore active render texture
		if(GuiHelper.DoNotDraw == false)
			RenderTexture.active = previousActive;
		
		// ***
		// Restore previous GUI matrix
		GUI.matrix = svMat;
 	}
	
	/// <summary>
	/// Show the GUI levels.
	/// </summary>
	void GUIShowLevels()
	{
		if(ScenesVisible == true)
		{   
			// Darken the background by rendering fade texture 
			GUI.color = new Color(0, 0, 0, 0.5f);
  			GUI.DrawTexture( new Rect(0, 0, Screen.width, Screen.height ), FadeInTexture );
 			GUI.color = Color.white;
		
			if(LoadingLevel == true)
			{
				string loading = "LOADING...";
				GuiHelper.StereoBox (StartX, StartY, WidthX, WidthY, ref loading, Color.yellow);
				return;
			}
			
			for (int i = 0; i < SceneNames.Length; i++)
			{
				Color c;
				if(i == CurrentLevel)
					c = Color.yellow;
				else
					c = Color.black;
				
				int y   = StartY + (i * StepY);
				
				GuiHelper.StereoBox (StartX, y, WidthX, WidthY, ref SceneNames[i], c);
			}
		}				
	}
	
	/// <summary>
	/// Show the VR variables.
	/// </summary>
	void GUIShowVRVariables()
	{
		bool SpaceHit = Input.GetKey("space");
		if ((OldSpaceHit == false) && (SpaceHit == true))
		{
			if(ShowVRVars == true) 
				ShowVRVars = false;
			else 
				ShowVRVars = true;
		}
		
		OldSpaceHit = SpaceHit;

		int y   = VRVarsSY;
		
		if(ShowVRVars == false)
		{
			if((MagCal.Disabled () == false) && (MagCal.Ready () == false))
			{
				// Print out auto mag correction state
				MagCal.GUIMagYawDriftCorrection(VRVarsSX, y, 
												VRVarsWidthX, VRVarsWidthY,
												ref GuiHelper);
			}
		}
		else
		{				
			// Print out auto mag correction state
			MagCal.GUIMagYawDriftCorrection(VRVarsSX, y, 
											VRVarsWidthX, VRVarsWidthY,
											ref GuiHelper);
			
			// Draw FPS
			GuiHelper.StereoBox (VRVarsSX, y += StepY, VRVarsWidthX, VRVarsWidthY, 
								 ref strFPS, Color.green);
		
			// Don't draw these vars if CameraController is not present
			if(CameraController != null)
			{
				GuiHelper.StereoBox (VRVarsSX, y += StepY, VRVarsWidthX, VRVarsWidthY, 
								 ref strPrediction, Color.white);		
				GuiHelper.StereoBox (VRVarsSX, y += StepY, VRVarsWidthX, VRVarsWidthY, 
								 ref strIPD, Color.yellow);			
				GuiHelper.StereoBox (VRVarsSX, y += StepY, VRVarsWidthX, VRVarsWidthY, 
								 ref strED, Color.white);
				GuiHelper.StereoBox (VRVarsSX, y += StepY, VRVarsWidthX, VRVarsWidthY, 
								 ref strFOV, Color.yellow);
			}
		
			// Eventually remove distortion from being changed
			// Don't draw if CameraController is not present
			if(CameraController != null)
			{
				// Distortion k values
				GuiHelper.StereoBox (VRVarsSX, y += StepY, VRVarsWidthX, VRVarsWidthY, 
								 ref strDistortion1, 
								 Color.red);
				GuiHelper.StereoBox (VRVarsSX, y += StepY, VRVarsWidthX, VRVarsWidthY, 
								 ref strDistortion2, 
								 Color.red);
			}
			
			// PGG We will not draw these while tuning distortion
			/*
			// Don't draw these vars if PlayerController is not present
			if(PlayerController != null)
			{
				GuiHelper.StereoBox (VRVarsSX, y += StepY, VRVarsWidthX, VRVarsWidthY, 
									 ref strHeight, Color.yellow);
				GuiHelper.StereoBox (VRVarsSX, y += StepY, VRVarsWidthX, VRVarsWidthY, 
									 ref strSpeedRotationMultipler, Color.white);
			}
			*/
			
		}
	}
	
	// SNAPSHOT MANAGEMENT
	
	/// <summary>
	/// Handle update of snapshots.
	/// </summary>
	void UpdateHandleSnapshots()
	{
		// Default shapshot
		if(Input.GetKeyDown(KeyCode.F2) || Input.GetKeyDown(KeyCode.Y))
			LoadSnapshot ("DEFAULT");
		
		// Snapshot 1
		if(Input.GetKeyDown(KeyCode.F3) || Input.GetKeyDown(KeyCode.U))
		{	
			if(Input.GetKey(KeyCode.Tab))
				StoreSnapshot ("SNAPSHOT1");
			else
				LoadSnapshot ("SNAPSHOT1");
		}
		
		// Snapshot 2
		if(Input.GetKeyDown(KeyCode.F4) || Input.GetKeyDown(KeyCode.I))
		{	
			if(Input.GetKey(KeyCode.Tab))
				StoreSnapshot ("SNAPSHOT2");
			else
				LoadSnapshot ("SNAPSHOT2");
		}
		
		// Snapshot 3
		if(Input.GetKeyDown(KeyCode.F5) || Input.GetKeyDown(KeyCode.O))
		{	
			if(Input.GetKey(KeyCode.Tab))
				StoreSnapshot ("SNAPSHOT3");
			else
				LoadSnapshot ("SNAPSHOT3");
		}
		
	}
	
	/// <summary>
	/// Stores the snapshot.
	/// </summary>
	/// <returns><c>true</c>, if snapshot was stored, <c>false</c> otherwise.</returns>
	/// <param name="snapshotName">Snapshot name.</param>
	bool StoreSnapshot(string snapshotName)
	{
		float f = 0;
		
		PresetManager.SetCurrentPreset(snapshotName);
		
		if(CameraController != null)
		{
			CameraController.GetIPD(ref f);
			PresetManager.SetPropertyFloat("IPD", ref f);
	
			f = OVRDevice.GetPredictionTime(0);
			PresetManager.SetPropertyFloat("PREDICTION", ref f);
		
			CameraController.GetVerticalFOV(ref f);
			PresetManager.SetPropertyFloat("FOV", ref f);
			
			f = OVRDevice.EyeToScreenDistance;
			PresetManager.SetPropertyFloat("ED", ref f);
			
			Vector3 neckPosition = Vector3.zero;
			CameraController.GetNeckPosition(ref neckPosition);
			PresetManager.SetPropertyFloat("HEIGHT", ref neckPosition.y);
			
			float Dk0 = 0.0f;
			float Dk1 = 0.0f;
			float Dk2 = 0.0f;
			float Dk3 = 0.0f;		
			CameraController.GetDistortionCoefs(ref Dk0, ref Dk1, ref Dk2, ref Dk3);
		
			PresetManager.SetPropertyFloat("DISTORTIONK0", ref Dk0);
			PresetManager.SetPropertyFloat("DISTORTIONK1", ref Dk1);
			PresetManager.SetPropertyFloat("DISTORTIONK2", ref Dk2);
			PresetManager.SetPropertyFloat("DISTORTIONK3", ref Dk3);	
		}
			
		if(PlayerController != null)
		{
			PlayerController.GetMoveScaleMultiplier(ref f);
			PresetManager.SetPropertyFloat("SPEEDMULT", ref f);

			PlayerController.GetRotationScaleMultiplier(ref f);
			PresetManager.SetPropertyFloat("ROTMULT", ref f);
		}
	
		return true;
	}
	
	/// <summary>
	/// Loads the snapshot.
	/// </summary>
	/// <returns><c>true</c>, if snapshot was loaded, <c>false</c> otherwise.</returns>
	/// <param name="snapshotName">Snapshot name.</param>
	bool LoadSnapshot(string snapshotName)
	{
		float f = 0;
		
		PresetManager.SetCurrentPreset(snapshotName);
		
		if(CameraController != null)
		{
			if(PresetManager.GetPropertyFloat("IPD", ref f) == true)
				CameraController.SetIPD(f);
		
			if(PresetManager.GetPropertyFloat("PREDICTION", ref f) == true)
				OVRDevice.SetPredictionTime(0, f);
		
			if(PresetManager.GetPropertyFloat("FOV", ref f) == true)
				CameraController.SetVerticalFOV(f);
		
			if(PresetManager.GetPropertyFloat("ED", ref f) == true)
				OVRDevice.EyeToScreenDistance = f;
			
			if(PresetManager.GetPropertyFloat("HEIGHT", ref f) == true)
			{
				Vector3 neckPosition = Vector3.zero;
				CameraController.GetNeckPosition(ref neckPosition);
				neckPosition.y = f;
				CameraController.SetNeckPosition(neckPosition);
			}

			float Dk0 = 0.0f;
			float Dk1 = 0.0f;
			float Dk2 = 0.0f;
			float Dk3 = 0.0f;
			CameraController.GetDistortionCoefs(ref Dk0, ref Dk1, ref Dk2, ref Dk3);
		
			if(PresetManager.GetPropertyFloat("DISTORTIONK0", ref f) == true)
				Dk0 = f;
			if(PresetManager.GetPropertyFloat("DISTORTIONK1", ref f) == true)
				Dk1 = f;
			if(PresetManager.GetPropertyFloat("DISTORTIONK2", ref f) == true)
				Dk2 = f;
			if(PresetManager.GetPropertyFloat("DISTORTIONK3", ref f) == true)
				Dk3 = f;
		
			CameraController.SetDistortionCoefs(Dk0, Dk1, Dk2, Dk3);
		
		}
		
		if(PlayerController != null)
		{
			if(PresetManager.GetPropertyFloat("SPEEDMULT", ref f) == true)
				PlayerController.SetMoveScaleMultiplier(f);

			if(PresetManager.GetPropertyFloat("ROTMULT", ref f) == true)
				PlayerController.SetRotationScaleMultiplier(f);
		}
			
		return true;
	}
	
	// RIFT DETECTION
	
	/// <summary>
	/// Checks to see if HMD and / or sensor is available, and displays a 
	/// message if it is not.
	/// </summary>
	void CheckIfRiftPresent()
	{
		HMDPresent = OVRDevice.IsHMDPresent();
		SensorPresent = OVRDevice.IsSensorPresent(0); // 0 is the main head sensor
		
		if((HMDPresent == false) || (SensorPresent == false))
		{
			RiftPresentTimeout = 5.0f; // Keep message up for 10 seconds
			
			if((HMDPresent == false) && (SensorPresent == false))
				strRiftPresent = "NO HMD AND SENSOR DETECTED";
			else if (HMDPresent == false)
				strRiftPresent = "NO HMD DETECTED";
			else if (SensorPresent == false)
				strRiftPresent = "NO SENSOR DETECTED";
		}
	}
	
	/// <summary>
	/// Show if Rift is detected.
	/// </summary>
	/// <returns><c>true</c>, if show rift detected was GUIed, <c>false</c> otherwise.</returns>
	bool GUIShowRiftDetected()
	{
		if(RiftPresentTimeout > 0.0f)
		{
			GuiHelper.StereoBox (StartX, StartY, WidthX, WidthY, 
								 ref strRiftPresent, Color.white);
		
			return true;
		}
		else if(DeviceDetectionTimeout > 0.0f)
		{
			GuiHelper.StereoBox (StartX, StartY, WidthX, WidthY, 
								 ref strDeviceDetection, Color.white);
			
			return true;
		}
		
		return false;
	}
	
	/// <summary>
	/// Updates the device detection.
	/// </summary>
	void UpdateDeviceDetection()
	{
		if(RiftPresentTimeout > 0.0f)
			RiftPresentTimeout -= Time.deltaTime;
		
		if(DeviceDetectionTimeout > 0.0f)
			DeviceDetectionTimeout -= Time.deltaTime;
	}
	
	/// <summary>
	/// Updates the device detection message callback.
	/// </summary>
	/// <param name="device">Device.</param>
	/// <param name="attached">If set to <c>true</c> attached.</param>
	void UpdateDeviceDetectionMsgCallback(Device device, bool attached)
	{
		if(attached == true)
		{
			switch(device)
			{
				case(Device.HMDSensor):
					strDeviceDetection = "HMD SENSOR ATTACHED";
					break;
			
				case(Device.HMD):
					strDeviceDetection = "HMD ATTACHED";
					break;

				case(Device.LatencyTester):
					strDeviceDetection = "LATENCY SENSOR ATTACHED";
					break;
			}
		}
		else
		{
			switch(device)
			{
				case(Device.HMDSensor):
					strDeviceDetection = "HMD SENSOR DETACHED";
					break;
			
				case(Device.HMD):
					strDeviceDetection = "HMD DETACHED";
					break;

				case(Device.LatencyTester):
					strDeviceDetection = "LATENCY SENSOR DETACHED";
					break;
			}
		}
		
		// Do not show on startup of level, since we will allow the
		// other method of detecting the rift to show, and not allow
		// this method to create a false positive detection at the start
		if(AlphaFadeValue == 0.0f)
			DeviceDetectionTimeout = 3.0f;
	}
	
	// RIFT RESET ORIENTATION
	
	// UpdateResetOrientation
	void UpdateResetOrientation()
	{
		if( ((ScenesVisible == false) && 
			 (OVRGamepadController.GPC_GetButton((int)OVRGamepadController.Button.Down) == true)) ||
			(Input.GetKeyDown(KeyCode.B) == true) )
		{
			OVRDevice.ResetOrientation(0);
		}
	}

}
