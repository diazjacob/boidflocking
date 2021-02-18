using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoidManager : MonoBehaviour
{
    
	[SerializeField] private ComputeShader compute; //The compute shader

	//All boids and their movement scripts
	private BoidMovement[] _allBoids;
	private List<BoidMovement> _allBoidScripts = new List<BoidMovement>();

	//The behaviour move target.
	[SerializeField] private GameObject moveTarget;

	[Space]
	public Bounds tankBounds; //The bounds of the space for "bounds correction"
	[Space]

	[Header("Simulation Settings:")]
	public bool debug;
	public bool boundsCorrection;
	public bool targeting;
	public bool seperation;
	public bool alignment;
	public bool cohesion;
	public bool useOptimizedCalculator;

	//Global variables for controlling the system
	public float ViewDist;
	private float ViewDistSquared;
	[Range(0,1f)] public float TooCloseDist;
	private float TooCloseDistSquared;

	//For displaying the center of mass of all boids.
	[Space]
	[Header("Display Options:")]
	[SerializeField] private bool _showBoidCenter;
	[SerializeField] private bool _showBoidCenterDist;
	private float averageDistToTarget = 0;

	//Simple get functions
	public BoidMovement[] GetAllBoids() { return _allBoids; }
	public Vector3 GetMoveTarget() { return moveTarget.transform.position; }

	//Measurments
	public float GetDistToTarget() { return averageDistToTarget; }

	//The texture data, only serialized so it can be checked easily in the inspector.
	[SerializeField] private RenderTexture _positionTexture;
	[SerializeField] private Texture2D _tex;
	[SerializeField] private Vector3[] _positions;
	[SerializeField] private Vector3[] _directions;
	private int _kernelIndex;
	private const int TEX_DIMENTION = 325;


	[Space]
	[Space]
	//Used for writing the texture buffer to a material to be seen at runtime.
	[SerializeField] private GameObject _shaderTextureDisplay;
	[SerializeField] private Material _shaderTextureDisplayMat;
	private Material _trueTexDisplayMat;

	private void Awake()
	{
		//Retriveing all boids on startup
		_allBoids = FindObjectsOfType<BoidMovement>();

		//We're not using a zbuffer or stencil, so no render texture depth needed.
		//Setting up the render texture to link to the compute shader
		_positionTexture = new RenderTexture(TEX_DIMENTION, 3, 0);
		_positionTexture.enableRandomWrite = true;
		_positionTexture.Create();
		
		//Point filtering so no texture sampling is skewed by filters.
		_positionTexture.filterMode = FilterMode.Point;
		//_positionTexture.autoGenerateMips = false;

		//Setting up the texture
		_tex = new Texture2D(TEX_DIMENTION, 3, TextureFormat.ARGB32, false);
		_tex.filterMode = FilterMode.Point;
		_tex.wrapMode = TextureWrapMode.Clamp;

		_positions = new Vector3[TEX_DIMENTION];
		_directions = new Vector3[TEX_DIMENTION];

		//Initalizing all boids and performing dependency injecion.
		_allBoidScripts.Clear();
		for (int i = 0; i < _allBoids.Length; i++)
		{
			var script = _allBoids[i].GetComponent<BoidMovement>();
			_allBoidScripts.Add(script);
			script.SetManager(this, i, _allBoids.Length, _tex);
		}
	}

	void Start()
	{
		//Setting the display texture settings
		var renderer = _shaderTextureDisplay.GetComponent<MeshRenderer>();
		_trueTexDisplayMat = new Material(_shaderTextureDisplayMat);
		renderer.material = _trueTexDisplayMat;

		//Finding the kernel index and linking the texture
		_kernelIndex = compute.FindKernel("CSMain");
		compute.SetTexture(_kernelIndex, "Result", _positionTexture);
	}

	public Texture2D GetPositions()
	{
		//Allows the return of the calculated texture
		return _tex;
	}

	//Allows the agents to send their data to the manager.
	public void UpdateInfo(Vector3 pos, Vector3 forward, int id)
	{
		_positions[id] = pos;
		_directions[id] = forward;
	}

	void Update ()
	{
		//Updating calculation values
		ViewDistSquared = ViewDist * ViewDist;
		TooCloseDistSquared = ViewDistSquared * TooCloseDist * TooCloseDist;

		CalculateCloseBoids();

		//Update display plane texture
		_trueTexDisplayMat.SetTexture("_MainTex", _tex);
	}

	private void CalculateCloseBoids()
	{
		//We create our buffers, populate them, and send them off
		ComputeBuffer _inBuffer = new ComputeBuffer(_positions.Length, sizeof(float) * 3);
		_inBuffer.SetData(_positions);
		compute.SetBuffer(_kernelIndex, "In", _inBuffer);

		ComputeBuffer _inBufferDir = new ComputeBuffer(_positions.Length, sizeof(float) * 3);
		_inBufferDir.SetData(_directions);
		compute.SetBuffer(_kernelIndex, "InDir", _inBufferDir);

		//Sending extra data.
		compute.SetInt("NumberOfBoids", _allBoids.Length);
		compute.SetFloat("ViewDist", ViewDistSquared);
		compute.SetFloat("ViewDistTooClose", TooCloseDistSquared);

		compute.SetTexture(_kernelIndex, "Result", _positionTexture);

		//Performing the work
		compute.Dispatch(_kernelIndex, 1, 1, 1);

		//Releasing the buffers.
		_inBuffer.Release();
		_inBufferDir.Release();

		//A BETTER METHOD OF COPYING
		//Temporarily setting the shader texture to the active camera render texture
		//So we can leverage camera rendering techniques to sample our texture result.
		RenderTexture currRend = RenderTexture.active;
		RenderTexture.active = _positionTexture;
		_tex.ReadPixels(new Rect(0, 0, TEX_DIMENTION, 3), 0, 0);
		_tex.Apply();
		RenderTexture.active = currRend;

		//OLD COPY METHOD
		//Graphics.CopyTexture(_positionTexture, _tex);

	}


	//Drawing the editor data, strictly visual.
	private void OnDrawGizmosSelected()
	{
		if(Application.isPlaying)
		{
			if (_showBoidCenter)
			{
				Vector3 pos = Vector3.zero;
				for (int i = 0; i < _allBoids.Length; i++)
				{
					pos += _allBoids[i].transform.position;
				}

				pos /= _allBoids.Length;

				averageDistToTarget = pos.magnitude;

				Gizmos.DrawWireSphere(pos, 1);

				if (_showBoidCenterDist) Gizmos.DrawLine(pos, moveTarget.transform.position);

			}
		}

	}


}







