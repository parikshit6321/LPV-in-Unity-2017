﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RSMCameraScript : MonoBehaviour {

	public Vector2Int resolution = Vector2Int.zero;
	public Shader positionShader = null;
	public Shader normalShader = null;


	public RenderTexture lightingTexture = null;

	public RenderTexture positionTexture = null;

	public RenderTexture normalTexture = null;

	// Use this for initialization
	public void Initialize () {

		lightingTexture = new RenderTexture (resolution.x, resolution.y, 32, RenderTextureFormat.ARGBFloat);
		positionTexture = new RenderTexture (resolution.x, resolution.y, 32, RenderTextureFormat.ARGBFloat);
		normalTexture = new RenderTexture (resolution.x, resolution.y, 32, RenderTextureFormat.ARGBFloat);

		GetComponent<Camera> ().depthTextureMode = DepthTextureMode.Depth;

	}
	
	// Use this to render the RSM textures
	public void RenderRSM () {

		GetComponent<Camera> ().targetTexture = lightingTexture;
		GetComponent<Camera> ().Render ();

		GetComponent<Camera> ().targetTexture = positionTexture;
		GetComponent<Camera> ().RenderWithShader (positionShader, null);

		GetComponent<Camera> ().targetTexture = normalTexture;
		GetComponent<Camera> ().RenderWithShader (normalShader, null);
	}
}