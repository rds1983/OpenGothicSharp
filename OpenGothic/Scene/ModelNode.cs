using DigitalRiseModel;

namespace OpenGothic.Scene;

/// <summary>
/// A placed model instance. The transform positions the whole model; each part is drawn
/// either rigidly (a static mesh part, positioned by its bone) or deformed by the skeleton
/// (a skinned part).
/// </summary>
public class ModelNode : SceneNode
{
	public DrModelInstance Instance { get; set; }
}
