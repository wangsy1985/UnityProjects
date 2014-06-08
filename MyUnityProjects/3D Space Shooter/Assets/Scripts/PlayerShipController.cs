using UnityEngine;
using System.Collections;

public class PlayerShipController : MonoBehaviour {

	public GameObject shot;
	public Transform spawn;
	public float fireRate;

	private float nextFire;
	private int mode;
	private float nextMode;

	void Start(){
		nextFire = 0;
		mode = 0;
	}

	void Update(){
		if (Input.GetButton ("A") && Time.time > nextFire) {
			nextFire = Time.time + fireRate;
			Instantiate (shot, spawn.position, spawn.rotation);
			audio.Play ();
		} 
		//視角を切り換えるテスト
		/*
		else if (Input.GetButton ("B") && Time.time > nextMode) {
			Debug.Log("B, transform.position.z = " + transform.position.z + "; mode = " + mode);
			if(mode % 3 == 0)
				transform.position = new Vector3(transform.position.x, transform.position.y, transform.position.z + 1.3f);
			else if(mode % 3 == 1)
				transform.position = new Vector3(transform.position.x, transform.position.y, transform.position.z - 2.08f);
			else if(mode % 3 == 2)
				transform.position = new Vector3(transform.position.x, transform.position.y, transform.position.z + 0.78f);
			mode++;
			nextMode = Time.time + 0.3f;
		}
		*/
	}
}
