﻿using System;
using AGS.API;

namespace AGS.Engine
{
	public partial class AGSSlider
	{
		partial void init(Resolver resolver, IImage image)
		{
			RenderLayer = AGSLayers.UI;
			IgnoreScalingArea = true;
			IgnoreViewport = true;
			Anchor = new AGSPoint ();
			Image = image;
			Enabled = true;
		}

		public void ApplySkin(ISlider slider)
		{
			throw new NotSupportedException ();
		}
	}
}
