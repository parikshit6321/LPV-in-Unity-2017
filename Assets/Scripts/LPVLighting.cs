using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class LPVLighting : MonoBehaviour {
	
	private struct LPVCascade {
		public RenderTexture lpvRedSH;
		public RenderTexture lpvGreenSH;
		public RenderTexture lpvBlueSH;
		public RenderTexture lpvLuminance;
	};

	public ComputeShader lpvCleanupShader = null;
	public ComputeShader lpvInjectionShader = null;
	public ComputeShader lpvPropagationShader = null;
	public Shader lpvRenderShader = null;
	public bool screenSpaceVPLInjection = true;
	public bool rsmVPLInjection = true;
	public int lpvDimension = 32;
	public int propagationSteps = 15;
	public float firstCascadeBoundary = 50.0f;
	public float secondCascadeBoundary = 100.0f;
	public float thirdCascadeBoundary = 200.0f;
	public float indirectLightStrength = 1.0f;

	private Material lpvRenderMaterial = null;
	private RenderTexture lightingTexture = null;
	private RenderTexture positionTexture = null;
	private RenderTexture normalTexture = null;

	private LPVCascade firstCascade;
	private LPVCascade secondCascade;
	private LPVCascade thirdCascade;

	private Camera[] cameras = null;
	private Camera rsmCamera = null;

	private RenderTextureDescriptor lpvTextureDescriptorSH;
	private RenderTextureDescriptor lpvTextureDescriptorLuminance;

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

	// Function to initialize an LPV cascade
	private void InitializeLPVCascade (ref LPVCascade cascade) {

		cascade.lpvRedSH = new RenderTexture (lpvTextureDescriptorSH);
		cascade.lpvGreenSH = new RenderTexture (lpvTextureDescriptorSH);
		cascade.lpvBlueSH = new RenderTexture (lpvTextureDescriptorSH);
		cascade.lpvLuminance = new RenderTexture (lpvTextureDescriptorLuminance);

		cascade.lpvRedSH.filterMode = FilterMode.Trilinear;
		cascade.lpvGreenSH.filterMode = FilterMode.Trilinear;
		cascade.lpvBlueSH.filterMode = FilterMode.Trilinear;
		cascade.lpvLuminance.filterMode = FilterMode.Trilinear;

		cascade.lpvRedSH.Create ();
		cascade.lpvGreenSH.Create ();
		cascade.lpvBlueSH.Create ();
		cascade.lpvLuminance.Create ();

	}

	// Function to create the 3D LPV Textures
	private void InitializeLPVTextures () {

		lpvTextureDescriptorSH = new RenderTextureDescriptor ();
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

		lpvTextureDescriptorLuminance = new RenderTextureDescriptor ();
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

		InitializeLPVCascade (ref firstCascade);
		InitializeLPVCascade (ref secondCascade);
		InitializeLPVCascade (ref thirdCascade);

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
	private void LPVGridCleanup (ref LPVCascade cascade) {

		int kernelHandle = lpvCleanupShader.FindKernel("CSMain");
		lpvCleanupShader.SetTexture(kernelHandle, "lpvRedSH", cascade.lpvRedSH);
		lpvCleanupShader.SetTexture(kernelHandle, "lpvGreenSH", cascade.lpvGreenSH);
		lpvCleanupShader.SetTexture(kernelHandle, "lpvBlueSH", cascade.lpvBlueSH);
		lpvCleanupShader.SetTexture(kernelHandle, "lpvLuminance", cascade.lpvLuminance);
		lpvCleanupShader.Dispatch(kernelHandle, lpvDimension, lpvDimension, lpvDimension);

	}
	 
	// Function to inject the vpl data into LPV grid as spherical harmonics
	private void LPVGridInjection (ref LPVCascade cascade, float cascadeBoundary) {
		
		int kernelHandle = lpvInjectionShader.FindKernel("CSMain");

		if (rsmVPLInjection) {
			// RSM textures injection
			lpvInjectionShader.SetTexture(kernelHandle, "lpvRedSH", cascade.lpvRedSH);
			lpvInjectionShader.SetTexture(kernelHandle, "lpvGreenSH", cascade.lpvGreenSH);
			lpvInjectionShader.SetTexture(kernelHandle, "lpvBlueSH", cascade.lpvBlueSH);
			lpvInjectionShader.SetTexture(kernelHandle, "lpvLuminance", cascade.lpvLuminance);
			lpvInjectionShader.SetInt("lpvDimension", lpvDimension);
			lpvInjectionShader.SetFloat("worldVolumeBoundary", cascadeBoundary);
			lpvInjectionShader.SetTexture(kernelHandle, "lightingTexture", rsmCamera.GetComponent<RSMCameraScript>().lightingTexture);
			lpvInjectionShader.SetTexture(kernelHandle, "positionTexture", rsmCamera.GetComponent<RSMCameraScript>().positionTexture);
			lpvInjectionShader.SetTexture(kernelHandle, "normalTexture", rsmCamera.GetComponent<RSMCameraScript>().normalTexture);
			lpvInjectionShader.Dispatch(kernelHandle, rsmCamera.GetComponent<RSMCameraScript>().resolution.x, rsmCamera.GetComponent<RSMCameraScript>().resolution.y, 1);
		}

		if (screenSpaceVPLInjection) {
			// Screen textures injection
			lpvInjectionShader.SetTexture(kernelHandle, "lpvRedSH", cascade.lpvRedSH);
			lpvInjectionShader.SetTexture(kernelHandle, "lpvGreenSH", cascade.lpvGreenSH);
			lpvInjectionShader.SetTexture(kernelHandle, "lpvBlueSH", cascade.lpvBlueSH);
			lpvInjectionShader.SetTexture(kernelHandle, "lpvLuminance", cascade.lpvLuminance);
			lpvInjectionShader.SetInt("lpvDimension", lpvDimension);
			lpvInjectionShader.SetFloat("cascadeBoundary", cascadeBoundary);
			lpvInjectionShader.SetTexture(kernelHandle, "lightingTexture", lightingTexture);
			lpvInjectionShader.SetTexture(kernelHandle, "positionTexture", positionTexture);
			lpvInjectionShader.SetTexture(kernelHandle, "normalTexture", normalTexture);
			lpvInjectionShader.Dispatch(kernelHandle, Screen.width, Screen.height, 1);
		}

	}

	// Function to propagate the lighting stored as spherical harmonics in the LPV grid to its neightbouring cells
	private void LPVGridPropagation (ref LPVCascade cascade) {

		int kernelHandle = lpvPropagationShader.FindKernel("CSMain");
		lpvPropagationShader.SetTexture(kernelHandle, "lpvRedSH", cascade.lpvRedSH);
		lpvPropagationShader.SetTexture(kernelHandle, "lpvGreenSH", cascade.lpvGreenSH);
		lpvPropagationShader.SetTexture(kernelHandle, "lpvBlueSH", cascade.lpvBlueSH);
		lpvPropagationShader.SetTexture(kernelHandle, "lpvLuminance", cascade.lpvLuminance);
		lpvPropagationShader.SetInt("lpvDimension", lpvDimension);
		lpvPropagationShader.Dispatch(kernelHandle, lpvDimension, lpvDimension, lpvDimension);

	}

	// Called when the scene is rendered into the framebuffer
	void OnRenderImage (RenderTexture source, RenderTexture destination) {

		lpvRenderMaterial.SetMatrix ("InverseViewMatrix", GetComponent<Camera>().cameraToWorldMatrix);
		lpvRenderMaterial.SetMatrix ("InverseProjectionMatrix", GetComponent<Camera>().projectionMatrix.inverse);
		lpvRenderMaterial.SetFloat ("firstCascadeBoundary", firstCascadeBoundary);
		lpvRenderMaterial.SetFloat ("secondCascadeBoundary", secondCascadeBoundary);
		lpvRenderMaterial.SetFloat ("thirdCascadeBoundary", thirdCascadeBoundary);
		lpvRenderMaterial.SetFloat ("lpvDimension", lpvDimension);
		lpvRenderMaterial.SetFloat ("indirectLightStrength", indirectLightStrength);

		LPVGridCleanup (ref firstCascade);
		LPVGridCleanup (ref secondCascade);
		LPVGridCleanup (ref thirdCascade);

		Graphics.Blit (source, lightingTexture);
		Graphics.Blit (source, positionTexture, lpvRenderMaterial, 0);
		Graphics.Blit (source, normalTexture, lpvRenderMaterial, 1);

		if (rsmCamera != null) {
			if (rsmVPLInjection) {
				rsmCamera.GetComponent<RSMCameraScript> ().RenderRSM ();
			}
		}

		LPVGridInjection (ref firstCascade, firstCascadeBoundary);
		LPVGridInjection (ref secondCascade, secondCascadeBoundary);
		LPVGridInjection (ref thirdCascade, thirdCascadeBoundary);

		for (int i = 0; i < propagationSteps; ++i) {
			LPVGridPropagation (ref firstCascade);
			LPVGridPropagation (ref secondCascade);
			LPVGridPropagation (ref thirdCascade);
		}

		lpvRenderMaterial.SetTexture ("positionTexture", positionTexture);
		lpvRenderMaterial.SetTexture ("normalTexture", normalTexture);

		lpvRenderMaterial.SetTexture ("lpvRedSHFirstCascade", firstCascade.lpvRedSH);
		lpvRenderMaterial.SetTexture ("lpvGreenSHFirstCascade", firstCascade.lpvGreenSH);
		lpvRenderMaterial.SetTexture ("lpvBlueSHFirstCascade", firstCascade.lpvBlueSH);

		lpvRenderMaterial.SetTexture ("lpvRedSHSecondCascade", secondCascade.lpvRedSH);
		lpvRenderMaterial.SetTexture ("lpvGreenSHSecondCascade", secondCascade.lpvGreenSH);
		lpvRenderMaterial.SetTexture ("lpvBlueSHSecondCascade", secondCascade.lpvBlueSH);

		lpvRenderMaterial.SetTexture ("lpvRedSHThirdCascade", thirdCascade.lpvRedSH);
		lpvRenderMaterial.SetTexture ("lpvGreenSHThirdCascade", thirdCascade.lpvGreenSH);
		lpvRenderMaterial.SetTexture ("lpvBlueSHThirdCascade", thirdCascade.lpvBlueSH);

		Graphics.Blit (source, destination, lpvRenderMaterial, 2);

	}
}