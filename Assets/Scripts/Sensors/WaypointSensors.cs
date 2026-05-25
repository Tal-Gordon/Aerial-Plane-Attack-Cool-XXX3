using UnityEngine;
using System.Collections;
using System;

namespace Assets.Scripts.Sensors
{
	public class WaypointSensors: BasicFlightSensors
	{
		public override SensorType SensorType => SensorType.Waypoint;

		[HideInInspector]
		public Transform currentWaypoint;

		private float maxDistance = 2000f;
		private float[] cachedFinalObs;

		public override float[] GetObservationData()
		{
			float[] baseObs = base.GetObservationData();

			if (cachedFinalObs == null)
				cachedFinalObs = new float[GetSensorCount()];

			Array.Copy(baseObs, cachedFinalObs, baseObs.Length);
			float[] finalObs = cachedFinalObs;

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