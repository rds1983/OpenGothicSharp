using Nursia.Rendering;
using Nursia.SceneGraph;

namespace OpenGothic.Viewer.UI
{
	internal interface IViewerWidget
	{
		Camera Camera { get; }
		RenderStatistics RenderStatistics { get; }
	}
}
