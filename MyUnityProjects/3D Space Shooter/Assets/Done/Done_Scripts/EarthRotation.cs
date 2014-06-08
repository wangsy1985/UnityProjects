using UnityEngine;
using System.Collections;

public class EarthRotation : MonoBehaviour {

	// Use this for initialization
	void Start () {
		rigidbody.angularVelocity = new Vector3 (0.0f, 0.5f, 0.0f);
	}
}
