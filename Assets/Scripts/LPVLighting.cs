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
		public RenderTexture lpvRedPropagationBuffer;
		public RenderTexture lpvGreenPropagationBuffer;
		public RenderTexture lpvBluePropagationBuffer;
	};

	[Header("Shaders")]
	public ComputeShader lpvCleanupShader = null;
	public ComputeShader lpvInjectionShader = null;
	public ComputeShader lpvPropagationShader = null;
	public ComputeShader lpvPropagationCompositionShader = null;
	public Shader lpvRenderShader = null;

	[Header("LPV Settings")]
	public bool backBuffering = true;
	public bool rsmVPLInjection = true;
	public bool screenSpaceVPLInjection = false;
	public Vector2Int screenSpaceVPLTextureResolution = Vector2Int.zero;
	public int lpvDimension = 32;
	public int propagationSteps = 14;
	public float firstCascadeBoundary = 50.0f;
	public float secondCascadeBoundary = 100.0f;
	public float thirdCascadeBoundary = 200.0f;

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

		lightingTexture = new RenderTexture (screenSpaceVPLTextureResolution.x, screenSpaceVPLTextureResolution.y, 0, RenderTextureFormat.ARGBFloat);
		positionTexture = new RenderTexture (screenSpaceVPLTextureResolution.x, screenSpaceVPLTextureResolution.y, 0, RenderTextureFormat.ARGBFloat);
		normalTexture = new RenderTexture (screenSpaceVPLTextureResolution.x, screenSpaceVPLTextureResolution.y, 0, RenderTextureFormat.ARGBFloat);

	}

	// Function to initialize an LPV cascade
	private void InitializeLPVCascade (ref LPVCascade cascade) {

		cascade.lpvRedSH = new RenderTexture (lpvTextureDescriptorSH);
		cascade.lpvGreenSH = new RenderTexture (lpvTextureDescriptorSH);
		cascade.lpvBlueSH = new RenderTexture (lpvTextureDescriptorSH);
		cascade.lpvLuminance = new RenderTexture (lpvTextureDescriptorLuminance);

		cascade.lpvRedPropagationBuffer = new RenderTexture (lpvTextureDescriptorSH);
		cascade.lpvGreenPropagationBuffer = new RenderTexture (lpvTextureDescriptorSH);
		cascade.lpvBluePropagationBuffer = new RenderTexture (lpvTextureDescriptorSH);

		cascade.lpvRedSHBackBuffer = new RenderTexture (lpvTextureDescriptorSH);
		cascade.lpvGreenSHBackBuffer = new RenderTexture (lpvTextureDescriptorSH);
		cascade.lpvBlueSHBackBuffer = new RenderTexture (lpvTextureDescriptorSH);
		cascade.lpvLuminanceBackBuffer = new RenderTexture (lpvTextureDescriptorLuminance);

		cascade.lpvRedSH.filterMode = FilterMode.Trilinear;
		cascade.lpvGreenSH.filterMode = FilterMode.Trilinear;
		cascade.lpvBlueSH.filterMode = FilterMode.Trilinear;
		cascade.lpvLuminance.filterMode = FilterMode.Trilinear;

		cascade.lpvRedPropagationBuffer.filterMode = FilterMode.Trilinear;
		cascade.lpvGreenPropagationBuffer.filterMode = FilterMode.Trilinear;
		cascade.lpvBluePropagationBuffer.filterMode = FilterMode.Trilinear;

		cascade.lpvRedSHBackBuffer.filterMode = FilterMode.Trilinear;
		cascade.lpvGreenSHBackBuffer.filterMode = FilterMode.Trilinear;
		cascade.lpvBlueSHBackBuffer.filterMode = FilterMode.Trilinear;
		cascade.lpvLuminanceBackBuffer.filterMode = FilterMode.Trilinear;

		cascade.lpvRedSH.Create ();
		cascade.lpvGreenSH.Create ();
		cascade.lpvBlueSH.Create ();
		cascade.lpvLuminance.Create ();

		cascade.lpvRedPropagationBuffer.Create ();
		cascade.lpvGreenPropagationBuffer.Create ();
		cascade.lpvBluePropagationBuffer.Create ();

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

		if (backBuffering) {
			if (bDisplayBackBuffer) {
				lpvCleanupShader.SetTexture (kernelHandle, "lpvRedSH", cascade.lpvRedSH);
				lpvCleanupShader.SetTexture (kernelHandle, "lpvGreenSH", cascade.lpvGreenSH);
				lpvCleanupShader.SetTexture (kernelHandle, "lpvBlueSH", cascade.lpvBlueSH);
				lpvCleanupShader.SetTexture (kernelHandle, "lpvLuminance", cascade.lpvLuminance);
			} else {
				lpvCleanupShader.SetTexture (kernelHandle, "lpvRedSH", cascade.lpvRedSHBackBuffer);
				lpvCleanupShader.SetTexture (kernelHandle, "lpvGreenSH", cascade.lpvGreenSHBackBuffer);
				lpvCleanupShader.SetTexture (kernelHandle, "lpvBlueSH", cascade.lpvBlueSHBackBuffer);
				lpvCleanupShader.SetTexture (kernelHandle, "lpvLuminance", cascade.lpvLuminanceBackBuffer);
			}
		} else {
			lpvCleanupShader.SetTexture (kernelHandle, "lpvRedSH", cascade.lpvRedSH);
			lpvCleanupShader.SetTexture (kernelHandle, "lpvGreenSH", cascade.lpvGreenSH);
			lpvCleanupShader.SetTexture (kernelHandle, "lpvBlueSH", cascade.lpvBlueSH);
			lpvCleanupShader.SetTexture (kernelHandle, "lpvLuminance", cascade.lpvLuminance);
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
					if (backBuffering) {
						if (bDisplayBackBuffer) {
							lpvInjectionShader.SetTexture (kernelHandle, "lpvRedSH", cascade.lpvRedSH);
							lpvInjectionShader.SetTexture (kernelHandle, "lpvGreenSH", cascade.lpvGreenSH);
							lpvInjectionShader.SetTexture (kernelHandle, "lpvBlueSH", cascade.lpvBlueSH);
							lpvInjectionShader.SetTexture (kernelHandle, "lpvLuminance", cascade.lpvLuminance);
						} else {
							lpvInjectionShader.SetTexture (kernelHandle, "lpvRedSH", cascade.lpvRedSHBackBuffer);
							lpvInjectionShader.SetTexture (kernelHandle, "lpvGreenSH", cascade.lpvGreenSHBackBuffer);
							lpvInjectionShader.SetTexture (kernelHandle, "lpvBlueSH", cascade.lpvBlueSHBackBuffer);
							lpvInjectionShader.SetTexture (kernelHandle, "lpvLuminance", cascade.lpvLuminanceBackBuffer);
						}
					} else {
						lpvInjectionShader.SetTexture (kernelHandle, "lpvRedSH", cascade.lpvRedSH);
						lpvInjectionShader.SetTexture (kernelHandle, "lpvGreenSH", cascade.lpvGreenSH);
						lpvInjectionShader.SetTexture (kernelHandle, "lpvBlueSH", cascade.lpvBlueSH);
						lpvInjectionShader.SetTexture (kernelHandle, "lpvLuminance", cascade.lpvLuminance);
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
			if (backBuffering) {
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
			} else {
				lpvInjectionShader.SetTexture(kernelHandle, "lpvRedSH", cascade.lpvRedSH);
				lpvInjectionShader.SetTexture(kernelHandle, "lpvGreenSH", cascade.lpvGreenSH);
				lpvInjectionShader.SetTexture(kernelHandle, "lpvBlueSH", cascade.lpvBlueSH);
				lpvInjectionShader.SetTexture(kernelHandle, "lpvLuminance", cascade.lpvLuminance);
			}

			lpvInjectionShader.SetInt("lpvDimension", lpvDimension);
			lpvInjectionShader.SetFloat("cascadeBoundary", cascadeBoundary);
			lpvInjectionShader.SetTexture(kernelHandle, "lightingTexture", lightingTexture);
			lpvInjectionShader.SetTexture(kernelHandle, "positionTexture", positionTexture);
			lpvInjectionShader.SetTexture(kernelHandle, "normalTexture", normalTexture);
			lpvInjectionShader.Dispatch(kernelHandle, screenSpaceVPLTextureResolution.x, screenSpaceVPLTextureResolution.y, 1);
		
		}

	}

	// Function to propagate the lighting stored as spherical harmonics in the LPV grid to its neightbouring cells
	private void LPVGridPropagation (ref LPVCascade cascade) {

		int kernelHandle = lpvPropagationShader.FindKernel("CSMain");

		lpvPropagationShader.SetTexture(kernelHandle, "lpvRedSHOutput", cascade.lpvRedPropagationBuffer);
		lpvPropagationShader.SetTexture(kernelHandle, "lpvGreenSHOutput", cascade.lpvGreenPropagationBuffer);
		lpvPropagationShader.SetTexture(kernelHandle, "lpvBlueSHOutput", cascade.lpvBluePropagationBuffer);

		if (backBuffering) {
			if (bDisplayBackBuffer) {
				lpvPropagationShader.SetTexture(kernelHandle, "lpvRedSHInput", cascade.lpvRedSH);
				lpvPropagationShader.SetTexture(kernelHandle, "lpvGreenSHInput", cascade.lpvGreenSH);
				lpvPropagationShader.SetTexture(kernelHandle, "lpvBlueSHInput", cascade.lpvBlueSH);
			} else {
				lpvPropagationShader.SetTexture(kernelHandle, "lpvRedSHInput", cascade.lpvRedSHBackBuffer);
				lpvPropagationShader.SetTexture(kernelHandle, "lpvGreenSHInput", cascade.lpvGreenSHBackBuffer);
				lpvPropagationShader.SetTexture(kernelHandle, "lpvBlueSHInput", cascade.lpvBlueSHBackBuffer);
			}
		} else {
			lpvPropagationShader.SetTexture(kernelHandle, "lpvRedSHInput", cascade.lpvRedSH);
			lpvPropagationShader.SetTexture(kernelHandle, "lpvGreenSHInput", cascade.lpvGreenSH);
			lpvPropagationShader.SetTexture(kernelHandle, "lpvBlueSHInput", cascade.lpvBlueSH);
		}

		lpvPropagationShader.SetInt("lpvDimension", lpvDimension);
		lpvPropagationShader.Dispatch(kernelHandle, lpvDimension, lpvDimension, lpvDimension);

		kernelHandle = lpvPropagationCompositionShader.FindKernel("CSMain");

		lpvPropagationCompositionShader.SetTexture(kernelHandle, "lpvRedSHInput", cascade.lpvRedPropagationBuffer);
		lpvPropagationCompositionShader.SetTexture(kernelHandle, "lpvGreenSHInput", cascade.lpvGreenPropagationBuffer);
		lpvPropagationCompositionShader.SetTexture(kernelHandle, "lpvBlueSHInput", cascade.lpvBluePropagationBuffer);

		if (backBuffering) {
			if (bDisplayBackBuffer) {
				lpvPropagationCompositionShader.SetTexture(kernelHandle, "lpvRedSHOutput", cascade.lpvRedSH);
				lpvPropagationCompositionShader.SetTexture(kernelHandle, "lpvGreenSHOutput", cascade.lpvGreenSH);
				lpvPropagationCompositionShader.SetTexture(kernelHandle, "lpvBlueSHOutput", cascade.lpvBlueSH);
			} else {
				lpvPropagationCompositionShader.SetTexture(kernelHandle, "lpvRedSHOutput", cascade.lpvRedSHBackBuffer);
				lpvPropagationCompositionShader.SetTexture(kernelHandle, "lpvGreenSHOutput", cascade.lpvGreenSHBackBuffer);
				lpvPropagationCompositionShader.SetTexture(kernelHandle, "lpvBlueSHOutput", cascade.lpvBlueSHBackBuffer);
			}
		} else {
			lpvPropagationCompositionShader.SetTexture(kernelHandle, "lpvRedSHOutput", cascade.lpvRedSH);
			lpvPropagationCompositionShader.SetTexture(kernelHandle, "lpvGreenSHOutput", cascade.lpvGreenSH);
			lpvPropagationCompositionShader.SetTexture(kernelHandle, "lpvBlueSHOutput", cascade.lpvBlueSH);
		}

		lpvPropagationCompositionShader.Dispatch(kernelHandle, lpvDimension, lpvDimension, lpvDimension);

	}

	// Render the scene from all the RSM cameras
	private void RenderRSMTextures () {

		for (int i = 0; i < rsmCameras.Count; ++i) {
			if (rsmCameras[i] != null) {
				if (rsmVPLInjection) {
					rsmCameras[i].GetComponent<RSMCameraScript> ().RenderRSM ();
				}
			}
		}

	}

	// Called when the scene is rendered into the framebuffer
	void OnRenderImage (RenderTexture source, RenderTexture destination) {

		if (backBuffering) {

			++currentPropagationStep;

			if (currentPropagationStep >= propagationSteps) {
				currentPropagationStep = 0;
				bDisplayBackBuffer = !bDisplayBackBuffer;
				LPVGridCleanup (ref firstCascade);
				LPVGridCleanup (ref secondCascade);
				LPVGridCleanup (ref thirdCascade);
				RenderRSMTextures ();
			}

		} else {

			currentPropagationStep = 0;
			bDisplayBackBuffer = false;
			LPVGridCleanup (ref firstCascade);
			LPVGridCleanup (ref secondCascade);
			LPVGridCleanup (ref thirdCascade);
			RenderRSMTextures ();

		}

		lpvRenderMaterial.SetMatrix ("InverseViewMatrix", GetComponent<Camera>().cameraToWorldMatrix);
		lpvRenderMaterial.SetMatrix ("InverseProjectionMatrix", GetComponent<Camera>().projectionMatrix.inverse);
		lpvRenderMaterial.SetFloat ("firstCascadeBoundary", firstCascadeBoundary);
		lpvRenderMaterial.SetFloat ("secondCascadeBoundary", secondCascadeBoundary);
		lpvRenderMaterial.SetFloat ("thirdCascadeBoundary", thirdCascadeBoundary);
		lpvRenderMaterial.SetFloat ("lpvDimension", lpvDimension);
		lpvRenderMaterial.SetVector ("playerPosition", this.transform.position);

		Graphics.Blit (source, lightingTexture);
		Graphics.Blit (source, positionTexture, lpvRenderMaterial, 0);
		Graphics.Blit (source, normalTexture, lpvRenderMaterial, 1);

		LPVGridInjection (ref firstCascade, firstCascadeBoundary);
		LPVGridInjection (ref secondCascade, secondCascadeBoundary);
		LPVGridInjection (ref thirdCascade, thirdCascadeBoundary);

		if (backBuffering) {
			LPVGridPropagation (ref firstCascade);
			LPVGridPropagation (ref secondCascade);
			LPVGridPropagation (ref thirdCascade);
		} else {

			for (int i = 0; i < propagationSteps; ++i) {
				LPVGridPropagation (ref firstCascade);
				LPVGridPropagation (ref secondCascade);
				LPVGridPropagation (ref thirdCascade);
			}

		}

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