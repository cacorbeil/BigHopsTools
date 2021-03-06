﻿using UnityEngine;
using System;
using UnityEngine.Serialization;

namespace Luckshot.Paths
{
	public class SplinePath : PathBase
	{
		[SerializeField, HideInInspector]
		Vector3[] points = null;
		public Vector3[] Points
		{
			get
			{
				if (points == null || points.Length == 0)
					Reset();

				return points;
			}
		}

		[SerializeField, HideInInspector]
		BezierControlPointMode[] modes = null;
		protected BezierControlPointMode[] Modes
		{
			get
			{
				if (modes == null || modes.Length == 0)
					Reset();

				return modes;
			}
		}

		public int ModeCount
		{ get { return Modes.Length; } }

		public int ControlPointCount
		{ get { return Points.Length; } }

		public Vector3 GetControlPointLocal(int index)
		{ return Points[index]; }

		public Vector3 GetControlPoint(int index)
		{ return transform.TransformPoint(Points[index]); }

		public void SetControlPoint(int index, Vector3 point, bool moveGuides = false)
		{ SetControlPointLocal(index, transform.InverseTransformPoint(point), moveGuides); }

		public void SetControlPointLocal(int index, Vector3 point, bool moveGuides = false)
		{
			if (index % 3 == 0 && moveGuides)
			{
				Vector3 delta = point - Points[index];
				if (loop)
				{
					if (index == 0)
					{
						Points[1] += delta;
						Points[Points.Length - 2] += delta;
						Points[Points.Length - 1] = point;
					}
					else if (index == Points.Length - 1)
					{
						Points[0] = point;
						Points[1] += delta;
						Points[index - 1] += delta;
					}
					else
					{
						Points[index - 1] += delta;
						Points[index + 1] += delta;
					}
				}
				else
				{
					if (index > 0)
					{
						Points[index - 1] += delta;
					}
					if (index + 1 < Points.Length)
					{
						Points[index + 1] += delta;
					}
				}
			}

			Points[index] = point;
			EnforceMode(index);

			OnPathChanged(this);
		}

		public BezierControlPointMode GetControlPointMode(int index)
		{ return Modes[(index + 1) / 3]; }

		public void SetControlPointMode(int index, BezierControlPointMode mode)
		{
			int modeIndex = (index + 1) / 3;
			Modes[modeIndex] = mode;
			if (loop)
			{
				if (modeIndex == 0)
				{
					Modes[Modes.Length - 1] = mode;
				}
				else if (modeIndex == Modes.Length - 1)
				{
					Modes[0] = mode;
				}
			}

			EnforceMode(index);
		}

		private void EnforceMode(int index)
		{
			int modeIndex = (index + 1) / 3; // What the hell is this doing? Every xth point has a specific control mode
			BezierControlPointMode mode = Modes[modeIndex];
			if (mode == BezierControlPointMode.Free || !loop && (modeIndex == 0 || modeIndex == Modes.Length - 1))
			{
				return;
			}

			int middleIndex = modeIndex * 3;
			int fixedIndex, enforcedIndex;
			if (index <= middleIndex)
			{
				fixedIndex = middleIndex - 1;
				if (fixedIndex < 0)
				{
					fixedIndex = Points.Length - 2;
				}
				enforcedIndex = middleIndex + 1;
				if (enforcedIndex >= Points.Length)
				{
					enforcedIndex = 1;
				}
			}
			else
			{
				fixedIndex = middleIndex + 1;
				if (fixedIndex >= Points.Length)
				{
					fixedIndex = 1;
				}
				enforcedIndex = middleIndex - 1;
				if (enforcedIndex < 0)
				{
					enforcedIndex = Points.Length - 2;
				}
			}

			Vector3 middle = Points[middleIndex];
			Vector3 enforcedTangent = middle - Points[fixedIndex];
			if (mode == BezierControlPointMode.Aligned)
			{
				enforcedTangent = enforcedTangent.normalized * Vector3.Distance(middle, Points[enforcedIndex]);
			}

			Vector3 newPoint = middle + enforcedTangent;
			if (Points[enforcedIndex] != newPoint)
			{
				Points[enforcedIndex] = middle + enforcedTangent;
				OnPathChanged(this);
			}
		}

		public int CurveCount
		{ get { return (Points.Length - 1) / 3; } }

		public override Vector3 GetPoint(float t)
		{
			if (Points.Length < 4)
				return Vector3.zero;

			int i;
			if (t >= 1f)
			{
				t = 1f;
				i = Points.Length - 4;
			}
			else
			{
				t = Mathf.Clamp01(t) * CurveCount;
				i = (int)t;
				t -= i;
				i *= 3;
			}
			return transform.TransformPoint(BezierUtils.GetPoint(Points[i], Points[i + 1], Points[i + 2], Points[i + 3], t));
		}

		public override float GetNearestAlpha(Vector3 point, int iterations = 10)
		{
			int nearestIter = 0;
			float nearestAlpha = 0f;
			float nearestDistance = float.MaxValue;

			// Get a general spot along the spline that our point is near
			// This is more accurate then immediately halfing
			int totalIterations = iterations * ControlPointCount;
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

		public Vector3 GetNearestSplinePoint(Vector3 position, int numIterations = 10)
		{ return GetPoint(GetNearestAlpha(position, numIterations)); }

		public override Vector3 GetVelocity(float t)
		{
			int i;
			if (t >= 1f)
			{
				t = 1f;
				i = Points.Length - 4;
			}
			else
			{
				t = Mathf.Clamp01(t) * CurveCount;
				i = (int)t;
				t -= i;
				i *= 3;
			}

			return transform.TransformPoint(BezierUtils.GetFirstDerivative(Points[i], Points[i + 1], Points[i + 2], Points[i + 3], t)) - transform.position;
		}

		public override Vector3 GetDirection(float t)
		{ return GetVelocity(t).normalized; }

		public override Vector3 GetNormal(float t)
		{
			Vector3 forward = GetDirection(t);
			Vector3 right = Vector3.Cross(transform.right, forward);
			return right.normalized;
		}

		public override float GetLength()
		{
			float dist = 0f;
			float alphaIter = 0.01f;

			Vector3 prevPos = GetPoint(0f);

			float alpha = 0;
			while (alpha < 1f)
			{
				alpha += alphaIter;
				alpha = Mathf.Clamp01(alpha);

				Vector3 pos = GetPoint(alpha);
				dist += (pos - prevPos).magnitude;
				prevPos = pos;
			}

			return dist;
		}

		public virtual void AddCurve()
		{
			Vector3 point = Points[Points.Length - 1];
			Array.Resize(ref points, Points.Length + 3);
			point.x += 1f;
			Points[Points.Length - 3] = point;
			point.x += 1f;
			Points[Points.Length - 2] = point;
			point.x += 1f;
			Points[Points.Length - 1] = point;

			Array.Resize(ref modes, Modes.Length + 1);
			Modes[Modes.Length - 1] = BezierControlPointMode.Mirrored;
			EnforceMode(Points.Length - 4);

			if (loop)
			{
				Points[Points.Length - 1] = Points[0];
				Modes[Modes.Length - 1] = Modes[0];
				EnforceMode(0);
			}
		}

		public virtual void RemoveCurve()
		{
			Array.Resize(ref points, Points.Length - 3);
			Array.Resize(ref modes, Modes.Length - 1);

			if (loop)
			{
				Points[Points.Length - 1] = Points[0];
				Modes[Modes.Length - 1] = Modes[0];
				EnforceMode(0);
			}
		}

		public virtual void Reset()
		{
			points = new Vector3[]
			{
			new Vector3(1f, 0f, 0f),
			new Vector3(2f, 0f, 0f),
			new Vector3(3f, 0f, 0f),
			new Vector3(4f, 0f, 0f)
			};

			modes = new BezierControlPointMode[]
			{
				BezierControlPointMode.Mirrored,
				BezierControlPointMode.Mirrored
			};
		}

		protected virtual void OnDrawGizmosSelected()
		{
			Gizmos.color = Color.green.SetA(0.3f);
			Gizmos.DrawSphere(GetPoint(0f), 0.1f);

			Gizmos.color = Color.red.SetA(0.3f);
			Gizmos.DrawSphere(GetPoint(1f), 0.1f);

			int numIterations = 10 * ControlPointCount;
			for (int i = 1; i < numIterations; i++)
			{
				Gizmos.color = Color.Lerp(Color.green, Color.red, i / (float)numIterations);
				Gizmos.DrawLine(GetPoint(i / (float)numIterations), GetPoint((i - 1) / (float)numIterations));
			}
		}
	}
}
