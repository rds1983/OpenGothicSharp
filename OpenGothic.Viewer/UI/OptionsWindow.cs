using Nursia;
using OpenGothic.Viewer;

namespace OpenGothic.UI
{
	public partial class OptionsWindow
	{
		public OptionsWindow()
		{
			BuildUI();

			_propertiesGraphics.Object = Nrs.GraphicsSettings;
			_propertiesDebug.Object = Nrs.DebugSettings;
			_propertiesEnvironment.Object = Configuration.RenderEnvironment;
		}
	}
}