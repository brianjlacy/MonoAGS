﻿//Code taken from: https://github.com/juhgiyo/EpPathFinding.cs/blob/master/EpPathFinding/PathFinder
/*! 
@file BaseGrid.cs
@author Woong Gyu La a.k.a Chris. <juhgiyo@gmail.com>
		<http://github.com/juhgiyo/eppathfinding.cs>
@date July 16, 2013
@brief BaseGrid Interface
@version 2.0
@section LICENSE
The MIT License (MIT)
Copyright (c) 2013 Woong Gyu La <juhgiyo@gmail.com>
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
@section DESCRIPTION
An Interface for the BaseGrid Class.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;

namespace AGS.Engine
{
	public class EPNode : IComparable
	{
		public int x;
		public int y;
		public bool walkable;
		public float heuristicStartToEndLen; // which passes current node
		public float startToCurNodeLen;
		public float? heuristicCurNodeToEndLen;
		public bool isOpened;
		public bool isClosed;
		public Object parent;

		public EPNode(int iX, int iY, bool? iWalkable = null)
		{
			this.x = iX;
			this.y = iY;
			this.walkable = (iWalkable.HasValue ? iWalkable.Value : false);
			this.heuristicStartToEndLen = 0;
			this.startToCurNodeLen = 0;
			this.heuristicCurNodeToEndLen = null;
			this.isOpened = false;
			this.isClosed = false;
			this.parent = null;

		}

		public EPNode(EPNode b)
		{
			this.x = b.x;
			this.y = b.y;
			this.walkable = b.walkable;
			this.heuristicStartToEndLen = b.heuristicStartToEndLen;
			this.startToCurNodeLen = b.startToCurNodeLen;
			this.heuristicCurNodeToEndLen = b.heuristicCurNodeToEndLen;
			this.isOpened = b.isOpened;
			this.isClosed = b.isClosed;
			this.parent = b.parent;
		}

		public void Reset(bool? iWalkable = null)
		{
			if (iWalkable.HasValue)
				walkable = iWalkable.Value;
			this.heuristicStartToEndLen = 0;
			this.startToCurNodeLen = 0;
			this.heuristicCurNodeToEndLen = null;
			this.isOpened = false;
			this.isClosed = false;
			this.parent = null;
		}


		public int CompareTo(object iObj)
		{
			EPNode tOtherNode = (EPNode)iObj;
			float result=this.heuristicStartToEndLen - tOtherNode.heuristicStartToEndLen;
			if (result > 0.0f)
				return 1;
			else if (result == 0.0f)
				return 0;
			return -1;
		}

		public static List<GridPos> Backtrace(EPNode iNode)
		{
			List<GridPos> path = new List<GridPos>();
			path.Add(new GridPos(iNode.x, iNode.y));
			while (iNode.parent != null)
			{
				iNode = (EPNode)iNode.parent;
				path.Add(new GridPos(iNode.x, iNode.y));
			}
			path.Reverse();
			return path;
		}


		public override int GetHashCode()
		{
			return x ^ y;
		}

		public override bool Equals(System.Object obj)
		{
			// If parameter is null return false.
			if (obj == null)
			{
				return false;
			}

			// If parameter cannot be cast to Point return false.
			EPNode p = obj as EPNode;
			if ((System.Object)p == null)
			{
				return false;
			}

			// Return true if the fields match:
			return (x == p.x) && (y == p.y);
		}

		public bool Equals(EPNode p)
		{
			// If parameter is null return false:
			if ((object)p == null)
			{
				return false;
			}

			// Return true if the fields match:
			return (x == p.x) && (y == p.y);
		}

		public static bool operator ==(EPNode a, EPNode b)
		{
			// If both are null, or both are same instance, return true.
			if (System.Object.ReferenceEquals(a, b))
			{
				return true;
			}

			// If one is null, but not both, return false.
			if (((object)a == null) || ((object)b == null))
			{
				return false;
			}

			// Return true if the fields match:
			return a.x == b.x && a.y == b.y;
		}

		public static bool operator !=(EPNode a, EPNode b)
		{
			return !(a == b);
		}

	}

	public abstract class BaseGrid
	{

		public BaseGrid()
		{
		}

		public BaseGrid(BaseGrid b)
		{
			m_gridRect = new GridRect(b.m_gridRect);
			width = b.width;
			height = b.height;
		}

		protected GridRect m_gridRect;
		public GridRect gridRect
		{
			get { return m_gridRect; }
		}

		public abstract int width { get; protected set; }

		public abstract int height { get; protected set; }

		public abstract EPNode GetNodeAt(int iX, int iY);

		public abstract bool IsWalkableAt(int iX, int iY);

		public abstract bool SetWalkableAt(int iX, int iY, bool iWalkable);

		public abstract EPNode GetNodeAt(GridPos iPos);

		public abstract bool IsWalkableAt(GridPos iPos);

		public abstract bool SetWalkableAt(GridPos iPos, bool iWalkable);

		public List<EPNode> GetNeighbors(EPNode iNode, bool iCrossCorners, bool iCrossAdjacentPoint)
		{
			int tX = iNode.x;
			int tY = iNode.y;
			List<EPNode> neighbors = new List<EPNode>();
			bool tS0 = false, tD0 = false,
			tS1 = false, tD1 = false,
			tS2 = false, tD2 = false,
			tS3 = false, tD3 = false;

			GridPos pos = new GridPos();
			if (this.IsWalkableAt(pos.Set(tX, tY - 1)))
			{
				neighbors.Add(GetNodeAt(pos));
				tS0 = true;
			}
			if (this.IsWalkableAt(pos.Set(tX + 1, tY)))
			{
				neighbors.Add(GetNodeAt(pos));
				tS1 = true;
			}
			if (this.IsWalkableAt(pos.Set(tX, tY + 1)))
			{
				neighbors.Add(GetNodeAt(pos));
				tS2 = true;
			}
			if (this.IsWalkableAt(pos.Set(tX - 1, tY)))
			{
				neighbors.Add(GetNodeAt(pos));
				tS3 = true;
			}
			if (iCrossCorners && iCrossAdjacentPoint)
			{
				tD0 = true;
				tD1 = true;
				tD2 = true;
				tD3 = true;
			}
			else if (iCrossCorners)
			{
				tD0 = tS3 || tS0;
				tD1 = tS0 || tS1;
				tD2 = tS1 || tS2;
				tD3 = tS2 || tS3;
			}
			else
			{
				tD0 = tS3 && tS0;
				tD1 = tS0 && tS1;
				tD2 = tS1 && tS2;
				tD3 = tS2 && tS3;
			}

			if (tD0 && this.IsWalkableAt(pos.Set(tX - 1, tY - 1)))
			{
				neighbors.Add(GetNodeAt(pos));
			}
			if (tD1 && this.IsWalkableAt(pos.Set(tX + 1, tY - 1)))
			{
				neighbors.Add(GetNodeAt(pos));
			}
			if (tD2 && this.IsWalkableAt(pos.Set(tX + 1, tY + 1)))
			{
				neighbors.Add(GetNodeAt(pos));
			}
			if (tD3 && this.IsWalkableAt(pos.Set(tX - 1, tY + 1)))
			{
				neighbors.Add(GetNodeAt(pos));
			}
			return neighbors;
		}

		public abstract void Reset();

		public abstract BaseGrid Clone();

	}
}