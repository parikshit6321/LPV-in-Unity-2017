using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class LPVLighting : MonoBehaviour {

	public ComputeShader lpvCleanupShader = null;
	public ComputeShader lpvInjectionShader = null;
	public ComputeShader lpvPropagationShader = null;
	public Shader lpvRenderShader = null;
	public int lpvDimension = 32;
	public int propagationSteps = 15;
	public float worldVolumeBoundary = 50.0f;
	public float indirectLightStrength = 1.0f;

	private Material lpvRenderMaterial = null;
	private RenderTexture lightingTexture = null;
	private RenderTexture positionTexture = null;
	private RenderTexture normalTexture = null;

	public struct LPVCell {
		public Vector4 redSH;
		public Vector4 greenSH;
		public Vector4 blueSH;
		public float luminance;
		public int directionFlag;
	};

	private LPVCell[] lpvGridData = null;
	private ComputeBuffer lpvGridBuffer = null;

	private Camera[] cameras = null;
	public Camera rsmCamera = null;

	// Use this for initialization
	void Start () {

		GetComponent<Camera> ().depthTextureMode = DepthTextureMode.Depth | DepthTextureMode.DepthNormals;

		InitializeLPVGrid ();

		InitializeRSMCamera ();

		if (lpvRenderShader != null) {
			lpvRenderMaterial = new Material (lpvRenderShader);
		}

		lightingTexture = new RenderTexture (Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat);
		positionTexture = new RenderTexture (Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat);
		normalTexture = new RenderTexture (Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat);

	}

	// Function to create and set the compute buffer for the LPV grid
	private void InitializeLPVGrid () {

		lpvGridData = new LPVCell[lpvDimension * lpvDimension * lpvDimension];

		for (int i = 0; i < lpvDimension * lpvDimension * lpvDimension; ++i)
		{
			lpvGridData [i].redSH = Vector4.zero;
			lpvGridData [i].greenSH = Vector4.zero;
			lpvGridData [i].blueSH = Vector4.zero;
			lpvGridData [i].luminance = 0.0f;
			lpvGridData [i].directionFlag = 0;
		}

		lpvGridBuffer = new ComputeBuffer(lpvGridData.Length, 56);
		lpvGridBuffer.SetData(lpvGridData);

	}

	// Function to initialize the RSM camera
	private void InitializeRSMCamera () {

		cameras = Resources.FindObjectsOfTypeAll<Camera> ();

		for (int i = 0; i < cameras.Length; ++i) {
			if (cameras [i].GetComponent<RSMCameraScript> () != null) {
				rsmCamera = cameras [i];
				break;
			}
		}

		if (rsmCamera != null) {
			rsmCamera.GetComponent<RSMCameraScript> ().Initialize ();
		}

	}

	// Function to cleanup all the data stored in the LPV grid
	private void LPVGridCleanup () {

		int kernelHandle = lpvCleanupShader.FindKernel("CSMain");
		lpvCleanupShader.SetBuffer(kernelHandle, "lpvGridBuffer", lpvGridBuffer);
		lpvCleanupShader.SetInt("lpvDimension", lpvDimension);
		lpvCleanupShader.Dispatch(kernelHandle, lpvDimension, lpvDimension, lpvDimension);

	}

	// Function to inject the vpl data into LPV grid as spherical harmonics
	private void LPVGridInjection () {

		// RSM textures injection
		int kernelHandle = lpvInjectionShader.FindKernel("CSMain");
		lpvInjectionShader.SetBuffer(kernelHandle, "lpvGridBuffer", lpvGridBuffer);
		lpvInjectionShader.SetInt("lpvDimension", lpvDimension);
		lpvInjectionShader.SetFloat("worldVolumeBoundary", worldVolumeBoundary);
		lpvInjectionShader.SetTexture(kernelHandle, "lightingTexture", rsmCamera.GetComponent<RSMCameraScript>().lightingTexture);
		lpvInjectionShader.SetTexture(kernelHandle, "positionTexture", rsmCamera.GetComponent<RSMCameraScript>().positionTexture);
		lpvInjectionShader.SetTexture(kernelHandle, "normalTexture", rsmCamera.GetComponent<RSMCameraScript>().normalTexture);
		lpvInjectionShader.Dispatch(kernelHandle, rsmCamera.GetComponent<RSMCameraScript>().resolution.x, rsmCamera.GetComponent<RSMCameraScript>().resolution.y, 1);

		// Screen textures injection
		lpvInjectionShader.SetBuffer(kernelHandle, "lpvGridBuffer", lpvGridBuffer);
		lpvInjectionShader.SetInt("lpvDimension", lpvDimension);
		lpvInjectionShader.SetFloat("worldVolumeBoundary", worldVolumeBoundary);
		lpvInjectionShader.SetTexture(kernelHandle, "lightingTexture", lightingTexture);
		lpvInjectionShader.SetTexture(kernelHandle, "positionTexture", positionTexture);
		lpvInjectionShader.SetTexture(kernelHandle, "normalTexture", normalTexture);
		lpvInjectionShader.Dispatch(kernelHandle, Screen.width, Screen.height, 1);

	}

	// Function to propagate the lighting stored as spherical harmonics in the LPV grid to its neightbouring cells
	private void LPVGridPropagation () {

		int kernelHandle = lpvPropagationShader.FindKernel("CSMain");
		lpvPropagationShader.SetBuffer(kernelHandle, "lpvGridBuffer", lpvGridBuffer);
		lpvPropagationShader.SetInt("lpvDimension", lpvDimension);
		lpvPropagationShader.Dispatch(kernelHandle, lpvDimension, lpvDimension, lpvDimension);

	}

	// Called when the game object is destroyed
	void OnDestroy () {

		if (lpvGridBuffer != null) {
			lpvGridBuffer.Release ();
		}

	}

	// Called when the scene is rendered into the framebuffer
	void OnRenderImage (RenderTexture source, RenderTexture destination) {

		lpvRenderMaterial.SetMatrix ("InverseViewMatrix", GetComponent<Camera>().cameraToWorldMatrix);
		lpvRenderMaterial.SetMatrix ("InverseProjectionMatrix", GetComponent<Camera>().projectionMatrix.inverse);
		lpvRenderMaterial.SetFloat ("worldVolumeBoundary", worldVolumeBoundary);
		lpvRenderMaterial.SetFloat ("lpvDimension", lpvDimension);
		lpvRenderMaterial.SetFloat ("indirectLightStrength", indirectLightStrength);

		LPVGridCleanup ();

		Graphics.Blit (source, lightingTexture);
		Graphics.Blit (source, positionTexture, lpvRenderMaterial, 0);
		Graphics.Blit (source, normalTexture, lpvRenderMaterial, 1);

		if (rsmCamera != null) {
			rsmCamera.GetComponent<RSMCameraScript> ().RenderRSM ();
		}

		LPVGridInjection ();

		for (int i = 0; i < propagationSteps; ++i) {
			LPVGridPropagation ();
		}

		lpvRenderMaterial.SetTexture ("positionTexture", positionTexture);
		lpvRenderMaterial.SetTexture ("normalTexture", normalTexture);
		lpvRenderMaterial.SetBuffer ("lpvGridBuffer", lpvGridBuffer);

		Graphics.Blit (source, destination, lpvRenderMaterial, 2);

	}
}
