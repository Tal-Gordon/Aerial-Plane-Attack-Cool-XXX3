using UnityEngine;
using System.Collections;
using System;

namespace Assets.Scripts.Sensors
{
	public class WaypointSensors: BasicFlightSensors
	{
		[HideInInspector]
		public Transform currentWaypoint;

		private float maxDistance = 2000f; // Cap for Distance normalization

		public override float[] GetObservationData()
		{
			float[] baseObs = base.GetObservationData();

			float[] finalObs = new float[GetSensorCount()];
			Array.Copy(baseObs, finalObs, baseObs.Length);

			int index = baseObs.Length;

			if (currentWaypoint != null)
			{
				// Local Direction
				Vector3 localPos = transform.InverseTransformPoint(currentWaypoint.position);
				Vector3 localDir = localPos.normalized;

				finalObs[index++] = localDir.x;
				finalObs[index++] = localDir.y;
				finalObs[index++] = localDir.z;

				// Normalized Distance
				float distance = localPos.magnitude / maxDistance;
				finalObs[index++] = distance;

				// Hoop Alignment
				Vector3 localHoopForward = transform.InverseTransformDirection(currentWaypoint.forward);

				finalObs[index++] = localHoopForward.x;
				finalObs[index++] = localHoopForward.y;
				finalObs[index++] = localHoopForward.z;
			}
			else
			{
				Debug.Log("No waypoints detected");
			}

			return finalObs;
		}

        public override int GetSensorCount()
		{
			return base.GetSensorCount() + 7;
		}
    }
}