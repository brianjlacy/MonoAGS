﻿using System;
using AGS.API;
using AGS.Engine;
using System.Drawing;

namespace DemoGame
{
	public class InventoryItems
	{
		private const string _baseFolder = "../../Assets/Inventory/";

		public static IInventoryItem Bottle { get; private set; }
		public static IInventoryItem VoodooDoll { get; private set; }
		public static IInventoryItem Poster { get; private set; }
		public static IInventoryItem Manual { get; private set; }

		public void Load(IGameFactory factory)
		{
			AGSLoadImageConfig loadConfig = new AGSLoadImageConfig
			{
				TransparentColorSamplePoint = new Point(0,0),
			};
			Bottle = factory.GetInventoryItem("Bottle", "../../Assets/Rooms/EmptyStreet/bottle.bmp");
			VoodooDoll = factory.GetInventoryItem("Voodoo Doll", _baseFolder + "voodooDoll.bmp", null, loadConfig, true);
			Poster = factory.GetInventoryItem("Poster", _baseFolder + "poster.bmp", playerStartsWithItem: true);
			Manual = factory.GetInventoryItem("Manual", _baseFolder + "manual.bmp", null, loadConfig, true);
		}
	}
}
