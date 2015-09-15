﻿using System;
using OpenTK;

namespace AGS.Engine
{
	public interface IGLMatrices
	{
		Matrix4 ModelMatrix { get; }
		Matrix4 ViewportMatrix { get; }
	}
}

