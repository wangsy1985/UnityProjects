using UnityEngine;
using System.Collections;

public class SkyBoxController : MonoBehaviour {

	public GameObject parentObject;

	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
		//transform.position = parentObject.transform.position;
		transform.Rotate (new Vector3(0.0f, 0.0f, 0.1f));
	}
}
