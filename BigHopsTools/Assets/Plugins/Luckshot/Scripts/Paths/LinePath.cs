﻿using UnityEngine;
using System;
using UnityEngine.Serialization;
using NaughtyAttributes;

namespace Luckshot.Paths
{
	public class LinePath : PathBase
	{
		[SerializeField, HideInInspector]
		private Vector3[] points = null;
		public Vector3[] Points
		{
			get
			{
				if (points == null || points.Length == 0)
					Reset();

				return points;
			}

			set
			{
				if (value != points)
				{
					value = points;
					RecalculateClockwise();

					OnPathChanged(this);
				}
			}
		}

		public int PointCount
		{ get { return Points.Length; } }

		private bool clockwise = false;
		public bool Clockwise
		{ get { return clockwise; } }

		private void Awake()
		{
			RecalculateClockwise();
		}

		[Button("Recalculate Clockwise")]
		private void RecalculateClockwise()
		{
			float sum = 0f;
			for (int i = 0; i < points.Length; i++)
			{
				Vector3 a = points[i];
				Vector3 b = points[(int)Mathf.Repeat(i - 1, points.Length)];

				sum += (b.x - a.x) * (b.z + a.z);
			}

			clockwise = sum > 0;
		}

		public override Vector3 GetNormal(float t)
		{
			Vector3 velocity = GetDirection(t);
			Vector3 normal = Vector3.Cross(Vector3.up, velocity).normalized;
			if (!clockwise)
				normal *= -1f;

			return normal;
		}

		public override Vector3 GetPoint(float t)
		{
			t = Mathf.Clamp01(t);

			int numPoints = loop ? points.Length : (points.Length - 1);
			float alphaPerPoint = 1f / (float)numPoints;

			int index = Mathf.FloorToInt(t / alphaPerPoint);
			index = Mathf.Clamp(index, 0, points.Length - 1);

			int nextIndex = index + 1;
			if (loop)
				nextIndex = (int)Mathf.Repeat(nextIndex, points.Length);
			else
				nextIndex = Mathf.Clamp(nextIndex, 0, points.Length - 1);

			float remainder = t % alphaPerPoint;

			Vector3 localPos = Vector3.Lerp(points[index], points[nextIndex], remainder / alphaPerPoint);
			return transform.TransformPoint(localPos);
		}

		public override float GetNearestAlpha(Vector3 point, int iterations = 10)
		{
			int nearestIter = 0;
			float nearestAlpha = 0f;
			float nearestDistance = float.MaxValue;

			// Get a general spot along the spline that our point is near
			// This is more accurate then immediately halfing
			int totalIterations = iterations * PointCount;
			for (int i = 0; i < totalIterations; i++)
			{
				float iterAlpha = i / (float)totalIterations;

				Vector3 iterPos = GetPoint(iterAlpha);
				float iterDistance = Vector3.Distance(point, iterPos);

				if (iterDistance < nearestDistance)
				{
					nearestIter = i;
					nearestAlpha = iterAlpha;
					nearestDistance = iterDistance;
				}
			}

			// Within a range around closest large iteration,
			// keep halving range till we have a good approximation
			float minIterAlpha = Mathf.Max(0, nearestIter - 1) / (float)totalIterations;
			float maxIterAlpha = Mathf.Min(totalIterations, nearestIter + 1) / (float)totalIterations;
			for (int i = 0; i < totalIterations; i++)
			{
				float iterAlpha = Mathf.Lerp(minIterAlpha, maxIterAlpha, i / (float)totalIterations);

				Vector3 iterPos = GetPoint(iterAlpha);
				float iterDistance = Vector3.Distance(point, iterPos);

				if (iterDistance < nearestDistance)
				{
					nearestAlpha = iterAlpha;
					nearestDistance = iterDistance;
				}
			}

			return nearestAlpha;
		}

		public Vector3 GetNearestPathPoint(Vector3 position, int numIterations = 10)
		{ return GetPoint(GetNearestAlpha(position, numIterations)); }

		public override Vector3 GetVelocity(float t)
		{
			t = Mathf.Clamp01(t);

			int numPoints = loop ? points.Length : (points.Length - 1);
			float alphaPerPoint = 1f / (float)numPoints;

			int index = Mathf.FloorToInt(t / alphaPerPoint);
			index = Mathf.Clamp(index, 0, points.Length - 1);

			int nextIndex = index + 1;
			if (loop)
				nextIndex = (int)Mathf.Repeat(nextIndex, points.Length);
			else
				nextIndex = Mathf.Clamp(nextIndex, 0, points.Length - 1);

			return transform.TransformVector(points[nextIndex] - points[index]);
		}

		public override Vector3 GetDirection(float alpha)
		{ return GetVelocity(alpha).normalized; }

		public override float GetLength()
		{
			float dist = 0f;

			Vector3 prevPos = transform.TransformPoint(Points[0]);
			for (int i = 1; i < Points.Length; i++)
			{
				Vector3 pos = transform.TransformPoint(Points[i]);
				dist += (pos - prevPos).magnitude;
				prevPos = pos;
			}

			return dist;
		}

		public void AddPoint()
		{
			Vector3 point = Points[Points.Length - 1];
			Array.Resize(ref points, Points.Length + 1);
			point.x += 1f;
			Points[Points.Length - 1] = point;

			if (loop)
			{
				Points[Points.Length - 1] = Points[0];
			}

			OnPathChanged(this);
		}

		public void RemovePoint()
		{
			Array.Resize(ref points, Points.Length - 1);

			if (loop)
			{
				Points[Points.Length - 1] = Points[0];
			}

			OnPathChanged(this);
		}

		public void Reset()
		{
			points = new Vector3[]
			{
				new Vector3(1f, 0f, 0f),
				new Vector3(2f, 0f, 0f)
			};

			OnPathChanged(this);
		}
	}
}