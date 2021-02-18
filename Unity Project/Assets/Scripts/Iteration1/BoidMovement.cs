using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using UnityEngine;
using UnityEngine.Networking;

public class BoidMovement : MonoBehaviour
{
	//Sight Code
	public const float GOLDRATIO = 1.61803398875f;
	[Tooltip("The number of rays casted per frame for boid sight tests")]
	[SerializeField] private int _sightRays;
	[Tooltip("The angle between each ray")]
	[SerializeField] [Range(0,1f)] private float _rayTurnFraction;
	[Tooltip("The radius of the sphere cast to catch obstacles better")]
	[SerializeField] private float _raycastRadius;
	[Space]
	
	//Attributes
	[Tooltip("The speed of the boid")]
	[SerializeField] private float _speed;
	//[SerializeField] private float _lazyness;
	[Tooltip("The possible turn angle per frame to avoid obstacles")]
	[SerializeField] private float _turnSpeed;
	[Tooltip("The fraction of possible turn angle per frame to track objects")]
	[SerializeField] private float _targetingTurnSpeed;
	[SerializeField] private float _seperateTurnSpeed;
	[SerializeField] private float _alignTurnSpeed;
	[SerializeField] private float _cohesionTurnSpeed;
	[Tooltip("The angle of sight for the boid")]
	[SerializeField] [Range(20f,180f)] private float _sightAngle;
	[Tooltip("The distance a boid can see obstructions")]
	[SerializeField] [Range(1, 6f)] private float _viewDistance;
	[SerializeField] [Range(0, 1f)] private float _tooCloseDistance;

	private Vector3[] _rayDirections;
	private BoidManager manager;
	private int _numBoids;

	private Texture2D _relativeValues;


	private int id;
	
	void Start ()
	{
		//Generating the fibonacci rays.
		_rayDirections = GenerateRays();
	}
	
	void Update ()
	{
		//performing one timestep of movement
		if (manager.useOptimizedCalculator) MovementOptimized();
		else Movement();

		//Updating personal values and the manager's values.
		_viewDistance = manager.ViewDist;
		_tooCloseDistance = manager.TooCloseDist;
		manager.UpdateInfo(transform.position, transform.forward, id);
	}
	
	//For dependency injection.
	public void SetManager(BoidManager tank, int d, int num, Texture2D tex) 
	{
		manager = tank;
		id = d;
		_numBoids = num;
		_relativeValues = tex;
	}

	private void MovementOptimized()
	{
		Vector3 bestDir;
		Quaternion target;

		//The best direction to turn to avoid obstacles
		bestDir = FindOpenDirection();
		//Applying Rotation
		target = Quaternion.LookRotation(bestDir, transform.up);
		transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _turnSpeed * Time.deltaTime);

		//Manually calculated targeting
		if (manager != null && manager.targeting)
		{
			bestDir = manager.GetMoveTarget() - transform.position;

			//DEBUG
			if (manager.debug) Debug.DrawRay(transform.position, bestDir.normalized, Color.cyan);

			RaycastHit ray;

			if (!Physics.SphereCast(transform.position, _raycastRadius, bestDir, out ray, _viewDistance))
			{
				target = Quaternion.LookRotation(bestDir, transform.up);
				transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _targetingTurnSpeed * Time.deltaTime);
			}
		}

		if (_numBoids > 0 && manager != null)
		{
			//Here in the optimized code we are simply handed the behaviour values to use !
			//MUCH faster!

			_relativeValues = manager.GetPositions();

			Color CohesionIN = _relativeValues.GetPixel(id, 0);
			Vector3 CohesionDir = new Vector3(CohesionIN.r, CohesionIN.g, CohesionIN.b);
			Color AlignmentIN = _relativeValues.GetPixel(id, 1);
			Vector3 AlignmentDir = new Vector3(AlignmentIN.r, AlignmentIN.g, AlignmentIN.b);
			Color SperationIN = _relativeValues.GetPixel(id, 2);
			Vector3 SeperationDir = -new Vector3(SperationIN.r, SperationIN.g, SperationIN.b);

			//print("Cohesion Vector: " + CohesionDir + "|| Color Source: " + CohesionIN + "|| At index: (" + id + ", 0)");

			//Steer away from close boids
			if (manager.seperation && SeperationDir.magnitude > Vector3.kEpsilon)
			{
				//DEBUG
				if (manager.debug) Debug.DrawLine(transform.position, transform.position + SeperationDir, Color.red);


				RaycastHit ray;
				if (!Physics.SphereCast(transform.position, _raycastRadius, SeperationDir, out ray, _viewDistance))
				{
					target = Quaternion.LookRotation(SeperationDir, transform.up);
					transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _seperateTurnSpeed * Time.deltaTime);
				}
			}

			//Line up with other boids
			if (manager.alignment && AlignmentDir.magnitude > Vector3.kEpsilon)
			{
				//DEBUG
				if (manager.debug) Debug.DrawLine(transform.position, transform.position + AlignmentDir, Color.green);

				RaycastHit ray;
				if (!Physics.SphereCast(transform.position, _raycastRadius, AlignmentDir, out ray, _viewDistance))
				{
					target = Quaternion.LookRotation(AlignmentDir, transform.up);
					transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _alignTurnSpeed * Time.deltaTime);
				}
			}

			//Steer toward average positon of all others.
			if (manager.cohesion && CohesionDir.magnitude > Vector3.kEpsilon)
			{
				//DEBUG
				if (manager.debug) Debug.DrawLine(transform.position, transform.position + CohesionDir, Color.blue);

				RaycastHit ray;
				if (!Physics.SphereCast(transform.position, _raycastRadius, CohesionDir, out ray, _viewDistance))
				{
					target = Quaternion.LookRotation(CohesionDir, transform.up);
					transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _cohesionTurnSpeed * Time.deltaTime);
				}
			}
		}

		//If there's an edge case of the boid outside the bounds, move toward the center of the bounds
		if (manager != null && !manager.tankBounds.Contains(transform.position) && manager.boundsCorrection)
		{
			bestDir = manager.tankBounds.center - transform.position;

			//DEBUG
			if (manager.debug) Debug.DrawRay(transform.position, bestDir.normalized, Color.yellow);

			RaycastHit ray;

			if (!Physics.SphereCast(transform.position, _raycastRadius, bestDir, out ray, _viewDistance))
			{
				target = Quaternion.LookRotation(bestDir, transform.up);
				transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _targetingTurnSpeed * Time.deltaTime);
			}
		}



		//Movement forward
		transform.position += transform.forward * _speed * Time.deltaTime; //Move froward
	}

	//OLD Movement code, not efficeient.
	private void Movement()
	{
		Vector3 bestDir;
		Quaternion target;

		BoidMovement[] allBoids = manager.GetAllBoids();
		List<Transform> closeBoids = new List<Transform>();
		
		for(int i = 0; i < allBoids.Length; i++)
		{
			GameObject other = allBoids[i].gameObject;
			if(Vector3.Distance(transform.position, other.transform.position) < _viewDistance)
			{
				Vector3 dirTo = other.transform.position - transform.position;
				if (Vector3.Angle(transform.forward, dirTo) < _sightAngle)
				{
					closeBoids.Add(other.transform);
				}
			}
		}
		closeBoids.Remove(gameObject.transform);

		//The best direction to turn to avoid obstacles
		bestDir = FindOpenDirection();
		//Applying Rotation
		target = Quaternion.LookRotation(bestDir, transform.up);
		transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _turnSpeed * Time.deltaTime);

		if (manager != null && manager.targeting)
		{
			bestDir = manager.GetMoveTarget() - transform.position;

			//DEBUG
			if (manager.debug) Debug.DrawRay(transform.position, bestDir.normalized, Color.cyan);

			RaycastHit ray;

			if (!Physics.SphereCast(transform.position, _raycastRadius, bestDir, out ray, _viewDistance))
			{
				target = Quaternion.LookRotation(bestDir, transform.up);
				transform.rotation = Quaternion.RotateTowards(transform.rotation, target,  _targetingTurnSpeed * Time.deltaTime);
			}
		}

		if (closeBoids.Count > 0 && manager != null)
		{
			Vector3 avgPos = Vector3.zero;
			Vector3 avgDir = Vector3.zero;
			Vector3 avgTooClosePos = Vector3.zero;
			int tooCloseCount = 0;
			bool TooCloseValid = false;
			for(int i = 0; i < closeBoids.Count; i++)
			{
				avgPos += closeBoids[i].position;
				avgDir += closeBoids[i].forward;

				if(Vector3.Distance(transform.position, closeBoids[i].position) < (_tooCloseDistance * _viewDistance))
				{
					avgTooClosePos += closeBoids[i].position;
					tooCloseCount++;
					TooCloseValid = true;
				}
			}
			avgPos /= closeBoids.Count;
			avgDir /= closeBoids.Count;

			//TEST
			//TEST

			avgTooClosePos /= tooCloseCount;

			Vector3 avgPosDir = Vector3.Normalize(avgPos - transform.position);
			Vector3 avgTooCloseDir = -Vector3.Normalize(avgTooClosePos - transform.position);


			//Steer away from close boids
			if (manager.seperation && TooCloseValid)
			{
				//DEBUG
				if (manager.debug) Debug.DrawLine(transform.position, transform.position + avgTooCloseDir, Color.red);
				

				RaycastHit ray;
				if (!Physics.SphereCast(transform.position, _raycastRadius, avgTooCloseDir, out ray, _viewDistance))
				{
					target = Quaternion.LookRotation(avgTooCloseDir, transform.up);
					transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _seperateTurnSpeed * Time.deltaTime);
				}
			}

			//Line up with other boids
			if (manager.alignment)
			{
				//DEBUG
				if (manager.debug) Debug.DrawLine(transform.position, transform.position + avgDir, Color.green);

				RaycastHit ray;
				if (!Physics.SphereCast(transform.position, _raycastRadius, avgDir, out ray, _viewDistance))
				{
					target = Quaternion.LookRotation(avgDir, transform.up);
					transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _alignTurnSpeed * Time.deltaTime);
				}
			}

			//Line up with other boid's centers
			if (manager.cohesion)
			{
				//DEBUG
				if (manager.debug) Debug.DrawLine(transform.position, transform.position + avgPosDir, Color.blue);

				RaycastHit ray;
				if (!Physics.SphereCast(transform.position, _raycastRadius, avgPosDir, out ray, _viewDistance))
				{
					target = Quaternion.LookRotation(avgPosDir, transform.up);
					transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _cohesionTurnSpeed * Time.deltaTime);
				}
			}
		}

		//If there's an edge case of the boid outside the bounds, move toward the center of the bounds
		if (manager != null && !manager.tankBounds.Contains(transform.position) && manager.boundsCorrection)
		{
			bestDir = manager.tankBounds.center - transform.position;

			//DEBUG
			if (manager.debug) Debug.DrawRay(transform.position, bestDir.normalized, Color.yellow);

			RaycastHit ray;

			if (!Physics.SphereCast(transform.position, _raycastRadius, bestDir, out ray, _viewDistance))
			{
				target = Quaternion.LookRotation(bestDir, transform.up);
				transform.rotation = Quaternion.RotateTowards(transform.rotation, target,  _targetingTurnSpeed * Time.deltaTime);
			}
		}



		//Movement forward
		transform.position += transform.forward * _speed * Time.deltaTime; //Move froward
	}

	private Vector3 FindOpenDirection()
	{
		//Here we sample all vision rays in order to find
		//a ray that is not hitting an obstacle. If we find a free
		//ray then we want to steer towards it with a high priority.

		_rayTurnFraction = 1/GOLDRATIO;

		Vector3 safeDir = transform.forward;
		float maxSafeDist = 0;
		
		for (int i = 0; i < _rayDirections.Length; i++)
		{
			RaycastHit ray;
			Vector3 dir = transform.TransformDirection(_rayDirections[i]);

			if (Physics.SphereCast(transform.position, _raycastRadius, dir, out ray, _viewDistance))
			{
				Debug.DrawLine(ray.point, ray.point + Vector3.up * 0.01f, Color.magenta, 1f);
				if (ray.distance > maxSafeDist)
				{
					safeDir = dir;
					maxSafeDist = ray.distance;
				}
			}
			else
			{
				return dir;
			}
		}

		return safeDir;
	}

	//The generation of the fibonacci rays on startup.
	private Vector3[] GenerateRays()
	{
		List<Vector3> directions = new List<Vector3>();
		//This is the point generation code.
		_rayTurnFraction = 1 / GOLDRATIO;
		for (int i = 0; i < _sightRays; i++)
		{
			float d = i / (_sightRays + 1f);
			float incline = Mathf.Acos(1 - 2 * d);
			float azimuth = (2 * Mathf.PI * _rayTurnFraction * i) % (2 * Mathf.PI);

			float x = Mathf.Sin(incline) * Mathf.Cos(azimuth);
			float y = Mathf.Sin(incline) * Mathf.Sin(azimuth);
			float z = Mathf.Cos(incline);

			Vector3 point = new Vector3(x, y, z).normalized;

			if (Vector3.Angle(Vector3.forward, point) < _sightAngle)
			{
				directions.Add(point);
			}
		}

		return directions.ToArray();
	}

	//Visual ray debug view.
	private void OnDrawGizmosSelected()
	{
		if (manager != null && manager.debug)
		{

			for (int i = 0; i < _rayDirections.Length; i++)
			{
				RaycastHit ray;
				Vector3 dir = transform.TransformDirection(_rayDirections[i]);

				Gizmos.DrawLine(transform.position, dir * _viewDistance + transform.position);
			}
					
			
		}
	}
	

}
