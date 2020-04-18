﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CoreScript.Utility
{
    [System.Serializable]
    public class Path
    {
        [SerializeField, HideInInspector]
        List<Vector2> points;
        [SerializeField, HideInInspector]
        bool isClosed;
        [SerializeField, HideInInspector]
        bool autoSetControlPoints;

        public Path(Vector2 centre)
        {
            points = new List<Vector2>
            {
                centre + Vector2.left,
                centre + (Vector2.left + Vector2.up) * .5f,
                centre + (Vector2.right + Vector2.down) * .5f,
                centre + Vector2.right
            };
        }

        public Vector2 this[int i] { get { return points[i]; } }

        public bool IsClosed
        {
            get { return isClosed; }
            set
            {
                isClosed = value;

                if (isClosed)
                {
                    points.Add(points[NumPoints - 1] * 2 - points[NumPoints - 2]);
                    points.Add(points[0] * 2 - points[1]);
                    if (autoSetControlPoints)
                    {
                        AutoSetAnchorControlPoints(0);
                        AutoSetAnchorControlPoints(NumPoints - 3);
                    }
                    return;
                }

                points.RemoveRange(NumPoints - 2, 2);
                if (autoSetControlPoints)
                    AutoSetStartAndEndControls();
            }
        }
        public bool AutoSetControlPoints
        {
            get { return autoSetControlPoints; }
            set
            {
                autoSetControlPoints = value;
                if (autoSetControlPoints)
                    AutoSetAllControlPoints();
            }
        }

        public int NumPoints { get { return points.Count; } }
        public int NumSegments { get { return points.Count / 3; } }

        public void AddSegment(Vector2 anchorPos)
        {
            points.Add(points[NumPoints - 1] * 2 - points[NumPoints - 2]);
            points.Add((points[NumPoints - 1] + anchorPos) * .5f);
            points.Add(anchorPos);

            if (autoSetControlPoints)
                AutoSetAllAffectedControlPoints(NumPoints - 1);
        }

        public void SplitSegment(Vector2 anchorPos, int segmentIndex)
        {
            points.InsertRange(segmentIndex * 3 + 2, new Vector2[] { Vector2.zero, anchorPos, Vector2.zero });
            if (autoSetControlPoints)
            {
                AutoSetAllAffectedControlPoints(segmentIndex * 3 + 3);
                return;
            }
            AutoSetAnchorControlPoints(segmentIndex * 3 + 3);
        }

        public void DeleteSegment(int anchorIndex)
        {
            if (NumSegments < 2)
                return;

            if (anchorIndex == 0)
            {
                if (isClosed)
                    points[NumPoints - 1] = points[2];

                points.RemoveRange(0, 3);
            }
            else if (!isClosed && anchorIndex == NumPoints - 1)
                points.RemoveRange(anchorIndex - 2, 3);
            else
                points.RemoveRange(anchorIndex - 1, 3);

        }

        public Vector2[] GetPointsInSegment(int i)
        {
            return new Vector2[] { points[i * 3], points[i * 3 + 1], points[i * 3 + 2], points[LoopIndex(i * 3 + 3)] };
        }

        public void MovePoint(int i, Vector2 pos)
        {
            Vector2 deltaMove = pos - points[i];

            if (i % 3 != 0 && autoSetControlPoints)
                return;

            points[i] = pos;

            if (autoSetControlPoints)
            {
                AutoSetAllAffectedControlPoints(i);
                return;
            }

            if (i % 3 == 0)
            {
                if (i + 1 < NumPoints || isClosed)
                {
                    points[LoopIndex(i + 1)] += deltaMove;
                }
                if (i - 1 >= 0 || isClosed)
                {
                    points[LoopIndex(i - 1)] += deltaMove;
                }
                return;
            }

            bool nextPointIsAnchor = (i + 1) % 3 == 0;
            int correspondingControlIndex = (nextPointIsAnchor) ? i + 2 : i - 2;
            int anchorIndex = (nextPointIsAnchor) ? i + 1 : i - 1;

            if (correspondingControlIndex >= 0 && correspondingControlIndex < NumPoints || isClosed)
            {
                float dst = (points[LoopIndex(anchorIndex)] - points[LoopIndex(correspondingControlIndex)]).magnitude;
                Vector2 dir = (points[LoopIndex(anchorIndex)] - pos).normalized;
                points[LoopIndex(correspondingControlIndex)] = points[LoopIndex(anchorIndex)] + dir * dst;
            }
        }

        public Vector2[] CalculateEvenlySpacedPoints(float spacing, float res = 1f)
        {
            List<Vector2> evenlySpacePoint = new List<Vector2>
            {
                points[0]
            };

            Vector2 previousPoint = points[0];
            float dstSinceLastEvenPoint = 0;
            for (int segmentIndex = 0; segmentIndex < NumSegments; segmentIndex++)
            {
                Vector2[] pts = GetPointsInSegment(segmentIndex);
                float controlNetLength = (pts[0] - pts[1]).magnitude + (pts[1] - pts[2]).magnitude + (pts[2] - pts[3]).magnitude;
                float estimatedCurveLegth = (pts[0] - pts[3]).magnitude + controlNetLength * .5f;
                int division = Mathf.CeilToInt(estimatedCurveLegth * res * 10);
                float t = 0, deltaT = 1f / division;
                while (t <= 1)
                {
                    t += deltaT;
                    Vector2 pointOnCurve = UtilityCode.CubicLerp(pts, t);
                    dstSinceLastEvenPoint += (previousPoint - pointOnCurve).magnitude;
                    while (dstSinceLastEvenPoint >= spacing)
                    {
                        float overShotDst = dstSinceLastEvenPoint - spacing;
                        Vector2 newEvenlySpacePoint = pointOnCurve + (previousPoint - pointOnCurve).normalized * overShotDst;
                        evenlySpacePoint.Add(newEvenlySpacePoint);
                        dstSinceLastEvenPoint = overShotDst;
                        previousPoint = newEvenlySpacePoint;
                    }

                    previousPoint = pointOnCurve;

                }
            }

            return evenlySpacePoint.ToArray();
        }

        void AutoSetAllAffectedControlPoints(int updatedAnchorIndex)
        {
            for (int i = updatedAnchorIndex - 3; i <= updatedAnchorIndex + 3; i += 3)
            {
                if (i >= 0 && i < NumPoints || isClosed)
                    AutoSetAnchorControlPoints(LoopIndex(i));
            }

            AutoSetStartAndEndControls();
        }

        void AutoSetAllControlPoints()
        {
            for (int i = 0; i < NumPoints; i += 3)
                AutoSetAnchorControlPoints(i);

            AutoSetStartAndEndControls();
        }

        void AutoSetAnchorControlPoints(int anchorIndex)
        {
            Vector2 anchorPos = points[anchorIndex];
            Vector2 dir = Vector2.zero;
            float[] neighbourDistances = new float[2];

            if (anchorIndex - 3 >= 0 || isClosed)
            {
                Vector2 offset = points[LoopIndex(anchorIndex - 3)] - anchorPos;
                dir += offset.normalized;
                neighbourDistances[0] = offset.magnitude;
            }
            if (anchorIndex + 3 >= 0 || isClosed)
            {
                Vector2 offset = points[LoopIndex(anchorIndex + 3)] - anchorPos;
                dir -= offset.normalized;
                neighbourDistances[1] = -offset.magnitude;
            }

            dir.Normalize();

            for (int i = 0; i < 2; i++)
            {
                int controlIndex = anchorIndex + i * 2 - 1;
                if (controlIndex >= 0 && controlIndex < NumPoints || isClosed)
                    points[LoopIndex(controlIndex)] = anchorPos + dir * neighbourDistances[i] * .5f;
            }
        }

        void AutoSetStartAndEndControls()
        {
            if (isClosed)
                return;

            points[1] = (points[0] + points[2]) * .5f;
            points[NumPoints - 2] = (points[NumPoints - 1] + points[NumPoints - 3]) * .5f;
        }

        int LoopIndex(int i)
        {
            return (i + NumPoints) % NumPoints;
        }

    }
}