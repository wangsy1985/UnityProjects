using UnityEngine;
using System.Collections;
using System;

public class StartSceneController : MonoBehaviour {

	private AsyncOperation async;

	private void LoadLevel(){
		//async = Application.LoadLevelAsync (1);
		//Load ();
		Application.LoadLevel (1);
	}

	IEnumerator Load(){
		yield return async;
	}

	// Update is called once per frame
	void Update () {
		if (Input.anyKey) {
			Debug.Log("Start Loading! " + DateTime.Now.ToFileTime());
			LoadLevel(); 
		}
	}
}
