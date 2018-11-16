﻿using System.ComponentModel;
using AGS.API;

namespace AGS.Engine
{
    [PropertyFolder]
	public class AGSSayConfig : ISayConfig
	{ 
        public AGSSayConfig(IFontFactory fonts, IDefaultsSettings defaults)
		{
            //todo: should the factory provide speech config?
            if (fonts != null)
            {
                TextConfig = fonts.GetTextConfig(font: defaults.SpeechFont,
                  autoFit: AutoFit.TextShouldWrapAndLabelShouldFitHeight, alignment: Alignment.BottomLeft);
            }
			TextDelay = 70;
			LabelSize = new SizeF (250f, 200f);
			SkipText = SkipText.ByTimeAndMouse;
			BackgroundColor = Colors.Transparent;
		}

#pragma warning disable CS0067
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore CS0067

        public static AGSSayConfig FromConfig(ISayConfig config, float paddingBottomOffset = 0f)
		{
			AGSSayConfig sayConfig = new AGSSayConfig(null, null);
            sayConfig.TextConfig = config.TextConfig;
            sayConfig.TextConfig.PaddingBottom += paddingBottomOffset;
			sayConfig.TextDelay = config.TextDelay;
			sayConfig.SkipText = config.SkipText;
			sayConfig.LabelSize = config.LabelSize;
			sayConfig.Border = config.Border;
			sayConfig.BackgroundColor = config.BackgroundColor;
            sayConfig.PortraitConfig = config.PortraitConfig;
			return sayConfig;
		}

		#region ISayConfig implementation

		public ITextConfig TextConfig { get; set; }

		public int TextDelay { get; set; }

		public SkipText SkipText { get; set; }

		public SizeF LabelSize { get; set; }

		public IBorderStyle Border { get; set; }

		public Color BackgroundColor { get; set; }

        public PointF TextOffset { get; set; }

        public IPortraitConfig PortraitConfig { get; set; }

        #endregion

        public override string ToString() => TextConfig?.Brush?.Color.ToString() ?? "No color set";
    }
}