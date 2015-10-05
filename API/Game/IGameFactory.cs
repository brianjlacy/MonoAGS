﻿using System;
using System.Drawing;

namespace AGS.API
{
	public interface IGameFactory
	{
		IGraphicsFactory Graphics { get; }
		ISoundFactory Sound { get; }

		IOutfit LoadOutfitFromFolders(string baseFolder, string walkLeftFolder = null, string walkRightFolder = null,
			string walkDownFolder = null, string walkUpFolder = null, string idleLeftFolder = null, string idleRightFolder = null,
			string idleDownFolder = null, string idleUpFolder = null, string speakLeftFolder = null, string speakRightFolder = null,
			string speakDownFolder = null, string speakUpFolder = null,
			int delay = 4, IAnimationConfiguration animationConfig = null, ILoadImageConfig loadConfig = null);

		int GetInt(string name, int defaultValue = 0);
		float GetFloat(string name, float defaultValue = 0f);
		string GetString(string name, string defaultValue = null);

		IPanel GetPanel(IImage image, float x, float y, bool addToUi = true);
		IPanel GetPanel(float width, float height, float x, float y, bool addToUi = true);
		IPanel GetPanel(string imagePath, float x, float y, ILoadImageConfig loadConfig = null, bool addToUi = true); 

		ILabel GetLabel(string text, float width, float height, float x, float y, ITextConfig config = null, bool addToUi = true);
		IButton GetButton(IAnimation idle, IAnimation hovered, IAnimation pushed, float x, 
			float y, string text = "", ITextConfig config = null, bool addToUi = true, float width = -1f, float height = -1f);
		IButton GetButton(string idleImagePath, string hoveredImagePath, string pushedImagePath, 
			float x, float y, string text = "", ITextConfig config = null, bool addToUi = true, float width = -1f, float height = -1f);

		IObject GetObject(string[] sayWhenLook = null, string[] sayWhenInteract = null);
		ICharacter GetCharacter(IOutfit outfit, string[] sayWhenLook = null, string[] sayWhenInteract = null);
		IObject GetHotspot(string maskPath, string hotspot, string[] sayWhenLook = null, string[] sayWhenInteract = null);
		IObject GetHotspot(Bitmap maskBitmap, string hotspot, string[] sayWhenLook = null, string[] sayWhenInteract = null);

		IEdge GetEdge(float value = 0f);
		IRoom GetRoom(string id, float leftEdge = 0f, float rightEdge = 0f, float bottomEdge = 0f, float topEdge = 0f);

		IInventoryWindow GetInventoryWindow(float width, float height, float itemWidth, float itemHeight, float x, float y, 
			ICharacter character = null, bool addToUi = true);
		IInventoryItem GetInventoryItem(IObject graphics, IObject cursorGraphics, bool playerStartsWithItem = false);
		IInventoryItem GetInventoryItem(string hotspot, string graphicsFile, string cursorFile = null, ILoadImageConfig loadConfig = null,
			bool playerStartsWithItem = false);

		IDialogOption GetDialogOption(string text, ITextConfig config = null, ITextConfig hoverConfig = null,
			bool speakOption = true, bool showOnce = false);
		IDialog GetDialog(float x = 0f, float y = 0f, IObject graphics = null, 
			bool showWhileOptionsAreRunning = false, params IDialogOption[] options);

		void RegisterCustomData(ICustomSerializable customData);
	}
}

