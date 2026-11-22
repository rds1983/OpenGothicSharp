using System.IO;

namespace OpenGothic;

public class Settings
{
	public const string UserFileName = "Gothic.ini";

	private readonly IniFile _defaults = new IniFile();
	private readonly IniFile _base = new IniFile();
	private readonly IniFile _systemPack = new IniFile();
	private readonly IniFile _user = new IniFile();

	public string UserFilePath { get; private set; }

	public void Load(string gothicPath, string workingDirectory)
	{
		var systemFolder = Path.Combine(gothicPath, "system");

		_systemPack.Load(Path.Combine(systemFolder, "SystemPack.ini"));
		_base.Load(Path.Combine(systemFolder, "Gothic.ini"));

		UserFilePath = Path.Combine(workingDirectory, UserFileName);
		_user.Load(UserFilePath);

		LoadDefaults();
	}

	public void Save()
	{
		if (UserFilePath == null)
		{
			return;
		}

		_user.Save(UserFilePath);
	}

	public float MusicVolume
	{
		get => GetFloat("SOUND", "musicVolume", 0.5f);
		set => Set("SOUND", "musicVolume", value);
	}

	public float SoundVolume
	{
		get => GetFloat("SOUND", "soundVolume", 0.5f);
		set => Set("SOUND", "soundVolume", value);
	}

	public bool MusicEnabled
	{
		get => GetInt("SOUND", "musicEnabled", 1) != 0;
		set => Set("SOUND", "musicEnabled", value ? 1 : 0);
	}

	public bool SoundEnabled
	{
		get => GetInt("SOUND", "soundEnabled", 1) != 0;
		set => Set("SOUND", "soundEnabled", value ? 1 : 0);
	}

	public float VideoBrightness
	{
		get => GetFloat("VIDEO", "zVidBrightness", 0.5f);
		set => Set("VIDEO", "zVidBrightness", value);
	}

	public float VideoContrast
	{
		get => GetFloat("VIDEO", "zVidContrast", 0.5f);
		set => Set("VIDEO", "zVidContrast", value);
	}

	public float VideoGamma
	{
		get => GetFloat("VIDEO", "zVidGamma", 0.5f);
		set => Set("VIDEO", "zVidGamma", value);
	}

	public float MouseSensitivity
	{
		get => GetFloat("GAME", "mouseSensitivity", 0.5f);
		set => Set("GAME", "mouseSensitivity", value);
	}

	public bool InvertLookaround
	{
		get => GetInt("GAME", "camLookaroundInverse", 0) != 0;
		set => Set("GAME", "camLookaroundInverse", value ? 1 : 0);
	}

	public int ModelDetail
	{
		get => GetInt("PERFORMANCE", "modelDetail", 1);
		set => Set("PERFORMANCE", "modelDetail", value);
	}

	public bool ShowFpsCounter
	{
		get => GetInt("DEBUG", "Show_FPS_Counter", 0) != 0;
		set => Set("DEBUG", "Show_FPS_Counter", value ? 1 : 0);
	}

	public float CameraFov
	{
		get
		{
			var fov = GetFloat("PARAMETERS", "VerticalFOV", 67.5f);
			return fov < 1.0f ? 67.5f : fov;
		}
		set => Set("PARAMETERS", "VerticalFOV", value);
	}

	public int FpsLimit
	{
		get => GetInt("PARAMETERS", "FPS_Limit", 0);
		set => Set("PARAMETERS", "FPS_Limit", value);
	}

	public string GetString(string section, string name, string defaultValue) =>
		Lookup(section, name) is { } value ? value : defaultValue;

	public int GetInt(string section, string name, int defaultValue)
	{
		var value = Lookup(section, name);
		return value != null && int.TryParse(value, out var result) ? result : defaultValue;
	}

	public float GetFloat(string section, string name, float defaultValue)
	{
		var value = Lookup(section, name);
		return value != null && float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : defaultValue;
	}

	public void Set(string section, string name, string value) => _user.Set(section, name, value);

	public void Set(string section, string name, int value) => _user.Set(section, name, value);

	public void Set(string section, string name, float value) => _user.Set(section, name, value);

	private string Lookup(string section, string name)
	{
		if (_user.Has(section, name))
		{
			return _user.GetString(section, name);
		}

		if (_base.Has(section, name))
		{
			return _base.GetString(section, name);
		}

		if (_systemPack.Has(section, name))
		{
			return _systemPack.GetString(section, name);
		}

		if (_defaults.Has(section, name))
		{
			return _defaults.GetString(section, name);
		}

		return null;
	}

	private void LoadDefaults()
	{
		_defaults.Set("GAME", "enableMouse", 1);
		_defaults.Set("GAME", "mouseSensitivity", 0.5f);
		_defaults.Set("GAME", "camLookaroundInverse", 0);
		_defaults.Set("PERFORMANCE", "modelDetail", 1);

		_defaults.Set("VIDEO", "zVidBrightness", 0.5f);
		_defaults.Set("VIDEO", "zVidContrast", 0.5f);
		_defaults.Set("VIDEO", "zVidGamma", 0.5f);

		_defaults.Set("SOUND", "musicEnabled", 1);
		_defaults.Set("SOUND", "musicVolume", 0.5f);
		_defaults.Set("SOUND", "soundEnabled", 1);
		_defaults.Set("SOUND", "soundVolume", 0.5f);

		_defaults.Set("PARAMETERS", "VerticalFOV", 67.5f);
		_defaults.Set("PARAMETERS", "FPS_Limit", 0);
		_defaults.Set("DEBUG", "Show_FPS_Counter", 0);
	}
}
