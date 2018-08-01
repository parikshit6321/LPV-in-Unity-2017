using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResolutionInitializer : MonoBehaviour {

	public Vector2Int resolution = Vector2Int.zero;

	// Use this for initialization
	void Start () {
		Screen.SetResolution (resolution.x, resolution.y, true);
	}

}
