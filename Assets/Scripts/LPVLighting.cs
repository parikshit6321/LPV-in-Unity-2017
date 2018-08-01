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

	public RenderTexture lpvRedSH = null;
	public RenderTexture lpvGreenSH = null;
	public RenderTexture lpvBlueSH = null;
	public RenderTexture lpvLuminance = null;

	private Camera[] cameras = null;
	public Camera rsmCamera = null;

	// Use this for initialization
	void Start () {

		GetComponent<Camera> ().depthTextureMode = DepthTextureMode.Depth | DepthTextureMode.DepthNormals;

		InitializeLPVTextures ();

		InitializeRSMCamera ();

		if (lpvRenderShader != null) {
			lpvRenderMaterial = new Material (lpvRenderShader);
		}

		lightingTexture = new RenderTexture (Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat);
		positionTexture = new RenderTexture (Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat);
		normalTexture = new RenderTexture (Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat);

	}

	// Function to create the 3D LPV Textures
	private void InitializeLPVTextures () {

		RenderTextureDescriptor lpvTextureDescriptorSH = new RenderTextureDescriptor ();
		lpvTextureDescriptorSH.bindMS = false;
		lpvTextureDescriptorSH.colorFormat = RenderTextureFormat.ARGBFloat;
		lpvTextureDescriptorSH.depthBufferBits = 0;
		lpvTextureDescriptorSH.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
		lpvTextureDescriptorSH.enableRandomWrite = true;
		lpvTextureDescriptorSH.height = lpvDimension;
		lpvTextureDescriptorSH.msaaSamples = 1;
		lpvTextureDescriptorSH.volumeDepth = lpvDimension;
		lpvTextureDescriptorSH.width = lpvDimension;
		lpvTextureDescriptorSH.sRGB = true;

		RenderTextureDescriptor lpvTextureDescriptorLuminance = new RenderTextureDescriptor ();
		lpvTextureDescriptorLuminance.bindMS = false;
		lpvTextureDescriptorLuminance.colorFormat = RenderTextureFormat.RFloat;
		lpvTextureDescriptorLuminance.depthBufferBits = 0;
		lpvTextureDescriptorLuminance.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
		lpvTextureDescriptorLuminance.enableRandomWrite = true;
		lpvTextureDescriptorLuminance.height = lpvDimension;
		lpvTextureDescriptorLuminance.msaaSamples = 1;
		lpvTextureDescriptorLuminance.volumeDepth = lpvDimension;
		lpvTextureDescriptorLuminance.width = lpvDimension;
		lpvTextureDescriptorLuminance.sRGB = true;

		lpvRedSH = new RenderTexture (lpvTextureDescriptorSH);
		lpvGreenSH = new RenderTexture (lpvTextureDescriptorSH);
		lpvBlueSH = new RenderTexture (lpvTextureDescriptorSH);
		lpvLuminance = new RenderTexture (lpvTextureDescriptorLuminance);
	
		lpvRedSH.filterMode = FilterMode.Trilinear;
		lpvGreenSH.filterMode = FilterMode.Trilinear;
		lpvBlueSH.filterMode = FilterMode.Trilinear;
		lpvLuminance.filterMode = FilterMode.Trilinear;

		lpvRedSH.Create ();
		lpvGreenSH.Create ();
		lpvBlueSH.Create ();
		lpvLuminance.Create ();

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
		lpvCleanupShader.SetTexture(kernelHandle, "lpvRedSH", lpvRedSH);
		lpvCleanupShader.SetTexture(kernelHandle, "lpvGreenSH", lpvGreenSH);
		lpvCleanupShader.SetTexture(kernelHandle, "lpvBlueSH", lpvBlueSH);
		lpvCleanupShader.SetTexture(kernelHandle, "lpvLuminance", lpvLuminance);
		lpvCleanupShader.Dispatch(kernelHandle, lpvDimension, lpvDimension, lpvDimension);

	}

	// Function to inject the vpl data into LPV grid as spherical harmonics
	private void LPVGridInjection () {

		// RSM textures injection
		int kernelHandle = lpvInjectionShader.FindKernel("CSMain");
		lpvInjectionShader.SetTexture(kernelHandle, "lpvRedSH", lpvRedSH);
		lpvInjectionShader.SetTexture(kernelHandle, "lpvGreenSH", lpvGreenSH);
		lpvInjectionShader.SetTexture(kernelHandle, "lpvBlueSH", lpvBlueSH);
		lpvInjectionShader.SetTexture(kernelHandle, "lpvLuminance", lpvLuminance);
		lpvInjectionShader.SetInt("lpvDimension", lpvDimension);
		lpvInjectionShader.SetFloat("worldVolumeBoundary", worldVolumeBoundary);
		lpvInjectionShader.SetTexture(kernelHandle, "lightingTexture", rsmCamera.GetComponent<RSMCameraScript>().lightingTexture);
		lpvInjectionShader.SetTexture(kernelHandle, "positionTexture", rsmCamera.GetComponent<RSMCameraScript>().positionTexture);
		lpvInjectionShader.SetTexture(kernelHandle, "normalTexture", rsmCamera.GetComponent<RSMCameraScript>().normalTexture);
		lpvInjectionShader.Dispatch(kernelHandle, rsmCamera.GetComponent<RSMCameraScript>().resolution.x, rsmCamera.GetComponent<RSMCameraScript>().resolution.y, 1);

		// Screen textures injection
		lpvInjectionShader.SetTexture(kernelHandle, "lpvRedSH", lpvRedSH);
		lpvInjectionShader.SetTexture(kernelHandle, "lpvGreenSH", lpvGreenSH);
		lpvInjectionShader.SetTexture(kernelHandle, "lpvBlueSH", lpvBlueSH);
		lpvInjectionShader.SetTexture(kernelHandle, "lpvLuminance", lpvLuminance);
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
		lpvPropagationShader.SetTexture(kernelHandle, "lpvRedSH", lpvRedSH);
		lpvPropagationShader.SetTexture(kernelHandle, "lpvGreenSH", lpvGreenSH);
		lpvPropagationShader.SetTexture(kernelHandle, "lpvBlueSH", lpvBlueSH);
		lpvPropagationShader.SetTexture(kernelHandle, "lpvLuminance", lpvLuminance);
		lpvPropagationShader.SetInt("lpvDimension", lpvDimension);
		lpvPropagationShader.Dispatch(kernelHandle, lpvDimension, lpvDimension, lpvDimension);

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
		lpvRenderMaterial.SetTexture ("lpvRedSH", lpvRedSH);
		lpvRenderMaterial.SetTexture ("lpvGreenSH", lpvGreenSH);
		lpvRenderMaterial.SetTexture ("lpvBlueSH", lpvBlueSH);

		Graphics.Blit (source, destination, lpvRenderMaterial, 2);

	}
}