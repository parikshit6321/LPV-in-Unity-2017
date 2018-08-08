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
		public RenderTexture lpvRedSHBackBuffer;
		public RenderTexture lpvGreenSHBackBuffer;
		public RenderTexture lpvBlueSHBackBuffer;
		public RenderTexture lpvLuminanceBackBuffer;
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
	private List<Camera> rsmCameras =null;

	private RenderTextureDescriptor lpvTextureDescriptorSH;
	private RenderTextureDescriptor lpvTextureDescriptorLuminance;

	private bool bDisplayBackBuffer = false;
	private int currentPropagationStep = 0;

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

		cascade.lpvRedSHBackBuffer = new RenderTexture (lpvTextureDescriptorSH);
		cascade.lpvGreenSHBackBuffer = new RenderTexture (lpvTextureDescriptorSH);
		cascade.lpvBlueSHBackBuffer = new RenderTexture (lpvTextureDescriptorSH);
		cascade.lpvLuminanceBackBuffer = new RenderTexture (lpvTextureDescriptorLuminance);

		cascade.lpvRedSH.filterMode = FilterMode.Trilinear;
		cascade.lpvGreenSH.filterMode = FilterMode.Trilinear;
		cascade.lpvBlueSH.filterMode = FilterMode.Trilinear;
		cascade.lpvLuminance.filterMode = FilterMode.Trilinear;
		cascade.lpvRedSHBackBuffer.filterMode = FilterMode.Trilinear;
		cascade.lpvGreenSHBackBuffer.filterMode = FilterMode.Trilinear;
		cascade.lpvBlueSHBackBuffer.filterMode = FilterMode.Trilinear;
		cascade.lpvLuminanceBackBuffer.filterMode = FilterMode.Trilinear;

		cascade.lpvRedSH.Create ();
		cascade.lpvGreenSH.Create ();
		cascade.lpvBlueSH.Create ();
		cascade.lpvLuminance.Create ();
		cascade.lpvRedSHBackBuffer.Create ();
		cascade.lpvGreenSHBackBuffer.Create ();
		cascade.lpvBlueSHBackBuffer.Create ();
		cascade.lpvLuminanceBackBuffer.Create ();

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

		LPVGridCleanup (ref firstCascade);
		LPVGridCleanup (ref secondCascade);
		LPVGridCleanup (ref thirdCascade);
	}

	// Function to initialize the RSM camera
	private void InitializeRSMCamera () {

		cameras = Resources.FindObjectsOfTypeAll<Camera> ();
		rsmCameras = new List<Camera> ();

		for (int i = 0; i < cameras.Length; ++i) {
			if (cameras [i].GetComponent<RSMCameraScript> () != null) {
				rsmCameras.Add (cameras [i]);
			}
		}

		for (int i = 0; i < rsmCameras.Count; ++i) {
			if (rsmCameras[i] != null) {
				rsmCameras[i].GetComponent<RSMCameraScript> ().Initialize ();
			}
		}


	}

	// Function to cleanup all the data stored in the LPV grid
	private void LPVGridCleanup (ref LPVCascade cascade) {

		int kernelHandle = lpvCleanupShader.FindKernel("CSMain");

		if (bDisplayBackBuffer) {
			lpvCleanupShader.SetTexture(kernelHandle, "lpvRedSH", cascade.lpvRedSH);
			lpvCleanupShader.SetTexture(kernelHandle, "lpvGreenSH", cascade.lpvGreenSH);
			lpvCleanupShader.SetTexture(kernelHandle, "lpvBlueSH", cascade.lpvBlueSH);
			lpvCleanupShader.SetTexture(kernelHandle, "lpvLuminance", cascade.lpvLuminance);
		} else {
			lpvCleanupShader.SetTexture(kernelHandle, "lpvRedSH", cascade.lpvRedSHBackBuffer);
			lpvCleanupShader.SetTexture(kernelHandle, "lpvGreenSH", cascade.lpvGreenSHBackBuffer);
			lpvCleanupShader.SetTexture(kernelHandle, "lpvBlueSH", cascade.lpvBlueSHBackBuffer);
			lpvCleanupShader.SetTexture(kernelHandle, "lpvLuminance", cascade.lpvLuminanceBackBuffer);
		}

		lpvCleanupShader.Dispatch(kernelHandle, lpvDimension, lpvDimension, lpvDimension);

	}
	 
	// Function to inject the vpl data into LPV grid as spherical harmonics
	private void LPVGridInjection (ref LPVCascade cascade, float cascadeBoundary) {
		
		int kernelHandle = lpvInjectionShader.FindKernel("CSMain");

		if (rsmVPLInjection) {

			for (int i = 0; i < rsmCameras.Count; ++i) {
			
				if (rsmCameras [i] != null) {

					// RSM textures injection
					if (bDisplayBackBuffer) {
						lpvInjectionShader.SetTexture(kernelHandle, "lpvRedSH", cascade.lpvRedSH);
						lpvInjectionShader.SetTexture(kernelHandle, "lpvGreenSH", cascade.lpvGreenSH);
						lpvInjectionShader.SetTexture(kernelHandle, "lpvBlueSH", cascade.lpvBlueSH);
						lpvInjectionShader.SetTexture(kernelHandle, "lpvLuminance", cascade.lpvLuminance);
					} else {
						lpvInjectionShader.SetTexture(kernelHandle, "lpvRedSH", cascade.lpvRedSHBackBuffer);
						lpvInjectionShader.SetTexture(kernelHandle, "lpvGreenSH", cascade.lpvGreenSHBackBuffer);
						lpvInjectionShader.SetTexture(kernelHandle, "lpvBlueSH", cascade.lpvBlueSHBackBuffer);
						lpvInjectionShader.SetTexture(kernelHandle, "lpvLuminance", cascade.lpvLuminanceBackBuffer);
					}

					lpvInjectionShader.SetInt("lpvDimension", lpvDimension);
					lpvInjectionShader.SetFloat("worldVolumeBoundary", cascadeBoundary);
					lpvInjectionShader.SetTexture(kernelHandle, "lightingTexture", rsmCameras[i].GetComponent<RSMCameraScript>().lightingTexture);
					lpvInjectionShader.SetTexture(kernelHandle, "positionTexture", rsmCameras[i].GetComponent<RSMCameraScript>().positionTexture);
					lpvInjectionShader.SetTexture(kernelHandle, "normalTexture", rsmCameras[i].GetComponent<RSMCameraScript>().normalTexture);
					lpvInjectionShader.Dispatch(kernelHandle, rsmCameras[i].GetComponent<RSMCameraScript>().resolution.x, rsmCameras[i].GetComponent<RSMCameraScript>().resolution.y, 1);
				
				}

			}

		}

		if (screenSpaceVPLInjection) {

			// Screen textures injection
			// RSM textures injection
			if (bDisplayBackBuffer) {
				lpvInjectionShader.SetTexture(kernelHandle, "lpvRedSH", cascade.lpvRedSH);
				lpvInjectionShader.SetTexture(kernelHandle, "lpvGreenSH", cascade.lpvGreenSH);
				lpvInjectionShader.SetTexture(kernelHandle, "lpvBlueSH", cascade.lpvBlueSH);
				lpvInjectionShader.SetTexture(kernelHandle, "lpvLuminance", cascade.lpvLuminance);
			} else {
				lpvInjectionShader.SetTexture(kernelHandle, "lpvRedSH", cascade.lpvRedSHBackBuffer);
				lpvInjectionShader.SetTexture(kernelHandle, "lpvGreenSH", cascade.lpvGreenSHBackBuffer);
				lpvInjectionShader.SetTexture(kernelHandle, "lpvBlueSH", cascade.lpvBlueSHBackBuffer);
				lpvInjectionShader.SetTexture(kernelHandle, "lpvLuminance", cascade.lpvLuminanceBackBuffer);
			}
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

		if (bDisplayBackBuffer) {
			lpvPropagationShader.SetTexture(kernelHandle, "lpvRedSH", cascade.lpvRedSH);
			lpvPropagationShader.SetTexture(kernelHandle, "lpvGreenSH", cascade.lpvGreenSH);
			lpvPropagationShader.SetTexture(kernelHandle, "lpvBlueSH", cascade.lpvBlueSH);
		} else {
			lpvPropagationShader.SetTexture(kernelHandle, "lpvRedSH", cascade.lpvRedSHBackBuffer);
			lpvPropagationShader.SetTexture(kernelHandle, "lpvGreenSH", cascade.lpvGreenSHBackBuffer);
			lpvPropagationShader.SetTexture(kernelHandle, "lpvBlueSH", cascade.lpvBlueSHBackBuffer);
		}

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
		lpvRenderMaterial.SetVector ("playerPosition", this.transform.position);

		Graphics.Blit (source, lightingTexture);
		Graphics.Blit (source, positionTexture, lpvRenderMaterial, 0);
		Graphics.Blit (source, normalTexture, lpvRenderMaterial, 1);

		for (int i = 0; i < rsmCameras.Count; ++i) {
			if (rsmCameras[i] != null) {
				if (rsmVPLInjection) {
					rsmCameras[i].GetComponent<RSMCameraScript> ().RenderRSM ();
				}
			}
		}

		LPVGridInjection (ref firstCascade, firstCascadeBoundary);
		LPVGridInjection (ref secondCascade, secondCascadeBoundary);
		LPVGridInjection (ref thirdCascade, thirdCascadeBoundary);

		LPVGridPropagation (ref firstCascade);
		LPVGridPropagation (ref secondCascade);
		LPVGridPropagation (ref thirdCascade);

		++currentPropagationStep;

		if (currentPropagationStep >= propagationSteps) {
			currentPropagationStep = 0;
			bDisplayBackBuffer = !bDisplayBackBuffer;
			LPVGridCleanup (ref firstCascade);
			LPVGridCleanup (ref secondCascade);
			LPVGridCleanup (ref thirdCascade);
		}

		lpvRenderMaterial.SetTexture ("positionTexture", positionTexture);
		lpvRenderMaterial.SetTexture ("normalTexture", normalTexture);

		if (bDisplayBackBuffer) {
			lpvRenderMaterial.SetTexture ("lpvRedSHFirstCascade", firstCascade.lpvRedSHBackBuffer);
			lpvRenderMaterial.SetTexture ("lpvGreenSHFirstCascade", firstCascade.lpvGreenSHBackBuffer);
			lpvRenderMaterial.SetTexture ("lpvBlueSHFirstCascade", firstCascade.lpvBlueSHBackBuffer);

			lpvRenderMaterial.SetTexture ("lpvRedSHSecondCascade", secondCascade.lpvRedSHBackBuffer);
			lpvRenderMaterial.SetTexture ("lpvGreenSHSecondCascade", secondCascade.lpvGreenSHBackBuffer);
			lpvRenderMaterial.SetTexture ("lpvBlueSHSecondCascade", secondCascade.lpvBlueSHBackBuffer);

			lpvRenderMaterial.SetTexture ("lpvRedSHThirdCascade", thirdCascade.lpvRedSHBackBuffer);
			lpvRenderMaterial.SetTexture ("lpvGreenSHThirdCascade", thirdCascade.lpvGreenSHBackBuffer);
			lpvRenderMaterial.SetTexture ("lpvBlueSHThirdCascade", thirdCascade.lpvBlueSHBackBuffer);
		} else {
			lpvRenderMaterial.SetTexture ("lpvRedSHFirstCascade", firstCascade.lpvRedSH);
			lpvRenderMaterial.SetTexture ("lpvGreenSHFirstCascade", firstCascade.lpvGreenSH);
			lpvRenderMaterial.SetTexture ("lpvBlueSHFirstCascade", firstCascade.lpvBlueSH);

			lpvRenderMaterial.SetTexture ("lpvRedSHSecondCascade", secondCascade.lpvRedSH);
			lpvRenderMaterial.SetTexture ("lpvGreenSHSecondCascade", secondCascade.lpvGreenSH);
			lpvRenderMaterial.SetTexture ("lpvBlueSHSecondCascade", secondCascade.lpvBlueSH);

			lpvRenderMaterial.SetTexture ("lpvRedSHThirdCascade", thirdCascade.lpvRedSH);
			lpvRenderMaterial.SetTexture ("lpvGreenSHThirdCascade", thirdCascade.lpvGreenSH);
			lpvRenderMaterial.SetTexture ("lpvBlueSHThirdCascade", thirdCascade.lpvBlueSH);
		}

		Graphics.Blit (source, destination, lpvRenderMaterial, 2);

	}
}