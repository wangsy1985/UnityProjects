/************************************************************************************

Filename    :   OVRGamepadController.cs
Content     :   Interface to gamepad controller
Created     :   November 12, 2013
Authors     :   Peter Giokaris

Copyright   :   Copyright 2013 Oculus VR, Inc. All Rights reserved.

Use of this software is subject to the terms of the Oculus LLC license
agreement provided at the time of installation or download, or which
otherwise accompanies this software in either electronic or hard copy form.

************************************************************************************/
using UnityEngine;
using System;
using System.Runtime.InteropServices;


//-------------------------------------------------------------------------------------
// ***** OVRGamepadController
//

/// <summary>
/// OVRGamepadController is an interface class to a gamepad controller.
/// On Win machines, the gamepad must be XInput-compliant.
/// </summary>
public class OVRGamepadController : MonoBehaviour
{		
	//-------------------------
	// Input enums
	public enum Axis { LeftXAxis, LeftYAxis, RightXAxis, RightYAxis, LeftTrigger, RightTrigger };
	public enum Button { A, B, X, Y, Up, Down, Left, Right, Start, Back, LStick, RStick, L1, R1 };
		
	/// <summary>
	/// GPC_GetAxis
	/// </summary>
	/// <returns>The c_ get axis.</returns>
	/// <param name="axis">Axis.</param>
	public static float GPC_GetAxis(int axis)
	{
		if(axis == (int)Axis.LeftXAxis)
		{
			return Input.GetAxis ("Left_X_Axis");	
		}
		if(axis == (int)Axis.LeftYAxis)
		{
			return Input.GetAxis ("Left_Y_Axis");				
		}
		if(axis == (int)Axis.RightXAxis)
		{
			return Input.GetAxis ("Right_X_Axis");				
		}
		if(axis == (int)Axis.RightYAxis)
		{
			return Input.GetAxis ("Right_Y_Axis");				
		}
		if( axis == (int) Axis.LeftTrigger)
		{
			return Input.GetAxis ("LeftTrigger");
		}
		if( axis == (int) Axis.RightTrigger)
		{
			return Input.GetAxis ("RightTrigger");
		}

		return 0.0f;
	}

	/// <summary>
	/// GPC_GetButton
	/// </summary>
	/// <returns><c>true</c>, if c_ get button was GPed, <c>false</c> otherwise.</returns>
	/// <param name="button">Button.</param>
	public static bool GPC_GetButton(int button)
	{
		return false;
	}
}
