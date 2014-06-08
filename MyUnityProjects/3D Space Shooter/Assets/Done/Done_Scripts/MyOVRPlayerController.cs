using UnityEngine;
using System.Collections;

public class MyOVRPlayerController : MonoBehaviour {

	public Done_Boundary boundary;

	private Done_GameController gameController;
	private bool hasChanged = false;

	void Start(){
		GameObject gameControllerObject = GameObject.FindGameObjectWithTag ("GameController");
		if (gameControllerObject != null)
		{
			gameController = gameControllerObject.GetComponent <Done_GameController>();
		}
		if (gameController == null)
		{
			Debug.Log ("Cannot find 'GameController' script");
		}
	}

	void FixedUpdate () {
		Debug.Log (transform.position.ToString ());

//		if (gameController.isGameOver () && hasChanged == false) {
//			hasChanged = true;
//			boundary.xMax = transform.position.x;
//			boundary.xMin = transform.position.x;
//			boundary.zMax = transform.position.z;
//			boundary.zMin = transform.position.z;
//		}

		Debug.Log (string.Format("{0}; {1}; {2}; {3}", boundary.xMax, boundary.xMin, boundary.zMax, boundary.zMin));

		if (transform.position.x <= boundary.xMin)
			transform.position = new Vector3 (boundary.xMin, transform.position.y, transform.position.z);
		else if(transform.position.x >= boundary.xMax)
			transform.position = new Vector3 (boundary.xMax, transform.position.y, transform.position.z);

		if(transform.position.z <= boundary.zMin)
			transform.position = new Vector3 (transform.position.x, transform.position.y, boundary.zMin);
		else if(transform.position.z >= boundary.zMax)
			transform.position = new Vector3 (transform.position.x, transform.position.y, boundary.zMax);
	}
}
