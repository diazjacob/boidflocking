//A DEPRECIATED CLASS FOR TAKING MEASURMENTS WITHIN THE RESEARCH PROJECT

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;

public class MeasurmentTaker : MonoBehaviour {

	//[SerializeField] private string _csvName = "data.csv";

	[Header("Average Targeting Position Distance Metrics:")]
	private List<Sample> avgPosDist;
	[SerializeField] private float _avgPosDistInterval = 0.01f;
	[SerializeField] private int _numberOfMeasurments = 100;
	private int _currMeasurmentNum = -1;
	private float _timer = 0;
	private float _measurmentStartTime = 0;


	private BoidManager manager;

	// Use this for initialization
	void Start () 
	{
		manager = GetComponent<BoidManager>();
		avgPosDist = new List<Sample>();
	}
	
	// Update is called once per frame
	void Update () 
	{
		if(_currMeasurmentNum != -1)
        {
			if (_timer > _avgPosDistInterval)
			{
				avgPosDist.Add(new Sample(Time.time - _measurmentStartTime, manager.GetDistToTarget()));

				_currMeasurmentNum++;
				_timer = 0;
			}
			else _timer += Time.deltaTime;

			if (_currMeasurmentNum + 1 > _numberOfMeasurments)
			{
				_currMeasurmentNum = -1;
				CSVMaker(avgPosDist);
			}
		}

	}

	[ContextMenu("Start AVG POS DIST Measurment")]
	private void StartMeasurment()
    {
		_measurmentStartTime = Time.time;
		_currMeasurmentNum = 0;
		avgPosDist = new List<Sample>();
	}

	public string CSVMaker(List<Sample> samples)
	{
		var sb = new StringBuilder("Time,Value");
		foreach (var sample in samples)
		{
			sb.Append('\n').Append(sample.time.ToString()).Append(',').Append(sample.value.ToString());
		}

		return sb.ToString();
	}

	[System.Serializable]
	public class Sample
	{
		public Sample(float t, float v)
		{
			time = t;
			value = v;
		}

		public float time;
		public float value;
	}
}


