using UnityEngine;
using System.Collections;
using System;

public class MainSceneController : MonoBehaviour {

	public GameObject prefab;
	public int numOfPrefabs;

	void Start(){
		Debug.Log("Start " + DateTime.Now.ToFileTime());
		for (int i = 0; i < numOfPrefabs; i++) {
			Instantiate(prefab, new Vector3(i * 2.0f, 0.5f, 0), Quaternion.identity);
		}
		Debug.Log("Start End " + DateTime.Now.ToFileTime());
	}

	void OnLevelWasLoaded(int level){
		Debug.Log("Loading is finished. " + DateTime.Now.ToFileTime());
	}
}
